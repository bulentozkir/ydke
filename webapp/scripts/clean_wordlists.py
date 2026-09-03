# -*- coding: utf-8 -*-
"""Rule-based cleaner for the vocabulary data files.

Rather than hand-listing thousands of words per file, this removes entries
that fail objective, statable tests. Every rule is deliberately conservative:
it only fires when the entry is verifiably defective, so it can be run across
English, French and Italian files without per-file curation.

Rules
-----
PROPER    NOT APPLIED AUTOMATICALLY. Capitalisation mid-example looked like a
          clean proper-noun signal, but it flags ministry/admiral/braille/
          python, and guarding it with a dictionary instead protects the real
          names (the dictionary lists bolivia, medusa and judas too). Names are
          therefore removed from curated per-level lists built by
          scripts/_diag_cefrj_membership.py, as done in scripts/clean_en_a1.py.
          is_proper() is kept below because it is still a useful review aid.
INFLECT   The headword is a regular inflected form (-s/-ed/-en/-er/-est/-ly)
          of another headword that this same file already teaches, so it adds
          nothing and clutters the deck.
CRUDE     The headword, its definition, or its example contains profanity or
          adult content that does not belong in a school-facing word list.
BROKEN    The definition self-identifies as being about a different word than
          the headword: it starts with a gloss for another entry, or the
          example never uses the headword in any inflected form AND a real
          example was available (i.e. it is not the honest "no example"
          fallback).

Usage:
  python scripts/clean_wordlists.py --lang en                 # dry run
  python scripts/clean_wordlists.py --lang en --apply
  python scripts/clean_wordlists.py data/wordsa2.js --apply
"""
import argparse
import csv
import io
import os
import re
import sys
import tempfile
import urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import name_gazetteer                                            # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")

CEFRJ_URL = ("https://raw.githubusercontent.com/openlanguageprofiles/olp-en-cefrj/"
             "master/cefrj-vocabulary-profile-1.5.csv")
CEFRJ_PATH = os.path.join(tempfile.gettempdir(),
                          "cefrj-vocabulary-profile-1.5.csv")


def load_known_en():
    """Return (cefr, dictionary): two guards with deliberately different jobs.

    cefr        ~7k CEFR-J headwords plus their regular inflections and the
                calendar/language words. High precision "this is definitely
                real vocabulary a learner needs". Used to protect words from
                the name rules, because january, friday, martin and harry are
                all genuine first names or surnames as well.
    dictionary  a full 370k English word list. Broad but indiscriminate: it
                also contains bolivia, medusa and judas lowercased, so it can
                only be used to protect against the PLACE and INFLECT rules,
                never against the PERSON rule.
    """
    if not os.path.exists(CEFRJ_PATH):
        urllib.request.urlretrieve(CEFRJ_URL, CEFRJ_PATH)
    cefr = set()
    with io.open(CEFRJ_PATH, encoding="utf-8-sig", newline="") as fh:
        for row in csv.DictReader(fh):
            head = (row.get("headword") or "").strip().lower()
            for form in head.split("/"):
                form = form.strip().rstrip(".")
                if form:
                    cefr.add(form)
    # CEFR-J lists lemmas only, so also accept their regular inflections;
    # otherwise months/wanted/bigger look unknown and get cut.
    for w in list(cefr):
        cefr.update({w + "s", w + "es", w + "ed", w + "ing", w + "er",
                     w + "est", w + "ly"})
    cefr.update("""january february march april may june july august september
        october november december monday tuesday wednesday thursday friday
        saturday sunday english turkish german french italian spanish european
        christmas easter earth""".split())
    # Ordinary nouns that are also a surname or a town somewhere, and that
    # happen to appear capitalised in their harvested example. Reviewed by
    # hand from the NAME rule's dry-run output; all are real vocabulary.
    cefr.update("""confederation mauve gibbon tarsus purpura keystone morel
        amaryllis hermitage petunia nightingale grail ensign parry canton
        thane homestead brill slater popper porta arras astrakhan""".split())

    dict_path = os.path.join(tempfile.gettempdir(), "words_alpha.txt")
    if not os.path.exists(dict_path):
        urllib.request.urlretrieve(
            "https://raw.githubusercontent.com/dwyl/english-words/master/"
            "words_alpha.txt", dict_path)
    with io.open(dict_path, encoding="utf-8") as fh:
        dictionary = {line.strip().lower() for line in fh if line.strip()}
    return cefr, dictionary

LANG_FILES = {
    "en": ["wordsa1.js", "wordsa2.js", "wordsb1.js", "wordsb2.js",
           "wordsc1.js", "wordsc2.js", "toefl.js"],
    "fr": ["wordsa1fr.js", "wordsa2fr.js", "wordsb1fr.js", "wordsb2fr.js",
           "wordsc1fr.js", "wordsc2fr.js"],
    "it": ["wordsa1it.js", "wordsa2it.js", "wordsb1it.js", "wordsb2it.js",
           "wordsc1it.js", "wordsc2it.js"],
    "de": ["wordsa1gode.js", "wordsa2gode.js", "wordsb1gode.js",
           "wordsb2gode.js", "wordsc1gode.js", "wordsc2gode.js"],
}

ENTRY_RE = re.compile(r'\n  \{\s*\n?(?:[^{}]|\{[^{}]*\})*?\n?  \},', re.S)
FIELD_RE = {k: re.compile(r'%s:\s*"((?:\\.|[^"\\])*)"' % k)
            for k in ("word", "pos", "level", "definition", "example")}

# Vulgar slang and slurs. Matching this means the ENTRY is unsuitable, not
# just its example: for a non-English file the English half of `definition`
# IS the meaning of the headword, so "le trouduc - asshole; asshat" tells us
# the headword itself is obscene.
# Deliberately NOT included: anus, vagina, rectum, penis, testicle. Those
# appear in legitimate clinical definitions (enema, proctologist, vestibule)
# and are not vulgar in that register.
# Also deliberately NOT included: "retard". It matched "retards your fall"
# (parachute), "retards the clotting" (anticoagulant) and every French
# example containing "en retard", which simply means "late".
VULGAR_RE = re.compile(
    r"\b(fuck\w*|shit\w*|cunt\w*|bitch\w*|whore\w*|slut\w*|slattern|"
    r"dickhead|dickwad|fuckhead|shithead|asshat|asshole|arsehole|arse|"
    r"bastard\w*|wank\w*|blowjob|handjob|boner|bimbo|hooker|pimp|porn\w*|"
    r"masturbat\w*|orgasm\w*|buggery|sodom\w*|fuckpad|"
    r"nigger\w*|negro(es)?|faggot\w*|poof\b|"
    r"goddamn|goddam|motherfuck\w*|douchebag|"
    r"junkie|heroin|cocaine|marijuana|cannabis|reefer|stoned)\b", re.I)

# Kept as an alias so existing callers (_diag_bad_context.py) keep working.
CRUDE_RE = VULGAR_RE


FALLBACK_RE = re.compile(
    r"No example sentence available|No dictionary definition available|"
    r"Kein Beispielsatz|Aucune phrase d'exemple|Bu kelime için", re.I)

TOKEN_RE = re.compile(r"[^\W\d_]+", re.UNICODE)
SENT_START_RE = re.compile(r'(?:^|[.!?]\s+|["\u201c(]\s*)$')


def native(field):
    return field.split(" - ")[0]


def primary_gloss(definition):
    """The first two senses only of the English half of a definition.

    Harvested glosses list several synonyms and judging the whole string
    condemns an ordinary word for its coarsest one: "engueuler - to give
    someone a roasting, to tell someone off, to chew out, to give shit" is
    not a rude headword. But the leading sense alone is too narrow, because
    slang entries often lead with a euphemism ("le catin - harlot, slattern,
    whore"). The first two senses is the balance that classified every
    reviewed case correctly.
    """
    en = native(definition).strip()
    return ";".join(re.split(r"[;,]", en)[:2])


# Slang entries whose vulgar sense only appears from the third gloss onwards,
# so the two-gloss window above cannot see it. Confirmed by hand.
VULGAR_WORDS = {
    "défoncé", "le pouffiasse", "sturzbesoffen", "rumalbern", "rumsen",
    "hackedicht", "le tantouze", "l'alphonse", "cocain", "cannabin",
}


def is_vulgar_entry(word, definition, lang):
    # Only English headwords may be matched against English profanity: the
    # German verb "wanken" (to sway) and French "la douche" (shower) are not
    # rude, they just look like English swear words.
    if word.strip().lower() in VULGAR_WORDS:
        return True
    if lang == "en" and VULGAR_RE.search(word):
        return True
    return bool(VULGAR_RE.search(primary_gloss(definition)))


def lemmas(w):
    out = set()
    for suf, repl in (("s", ""), ("es", ""), ("ed", ""), ("ed", "e"),
                      ("en", ""), ("en", "e"), ("er", ""), ("er", "e"),
                      ("est", ""), ("est", "e"), ("ly", ""), ("ies", "y"),
                      ("ied", "y"), ("ier", "y"), ("iest", "y")):
        if w.endswith(suf) and len(w) - len(suf) >= 3:
            out.add(w[: len(w) - len(suf)] + repl)
    if len(w) > 4 and w[-1] == w[-2]:
        out.add(w[:-1])
    return {b for b in out if len(b) >= 3}


def is_proper(word, example):
    """True if the headword shows up capitalised mid-sentence in its example."""
    ex = native(example)
    if not word or not word[0].isalpha() or word[0].isupper():
        return False
    cap = word[0].upper() + word[1:]
    for m in re.finditer(r"\b%s\b" % re.escape(cap), ex):
        if not SENT_START_RE.search(ex[: m.start()]):
            return True
    return False


def fuzzy_in(word, tokens):
    w = word.casefold()
    if w in tokens:
        return True
    if len(w) < 5:
        return False
    for t in tokens:
        if abs(len(t) - len(w)) > 5:
            continue
        common = 0
        for a, b in zip(t, w):
            if a != b:
                break
            common += 1
        if common >= max(5, int(len(w) * 0.65)):
            return True
    return False


def clean_file(path, lang, apply_changes, guards):
    cefr, dictionary, people, places = guards
    known = cefr | dictionary
    text = io.open(path, encoding="utf-8").read()
    blocks = ENTRY_RE.findall(text)
    words = set()
    for b in blocks:
        m = FIELD_RE["word"].search(b)
        if m:
            words.add(m.group(1).strip().lower())

    counts, examples = {}, {}
    bad_example = []

    def repl(m):
        block = m.group(0)
        get = lambda k: (FIELD_RE[k].search(block).group(1)
                         if FIELD_RE[k].search(block) else "")
        word, definition, example = get("word"), get("definition"), get("example")
        w = word.strip().lower()
        reason = None

        if is_vulgar_entry(word, definition, lang):
            # For a non-English file the English half of `definition` is the
            # meaning of the headword, so an obscene gloss means an obscene
            # headword ("le trouduc - asshole; asshat; dickwad").
            reason = "CRUDE"
        elif VULGAR_RE.search(example):
            # The headword itself is fine (parachute, itchy, intestine) - only
            # the harvested sentence around it is not. Deleting the word would
            # lose good vocabulary, so record it for example replacement.
            bad_example.append(word)
        elif not cefr:
            pass          # no authority list for this language yet: only CRUDE
        elif w in cefr:
            pass          # verified real vocabulary: never delete
        elif (w in people or w in places) and is_proper(word, example):
            # Two independent signals must agree before we call something a
            # name. Membership alone is far too loose - the surname list holds
            # foster, crystal, echo, stark, levy, abbey and born - and
            # capitalisation alone flags every month and weekday. Requiring
            # BOTH (and that CEFR-J does not vouch for the word) leaves only
            # entries like "Bobby may watch TV until 7:00."
            reason = "NAME"
        elif w in known:
            pass
        elif any(b in words and b in known for b in lemmas(w)):
            reason = "INFLECT"

        if not reason:
            return block
        counts[reason] = counts.get(reason, 0) + 1
        examples.setdefault(reason, []).append(word)
        words.discard(w)
        return ""

    out = ENTRY_RE.sub(repl, text)
    before, after = len(blocks), len(ENTRY_RE.findall(out))
    total = sum(counts.values())
    print("%-20s %5d -> %5d  (-%d)  %s   [bad example: %d]" % (
        os.path.basename(path), before, after, total,
        " ".join("%s=%d" % kv for kv in sorted(counts.items())), len(bad_example)))
    for reason in sorted(examples):
        print("    %-8s %s" % (reason, ", ".join(examples[reason][:16])))

    if apply_changes and total:
        if out.count("{") != out.count("}"):
            print("    ABORT: brace imbalance, file left untouched")
            return 0
        io.open(path, "w", encoding="utf-8", newline="\n").write(out)
    return total


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="*")
    ap.add_argument("--lang")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    names = args.files or LANG_FILES.get(args.lang or "", [])
    if not names:
        print("give files or --lang " + "/".join(LANG_FILES))
        return 1
    lang = args.lang or "en"
    if lang == "en":
        cefr, dictionary = load_known_en()
        people, places = name_gazetteer.load()
        print("guards: %d cefr, %d dictionary\n" % (len(cefr), len(dictionary)))
    else:
        cefr, dictionary, people, places = set(), set(), set(), set()
    grand = 0
    for name in names:
        path = name if os.path.sep in name else os.path.join(DATA, name)
        if os.path.exists(path):
            grand += clean_file(path, lang, args.apply,
                                (cefr, dictionary, people, places)) or 0
    print("\nremoved %d entries%s" % (grand, "" if args.apply else "  (DRY RUN)"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
