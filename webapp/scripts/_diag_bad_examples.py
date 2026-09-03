#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""_diag_bad_examples.py

Narrower, higher-confidence scan: flags entries where the EXAMPLE sentence
itself contains hard profanity/crude content -- regardless of whether the
headword/definition is perfectly legitimate. This is a DIFFERENT failure
mode than "bad word chosen" -- it's a Tatoeba/bundled-example sentence
that happened to contain objectionable language despite being a genuine
grammatical match for the target word. Fix strategy: replace ONLY the
example with the honest "no example available" fallback, KEEP the good
word+definition (established precedent from the phrasal-verbs pipeline).
"""
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

DATA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")

TARGET_FILES = [
    "wordsa1.js", "wordsa2.js", "wordsb1.js", "wordsb2.js", "wordsc1.js", "wordsc2.js",
    "wordsa1gode.js", "wordsa2gode.js", "wordsb1gode.js", "wordsb2gode.js", "wordsc1gode.js", "wordsc2gode.js",
    "wordsa1fr.js", "wordsa2fr.js", "wordsb1fr.js", "wordsb2fr.js", "wordsc1fr.js", "wordsc2fr.js",
    "phrasalverbsen.js", "toefl.js", "partikelverbde.js",
]

ENTRY_RE = re.compile(r'\{\s*word:\s*"((?:\\.|[^"])*)"\s*,\s*pos:\s*"((?:\\.|[^"])*)"[^{}]*?'
                      r'definition:\s*"((?:\\.|[^"])*)"[^{}]*?example:\s*"((?:\\.|[^"])*)"[^{}]*\}',
                      re.DOTALL)

# HARD profanity/explicit-content only (not soft keywords like "sexual"/
# "breast"/"naked" that have many legitimate uses) -- this list is meant
# to be high-precision, checked against the EXAMPLE field specifically.
HARD_PAT = re.compile(
    r"\bfuck|\bshit\b|\bcunt\b|\bnigger|\bfaggot\b|\bcum\b|\bjizz\b|"
    r"child porn|\bporn\b|pornograph|\bpenis\b|\bvagina\b|\bmasturbat|"
    r"\borgasm|\brape\b|\braping\b|\braped\b|\bmolest|paedophil|pedophil|"
    r"\bincest\b|no penis|no vagina",
    re.IGNORECASE,
)


def main():
    total = 0
    for fn in TARGET_FILES:
        path = os.path.join(DATA, fn)
        if not os.path.exists(path):
            continue
        text = open(path, encoding="utf-8").read()
        flagged = []
        for m in ENTRY_RE.finditer(text):
            word, pos, definition, example = m.groups()
            if HARD_PAT.search(example):
                flagged.append((word, example[:120]))
        if flagged:
            print(f"=== {fn}: {len(flagged)} bad examples ===")
            for w, e in flagged:
                print(f"  {w!r} -> ex: {e}")
            total += len(flagged)
    print(f"\nTOTAL bad examples: {total}")


if __name__ == "__main__":
    main()
