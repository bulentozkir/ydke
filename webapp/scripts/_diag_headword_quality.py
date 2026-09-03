#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Headword-quality audit -- the dimensions _diag_wordquality.js does not cover.

That tool checks whether a headword's definition and example are well formed.
This one asks a different question: is the headword itself a word worth
teaching, and is it in the right deck?

  PROPER_NOUN   headword is a person/place name (its own definition says so)
  INFLECTED_DUP headword is a regular inflection of another headword in the
                same file, so the deck teaches one lexeme several times
  VARIANT_DUP   2-4 headwords in a file share one definition verbatim
                (spelling variants padding the count; 5+ is D_SHARED)
  WRONG_LANG_EX example sentence is not in the file's language
  LONG_AT_A1    A1/A2 headword over 15 characters (level misassignment)

Usage:  python scripts/_diag_headword_quality.py [--samples N]
"""
import collections
import re
import sys

import _udsp_data as U
import _udsp_quality as Q

SAMPLES = 8
if "--samples" in sys.argv:
    SAMPLES = int(sys.argv[sys.argv.index("--samples") + 1])

LANG_OF_FILE = {}
for _n in U.word_files():
    LANG_OF_FILE[_n] = U.lang_of(_n)

# Wiktionary and WordNet name a person or place in a few fixed ways, and that
# phrasing beats capitalisation as a signal (the import lowercased headwords).
# Anchored at the start: "A state ... of" mid-definition is not a place name,
# which an unanchored pattern happily claimed on the first run.
PROPER = re.compile(
    r"^(A|An|The)\s+"
    r"((sur|fore|nick|place|male given|female given|unisex given|given|"
    r"habitational|patronymic|diminutive)\s*(sur)?name\b"
    r"|(city|town|village|hamlet|borough|county|river|mountain|lake|island|"
    r"commune|municipality|parish|township|census-designated place|"
    r"unincorporated community|CDP)\b[^.]{0,50}\bin\b"
    r"|capital (city )?of\b)"
    r"|^Surname\b"
    r"|^A (male|female) given name\b",
    re.I)

# One suffix rule per shape, applied to the FILE's own headword set only.
EN_SUFFIX = [("s", ""), ("es", ""), ("ed", ""), ("d", ""), ("ing", ""),
             ("ies", "y"), ("ied", "y"), ("ses", "s")]
FR_SUFFIX = [("s", ""), ("x", ""), ("es", "")]
# German gets only adjective/participle declension endings. Verb-vs-noun pairs
# like Boot/booten and Ablauf/ablaufen are separate lexemes, not inflections,
# so the pos guard below has to agree as well.
DE_SUFFIX = [("e", ""), ("en", ""), ("er", ""), ("es", ""), ("em", "")]
SUFFIX = {"en": EN_SUFFIX, "fr": FR_SUFFIX, "de": DE_SUFFIX,
          "es": FR_SUFFIX, "it": FR_SUFFIX, "pt": FR_SUFFIX}

_STOP_RAW = {
    "en": {"the", "of", "and", "to", "in", "is", "that", "it", "for",
           "was", "with", "he", "she", "you", "they", "his", "her", "on",
           "as", "at", "by", "this", "have", "had", "not", "are", "but"},
    "de": {"der", "die", "das", "und", "ist", "nicht", "ein", "eine", "mit",
           "ich", "du", "er", "sie", "es", "wir", "ihr", "den", "dem", "des",
           "zu", "auf", "für", "von", "war", "hat", "sich", "im", "aber"},
    "fr": {"le", "la", "les", "de", "des", "du", "un", "une", "est", "et",
           "dans", "pour", "qui", "que", "il", "elle", "nous", "vous", "ils",
           "ne", "pas", "sur", "au", "aux", "ce", "cette", "son", "sa"},
    "es": {"el", "la", "los", "las", "de", "del", "un", "una", "es", "y",
           "en", "para", "que", "con", "no", "se", "por", "su", "al", "lo"},
    "it": {"il", "lo", "la", "i", "gli", "le", "di", "del", "un", "una",
           "e", "in", "per", "che", "con", "non", "si", "da", "al", "sul"},
    "pt": {"o", "a", "os", "as", "de", "do", "da", "um", "uma", "e", "em",
           "para", "que", "com", "não", "se", "por", "no", "na", "ao"},
}
# A token shared by two languages proves nothing: English "I" is the Italian
# plural article and "in" is German, which made the first run call plain
# English sentences Italian. Keep only tokens unique to one language.
_seen = collections.Counter(t for s in _STOP_RAW.values() for t in s)
STOP = {l: {t for t in s if _seen[t] == 1 and len(t) > 1}
        for l, s in _STOP_RAW.items()}

hits = collections.defaultdict(list)
per_file = collections.Counter()
totals = collections.Counter()


def flag(rule, name, msg):
    totals[rule] += 1
    per_file[(rule, name)] += 1
    if len(hits[(rule, name)]) < SAMPLES:
        hits[(rule, name)].append(msg)


for name in U.word_files():
    text, entries = U.load(name)
    lang = LANG_OF_FILE[name]
    sep = U.sep_for(name)
    # words<level><lang>.js encodes the deck's level in its own filename, so
    # any entry carrying a different level is shelved in the wrong deck.
    m = re.match(r"words([abc][12])", name)
    file_level = m.group(1).upper() if m else None
    heads = {}
    pos_of = {}
    defs = collections.defaultdict(list)

    for e in entries:
        w = U.unescape(e.get("word", "")).strip()
        if w:
            heads.setdefault(Q.fold(w), w)
            pos_of.setdefault(Q.fold(w), U.unescape(e.get("pos", "")).strip())

    for e in entries:
        w = U.unescape(e.get("word", "")).strip()
        d = U.unescape(e.get("definition", ""))
        x = U.unescape(e.get("example", ""))
        dn = d.split(sep)[0].strip()
        xn = x.split(sep)[0].strip()
        lvl = U.unescape(e.get("level", "")) or "?"

        if dn and PROPER.search(dn):
            flag("PROPER_NOUN", name, '%s [%s] -> %s' % (w, lvl, dn[:70]))

        f = Q.fold(w)
        mypos = U.unescape(e.get("pos", "")).strip()
        # The synant schema has no pos field, so the guard below cannot tell
        # Boot/booten (noun vs verb) from einzeln/einzelnen (real declension).
        # Without it the German rule is guesswork, so skip those files.
        if not (lang == "de" and U.is_flat(name)):
            for suf, repl in SUFFIX.get(lang, []):
                if len(f) > len(suf) + 2 and f.endswith(suf):
                    base = f[: len(f) - len(suf)] + repl
                    if base == f or base not in heads:
                        continue
                    if mypos and pos_of.get(base) and mypos != pos_of[base]:
                        continue
                    flag("INFLECTED_DUP", name,
                         '%s  <- inflection of "%s"' % (w, heads[base]))
                    break

        if dn and not Q.is_no_definition(dn):
            defs[dn].append(w)

        if xn and not Q.is_placeholder_example(xn):
            toks = [t.lower() for t in Q.TOKEN_RE.findall(xn)]
            if len(toks) >= 5:
                own = sum(t in STOP[lang] for t in toks)
                best, bl = own, lang
                for l2, s2 in STOP.items():
                    if l2 == lang:
                        continue
                    n2 = sum(t in s2 for t in toks)
                    if n2 > best:
                        best, bl = n2, l2
                # A 2-token margin still caught French sentences that merely
                # share a couple of tokens with Spanish. Require the foreign
                # language to win outright and by a clear margin.
                if bl != lang and best >= 3 and best >= own + 3:
                    flag("WRONG_LANG_EX", name,
                         '%s [%s] looks %s -> %s' % (w, lang, bl, xn[:70]))

        if lvl in ("A1", "A2") and len(w) > 15 and " " not in w:
            flag("LONG_AT_A1", name, '%s [%s] (%d chars)' % (w, lvl, len(w)))

        if file_level and lvl != file_level:
            flag("LEVEL_MISMATCH", name,
                 '%s is level "%s" inside the %s deck' % (w, lvl, file_level))

    for d, ws in defs.items():
        if 2 <= len(ws) <= 4:
            for _ in ws[1:]:
                totals["VARIANT_DUP"] += 1
                per_file[("VARIANT_DUP", name)] += 1
            if len(hits[("VARIANT_DUP", name)]) < SAMPLES:
                hits[("VARIANT_DUP", name)].append(
                    "[%s] -> %s" % (", ".join(ws), d[:60]))

print("")
for rule in ("PROPER_NOUN", "INFLECTED_DUP", "VARIANT_DUP", "WRONG_LANG_EX",
             "LONG_AT_A1", "LEVEL_MISMATCH"):
    print("== %-14s %6d" % (rule, totals[rule]))
    worst = sorted([(v, k[1]) for k, v in per_file.items() if k[0] == rule],
                   reverse=True)[:5]
    for n, f in worst:
        print("      %6d  %s" % (n, f))
        for s in hits[(rule, f)][:3]:
            print("             . %s" % s)
    print("")
