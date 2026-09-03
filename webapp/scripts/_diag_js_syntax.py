#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""_diag_js_syntax.py

Checks for a missing comma between consecutive JS object entries in
data/*.js files -- the classic `},\n  {` pattern broken into `}\n  {`
(no comma) causes a JS parse error ("Unexpected token '{'"). Also checks
for balanced braces overall as a coarser sanity check.
"""
import os
import re
import sys

DATA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")

FILES = sys.argv[1:] if len(sys.argv) > 1 else [
    "wordsa1.js", "wordsa2.js", "wordsb1.js", "wordsb2.js", "wordsc1.js", "wordsc2.js",
    "wordsa1gode.js", "wordsa2gode.js", "wordsb1gode.js", "wordsb2gode.js", "wordsc1gode.js", "wordsc2gode.js",
    "wordsa1fr.js", "wordsa2fr.js", "wordsb1fr.js", "wordsb2fr.js", "wordsc1fr.js", "wordsc2fr.js",
    "wordsa1it.js", "wordsa2it.js", "wordsb1it.js", "wordsb2it.js", "wordsc1it.js", "wordsc2it.js",
    "wordsa1es.js", "wordsa2es.js", "wordsb1es.js", "wordsb2es.js", "wordsc1es.js", "wordsc2es.js",
    "wordsa1pt.js", "wordsa2pt.js", "wordsb1pt.js", "wordsb2pt.js", "wordsc1pt.js", "wordsc2pt.js",
    "phrasalverbsen.js", "toefl.js", "partikelverbde.js",
]

MISSING_COMMA_RE = re.compile(r'\}\s*\n\s*\{')

for fn in FILES:
    path = os.path.join(DATA, fn)
    if not os.path.exists(path):
        continue
    text = open(path, encoding="utf-8").read()
    n_open = text.count("{")
    n_close = text.count("}")
    missing = list(MISSING_COMMA_RE.finditer(text))
    status = "OK"
    if n_open != n_close:
        status = f"BRACE MISMATCH open={n_open} close={n_close}"
    if missing:
        lines_before = [text[:m.start()].count("\n") + 1 for m in missing]
        print(f"{fn}: {status}, {len(missing)} missing-comma spot(s) at line(s): {lines_before}")
    elif status != "OK":
        print(f"{fn}: {status}")
    else:
        print(f"{fn}: OK ({n_open} braces balanced, no missing-comma pattern)")
