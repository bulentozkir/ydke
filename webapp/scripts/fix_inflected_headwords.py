# -*- coding: utf-8 -*-
"""Plan/apply removal & repair of inflected-form headwords in data/words*.js.

A headword must be the dictionary (base/lemma) form: "abase", not "abasing";
"abfragen", not "Abfragen"; "Abgabe", not "Abgaben".

Detection
  * definition is a Wiktionary "form-of" stub
    (gerund of X / present participle of X / perfect participle of X /
     plural of X / inflection of X / bare "past participle")
  * English headword ending in -ing whose pos is "verb"

Resolution
  * lemma already present in the same language -> DELETE the inflected entry
  * lemma absent                               -> REWRITE entry to the lemma
                                                  (needs a new definition/example)

Usage
  python scripts/fix_inflected_headwords.py --plan     # write _inflected_plan.json
  python scripts/fix_inflected_headwords.py --report   # human-readable summary
  python scripts/fix_inflected_headwords.py --apply    # perform the deletions
"""
import argparse
import collections
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")
PLAN = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_inflected_plan.json")

FIELD_RE = re.compile(
    r'(?P<key>\bword|\bpos|\blevel|\bcategory|\bdefinition|\bexample)\s*:\s*'
    r'"(?P<val>(?:[^"\\]|\\.)*)"'
)
ENTRY_RE = re.compile(r"\{(?P<body>[^{}]*)\}", re.S)
ARTICLE_RE = re.compile(r"^(?:der|die|das|le|la|les|il|lo|i|gli|el|los|las|o|a|os|as)\s+", re.I)
ELISION_RE = re.compile(r"^l['\u2019]\s*", re.I)

FORM_OF = [
    ("gerund", re.compile(r"\bgerund of\s+([^\s;:,.()\u201c\u2018]+)", re.I)),
    ("present-participle", re.compile(r"\bpresent participle of\s+([^\s;:,.()\u201c\u2018]+)", re.I)),
    ("perfect-participle", re.compile(r"\b(?:perfect|past) participle of\s+([^\s;:,.()\u201c\u2018]+)", re.I)),
    ("plural", re.compile(r"\bplural of\s+([^\s;:,.()\u201c\u2018]+)", re.I)),
    ("singular", re.compile(r"\bsingular of\s+([^\s;:,.()\u201c\u2018]+)", re.I)),
    ("comparative", re.compile(r"\bcomparative of\s+[`']?([^\s;:,.()'`]+)", re.I)),
    ("superlative", re.compile(r"\bsuperlative of\s+[`']?([^\s;:,.()'`]+)", re.I)),
    ("inflection", re.compile(r"\binflection of\s+([^\s;:,.()\u201c\u2018]+)", re.I)),
]
BARE_PARTICIPLE = re.compile(r"^\(?(?:perfect|past|present)\s+participle\)?\.?$", re.I)
VERB_POS = {"verb", "verbe", "v", "v.", "verbo"}

# headwords that must never be touched even if their definition mentions a form-of
SKIP_WORDS = {
    ("en", "aborning"),      # archaic adverb, no valid lemma
    ("fr", "plus"),          # core adverb, definition merely cites a superlative
}

# German nominalised infinitives that are established nouns in their own right,
# not mere gerunds -- keep the headword, replace the useless definition.
DE_REAL_NOUNS = {
    "abkommen", "aufsehen", "bedenken", "eindringen", "eingreifen", "einkommen",
    "entgegenkommen", "entsetzen", "entzücken", "erstaunen", "essen", "grauen",
    "misstrauen", "bergsteigen", "vorgehen", "schlafengehen",
}

# strong participles whose infinitive cannot be derived mechanically
DE_STRONG_PARTICIPLES = {
    "zerschnitten": "zerschneiden",
    "überboten": "überbieten",
    "übergossen": "übergießen",
    "durchlitten": "durchleiden",
}


def lang_of(fname):
    stem = fname[:-3]
    for suf, lang in (("gode", "de"), ("fr", "fr"), ("it", "it"), ("es", "es"), ("pt", "pt")):
        if stem.endswith(suf):
            return lang
    return "en"


def norm(word):
    w = ELISION_RE.sub("", ARTICLE_RE.sub("", word.strip()))
    return w.casefold()


def parse(path):
    with open(path, encoding="utf-8", newline="") as fh:
        text = fh.read()
    entries = []
    for m in ENTRY_RE.finditer(text):
        body = m.group("body")
        if '"' not in body or "word" not in body:
            continue
        fields = {}
        for f in FIELD_RE.finditer(body):
            fields.setdefault(f.group("key"), f.group("val"))
        if "word" not in fields:
            continue
        fields["_start"] = m.start()
        fields["_end"] = m.end()
        entries.append(fields)
    return text, entries


def en_bases(w):
    stem = w[:-3]
    cands = [stem, stem + "e"]
    if len(stem) >= 3 and stem[-1] == stem[-2] and stem[-1] not in "aeiou":
        cands.append(stem[:-1])
    if stem.endswith("y"):
        cands.append(stem[:-1] + "ie")
    if stem.endswith("i"):
        cands.append(stem[:-1] + "y")
    return cands


def de_participle_to_infinitive(w):
    """Best-effort German participle -> infinitive."""
    cands = []
    base = w
    if base.endswith("iert"):
        cands.append(base[:-1] + "en")          # laminiert -> laminieren
    elif base.endswith("et"):
        cands.append(base[:-2] + "en")          # bemitleidet -> bemitleiden
        cands.append(base[:-1] + "n")
    elif base.endswith("ert") or base.endswith("elt"):
        cands.append(base[:-1] + "n")           # zertrümmert -> zertrümmern
    if base.endswith("t"):
        cands.append(base[:-1] + "en")
        cands.append(base[:-2] + "en")
    if base.endswith("en"):
        cands.append(base)
        if base.startswith("ge"):
            cands.append(base[2:])
    if base.endswith("end"):
        cands.append(base[:-1])  # present participle: -end -> -en
    # strip an internal/leading "ge" infix of separable verbs: angeschossen -> anschießen
    m = re.match(r"^([a-zA-ZäöüÄÖÜß]{2,7})ge(.+)$", base)
    if m:
        rest = m.group(2)
        for c in list(cands):
            pass
        stem = m.group(1) + rest
        cands.append(stem)
        if stem.endswith("t"):
            cands.append(stem[:-1] + "en")
        if not stem.endswith("en"):
            cands.append(stem + "en")
    if base.startswith("ge"):
        g = base[2:]
        cands.append(g)
        if g.endswith("t"):
            cands.append(g[:-1] + "en")
        if not g.endswith("en"):
            cands.append(g + "en")
    seen, out = set(), []
    for c in cands:
        if c and c != w and c not in seen:
            seen.add(c)
            out.append(c)
    return out


FORM_OF_PHRASES = (
    "gerund of", "present participle of", "perfect participle of",
    "past participle of", "plural of", "singular of", "inflection of",
)


def has_extra_gloss(definition, kind):
    """True when the definition carries real meaning beyond the form-of stub."""
    eng = definition.split(" - ")[0].strip()
    for phrase in FORM_OF_PHRASES:
        i = eng.lower().find(phrase)
        if i < 0:
            continue
        tail = eng[i + len(phrase):].lstrip().split(None, 1)
        rest = tail[1] if len(tail) > 1 else ""
        return len(rest.strip(" .;:,\u201c\u201d()\u2018\u2019")) > 2
    return False


def classify(entry, lang):
    """Return (kind, [lemma candidates]) or (None, [])."""
    word = entry["word"]
    bare = ELISION_RE.sub("", ARTICLE_RE.sub("", word.strip()))
    pos = (entry.get("pos") or "").strip().lower()
    eng = (entry.get("definition") or "").split(" - ")[0].strip()

    for kind, rx in FORM_OF:
        m = rx.search(eng)
        if m:
            lemma = m.group(1).strip().rstrip(".:;,\u201d\u2019\u201c")
            return kind, [lemma]

    if BARE_PARTICIPLE.match(eng):
        if lang == "de":
            if bare.casefold() in DE_STRONG_PARTICIPLES:
                return "bare-participle", [DE_STRONG_PARTICIPLES[bare.casefold()]]
            return "bare-participle", de_participle_to_infinitive(bare)
        return "bare-participle", []

    if lang == "en" and pos in VERB_POS and bare.lower().endswith("ing") and len(bare) > 5 \
            and " " not in bare and "-" not in bare:
        return "en-gerund", en_bases(bare.lower())

    return None, []


def build_plan():
    files = sorted(f for f in os.listdir(DATA) if f.startswith("words") and f.endswith(".js"))
    parsed = {}
    index = collections.defaultdict(dict)   # lang -> norm(word) -> [(file, word)]
    for f in files:
        text, entries = parse(os.path.join(DATA, f))
        parsed[f] = (text, entries)
        lang = lang_of(f)
        for e in entries:
            index[lang].setdefault(norm(e["word"]), []).append((f, e["_start"], e["word"]))

    plan = collections.defaultdict(list)
    stats = collections.Counter()
    # lemmas claimed by a rewrite, so two rewrites can't collide
    claimed = collections.defaultdict(set)

    flagged = []
    for f in files:
        lang = lang_of(f)
        text, entries = parsed[f]
        for e in entries:
            if (lang, e["word"].casefold()) in SKIP_WORDS:
                continue
            kind, cands = classify(e, lang)
            if not kind:
                continue
            flagged.append((f, lang, e, kind, cands))

    flagged_ids = {(f, e["_start"]) for f, _, e, _, _ in flagged}

    # 'exists' is evaluated against the ORIGINAL corpus, excluding entries that
    # are themselves scheduled for removal
    for f, lang, e, kind, cands in flagged:
        hit = None
        for c in cands:
            n = norm(c)
            if not n:
                continue
            others = [o for o in index[lang].get(n, [])
                      if (o[0], o[1]) != (f, e["_start"]) and (o[0], o[1]) not in flagged_ids]
            if others:
                hit = others[0][2]
                break
        rec = {
            "file": f, "lang": lang, "kind": kind,
            "word": e["word"], "pos": e.get("pos", ""), "level": e.get("level", ""),
            "category": e.get("category", ""),
            "definition": e.get("definition", ""), "example": e.get("example", ""),
            "start": e["_start"], "end": e["_end"],
            "candidates": cands,
        }
        pos = (e.get("pos") or "").strip().lower()
        # A participial ADJECTIVE is a dictionary headword in its own right
        # (German "ausgezeichnet"/"spannend" == English "amazing"/"interesting").
        # Keep the word, but its "participle of X" definition must be replaced.
        # Likewise a nominalised infinitive that carries a real gloss
        # ("Einkommen - income") is a genuine noun.
        keep = False
        if kind in ("present-participle", "perfect-participle", "inflection", "singular") \
                and pos == "adjective":
            keep = True
        if kind == "gerund" and (has_extra_gloss(e.get("definition", ""), kind)
                                 or norm(e["word"]) in DE_REAL_NOUNS):
            keep = True

        if keep:
            rec["action"] = "redefine"
            rec["lemma"] = cands[0] if cands else None
            stats[(lang, kind, "redefine")] += 1
        elif hit:
            rec["action"] = "delete"
            rec["lemma"] = hit
            stats[(lang, kind, "delete")] += 1
        else:
            lemma = cands[0] if cands else None
            if lemma and norm(lemma) not in claimed[lang]:
                claimed[lang].add(norm(lemma))
                rec["action"] = "rewrite"
                rec["lemma"] = lemma
                stats[(lang, kind, "rewrite")] += 1
            else:
                rec["action"] = "delete-nolemma"
                rec["lemma"] = lemma
                stats[(lang, kind, "delete-nolemma")] += 1
        plan[f].append(rec)

    return plan, stats


def cmd_plan(args):
    plan, stats = build_plan()
    with open(PLAN, "w", encoding="utf-8") as fh:
        json.dump(plan, fh, ensure_ascii=False, indent=1)
    total = sum(len(v) for v in plan.values())
    print(f"plan written: {PLAN}  ({total} entries)")
    for (lang, kind, action), n in sorted(stats.items()):
        print(f"  {lang} {kind:20} {action:15} {n:5}")
    print("TOTAL", total)


def cmd_report(args):
    with open(PLAN, encoding="utf-8") as fh:
        plan = json.load(fh)
    for f in sorted(plan):
        recs = plan[f]
        print(f"\n===== {f}  ({len(recs)})")
        for r in recs:
            print(f"  [{r['action']:14}] {r['word']:26} -> {r.get('lemma')}  ({r['kind']})")


def cmd_apply(args):
    with open(PLAN, encoding="utf-8") as fh:
        plan = json.load(fh)
    for f in sorted(plan):
        path = os.path.join(DATA, f)
        with open(path, encoding="utf-8", newline="") as fh:
            text = fh.read()
        recs = [r for r in plan[f] if r["action"].startswith("delete")]
        if not recs:
            continue
        # verify offsets still match before touching anything
        for r in recs:
            chunk = text[r["start"]:r["end"]]
            if f'word: "{r["word"]}"' not in chunk:
                raise SystemExit(f"OFFSET MISMATCH in {f} for {r['word']!r}")
        out = []
        prev = 0
        for r in sorted(recs, key=lambda x: x["start"]):
            start, end = r["start"], r["end"]
            # swallow the trailing comma and the newline + indentation
            while end < len(text) and text[end] in ",":
                end += 1
            while end < len(text) and text[end] in " \t":
                end += 1
            if text.startswith("\r\n", end):
                end += 2
            elif end < len(text) and text[end] == "\n":
                end += 1
            # and the indentation that preceded the '{'
            while start > 0 and text[start - 1] in " \t":
                start -= 1
            out.append(text[prev:start])
            prev = end
        out.append(text[prev:])
        new = "".join(out)
        with open(path, "w", encoding="utf-8", newline="") as fh:
            fh.write(new)
        print(f"  {f}: removed {len(recs)} entries  ({len(text)} -> {len(new)} bytes)")


def js_escape(s):
    return s.replace("\\", "\\\\").replace('"', '\\"')


def cmd_content(args):
    """Apply the `rewrite` / `redefine` actions using the authored replacements."""
    from _inflected_content import CONTENT

    with open(PLAN, encoding="utf-8") as fh:
        plan = json.load(fh)

    # every rewrite/redefine row must have authored content
    missing = []
    for f in sorted(plan):
        for r in plan[f]:
            if r["action"] in ("rewrite", "redefine"):
                if f'{f}|{r["word"]}' not in CONTENT:
                    missing.append(f'{f}|{r["word"]}')
    if missing:
        raise SystemExit("MISSING CONTENT for %d entries: %s" % (len(missing), missing[:10]))

    for f in sorted(plan):
        path = os.path.join(DATA, f)
        with open(path, encoding="utf-8", newline="") as fh:
            text = fh.read()
        recs = [r for r in plan[f] if r["action"] in ("rewrite", "redefine")]
        if not recs:
            continue

        # verify offsets still match before touching anything
        for r in recs:
            chunk = text[r["start"]:r["end"]]
            if f'word: "{r["word"]}"' not in chunk:
                raise SystemExit(f"OFFSET MISMATCH in {f} for {r['word']!r}")

        out = []
        prev = 0
        for r in sorted(recs, key=lambda x: x["start"]):
            new_word, new_pos, new_def, new_ex = CONTENT[f'{f}|{r["word"]}']
            repl = {
                "word": new_word,
                "pos": new_pos,
                "definition": new_def,
            }
            if new_ex:
                repl["example"] = new_ex
            seen = set()

            def sub(m, repl=repl, seen=seen):
                key = m.group("key")
                if key in repl and key not in seen:
                    seen.add(key)
                    return f'{key}: "{js_escape(repl[key])}"'
                return m.group(0)

            block = FIELD_RE.sub(sub, text[r["start"]:r["end"]])
            if set(repl) - seen:
                raise SystemExit(
                    f"FIELD NOT FOUND in {f} for {r['word']!r}: {sorted(set(repl) - seen)}"
                )
            out.append(text[prev:r["start"]])
            out.append(block)
            prev = r["end"]
        out.append(text[prev:])
        new = "".join(out)
        with open(path, "w", encoding="utf-8", newline="") as fh:
            fh.write(new)
        n_rw = sum(1 for r in recs if r["action"] == "rewrite")
        n_rd = len(recs) - n_rw
        print(f"  {f}: {n_rw} rewritten, {n_rd} redefined  ({len(text)} -> {len(new)} bytes)")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plan", action="store_true")
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--content", action="store_true")
    args = ap.parse_args()
    if args.plan:
        cmd_plan(args)
    elif args.report:
        cmd_report(args)
    elif args.apply:
        cmd_apply(args)
    elif args.content:
        cmd_content(args)
    else:
        ap.print_help()


if __name__ == "__main__":
    main()
