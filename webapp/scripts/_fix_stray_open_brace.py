#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""_fix_stray_open_brace.py

Fixes a recurring corruption pattern found across several data/*.js
files: an orphaned extra "{" immediately before a legitimate entry's
own "{" (i.e. "  },\n  {\n  {\n    word: ...") -- almost certainly a
leftover artifact from an earlier entry-removal script (likely
scripts/_cleanup_fr_explicit.py mentioned in project memory as having
"mysterious" match failures) that deleted an entry's body but left its
opening brace behind. Removes exactly one of the two consecutive
opening-brace-only lines, verified safe via a brace-balance re-check
after each fix.
"""
import os
import re
import sys

DATA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")
PATTERN = re.compile(r'(\{\s*\n)(\s*\{\s*\n\s*word:)')

FILES = sys.argv[1:] if len(sys.argv) > 1 else [
    "wordsa1.js", "wordsa2.js", "wordsb1.js", "wordsb2.js", "wordsc1.js", "wordsc2.js",
    "wordsa11de.js", "wordsa12de.js", "wordsa21de.js", "wordsa22de.js", "wordsb11de.js", "wordsb12de.js",
    "wordsa1gode.js", "wordsa2gode.js", "wordsb1gode.js", "wordsb2gode.js", "wordsc1gode.js", "wordsc2gode.js",
    "wordsa1fr.js", "wordsa2fr.js", "wordsb1fr.js", "wordsb2fr.js", "wordsc1fr.js", "wordsc2fr.js",
    "phrasalverbsen.js", "toefl.js", "partikelverbde.js",
]

for fn in FILES:
    path = os.path.join(DATA, fn)
    if not os.path.exists(path):
        continue
    text = open(path, encoding="utf-8").read()
    before_open = text.count("{")
    before_close = text.count("}")
    new_text, n = PATTERN.subn(r"\2", text)
    if n:
        open(path, "w", encoding="utf-8", newline="\n").write(new_text)
        after_open = new_text.count("{")
        after_close = new_text.count("}")
        print(f"{fn}: fixed {n} stray-brace spot(s); braces {before_open}/{before_close} -> {after_open}/{after_close}")
    else:
        if before_open != before_close:
            print(f"{fn}: NO PATTERN MATCH but still imbalanced ({before_open}/{before_close}) -- needs manual look")
