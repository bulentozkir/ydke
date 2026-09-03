#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""One-time diagnostic: check for duplicate words in given data files."""
import re
import sys

ROOT = r"c:\gitrepo\udsp\data"
FILES = sys.argv[1:] if len(sys.argv) > 1 else [
    'wordsa1.js', 'wordsa2.js', 'wordsb1.js', 'wordsb2.js', 'wordsc1.js', 'wordsc2.js', 'toefl.js',
    'wordsa1gode.js', 'wordsa2gode.js', 'wordsb1gode.js', 'wordsb2gode.js', 'wordsc1gode.js', 'wordsc2gode.js',
    'wordsa1fr.js', 'wordsa2fr.js', 'wordsb1fr.js', 'wordsb2fr.js', 'wordsc1fr.js', 'wordsc2fr.js',
    'wordsa1it.js', 'wordsa2it.js', 'wordsb1it.js', 'wordsb2it.js', 'wordsc1it.js', 'wordsc2it.js',
    'wordsa1es.js', 'wordsa2es.js', 'wordsb1es.js', 'wordsb2es.js', 'wordsc1es.js', 'wordsc2es.js',
    'wordsa1pt.js', 'wordsa2pt.js', 'wordsb1pt.js', 'wordsb2pt.js', 'wordsc1pt.js', 'wordsc2pt.js',
    'phrasalverbsen.js', 'partikelverbde.js',
]
WORD_RE = re.compile(r'word:\s*"((?:\\.|[^"])*)"')

for fn in FILES:
    path = ROOT + "\\" + fn
    text = open(path, encoding="utf-8").read()
    words = WORD_RE.findall(text)
    lw = [w.casefold() for w in words]
    seen = {}
    dupes = {}
    for w in lw:
        seen[w] = seen.get(w, 0) + 1
    dupes = {w: c for w, c in seen.items() if c > 1}
    print(fn, "total:", len(words), "unique:", len(seen), "dupe_words:", len(dupes))
    if dupes:
        sample = list(dupes.items())[:15]
        print("  sample:", sample)
