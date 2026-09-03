#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""add_category_field.py

Adds a new `category` field to every word entry in the data files that make
up the shared CEFR word-list system (each file gets ONE fixed category
value, inserted right after that entry's existing `level:` line):

    Vocabulary      -- the 24 plain CEFR word lists (EN/DE-telc/DE-gode/FR)
    Phrasal Verbs   -- data/phrasalverbsen.js
    TOEFL           -- data/toefl.js
    Partikelverben  -- data/partikelverbde.js

This powers a new "Category" selector in the header (see shared.js's
`levelCategory`/`categories` config + renderCategoryButtons()/setCategory())
that lets users filter which collection they're studying, independent of
CEFR level.

NOT touched: data/synant*.js (Word Morph's synonym/antonym data) and the
data/readingcomp*.js reading-passage files -- neither uses this
word/pos/level/definition/example schema, and both are out of scope for
this feature (see repo memory for the full rationale).

Usage: python scripts/add_category_field.py [--dry-run]
"""
import os
import re
import sys

DATA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")

VOCAB_FILES = [
    "wordsa1.js", "wordsa2.js", "wordsb1.js", "wordsb2.js", "wordsc1.js", "wordsc2.js",
    "wordsa11de.js", "wordsa12de.js", "wordsa21de.js", "wordsa22de.js", "wordsb11de.js", "wordsb12de.js",
    "wordsa1gode.js", "wordsa2gode.js", "wordsb1gode.js", "wordsb2gode.js", "wordsc1gode.js", "wordsc2gode.js",
    "wordsa1fr.js", "wordsa2fr.js", "wordsb1fr.js", "wordsb2fr.js", "wordsc1fr.js", "wordsc2fr.js",
]
FILE_CATEGORY = {fname: "Vocabulary" for fname in VOCAB_FILES}
FILE_CATEGORY["phrasalverbsen.js"] = "Phrasal Verbs"
FILE_CATEGORY["toefl.js"] = "TOEFL"
FILE_CATEGORY["partikelverbde.js"] = "Partikelverben"

# Matches a `level: "...",` line (any indent), capturing the indent so the
# inserted `category:` line matches it exactly.
LEVEL_LINE_RE = re.compile(r'^([ \t]*)level:\s*"[^"]*",\s*$', re.MULTILINE)


def add_category(path, category):
    with open(path, encoding="utf-8") as f:
        content = f.read()

    if re.search(r'^\s*category:\s*"', content, re.MULTILINE):
        print(f"[skip] {os.path.basename(path)} already has a category field")
        return 0

    def repl(m):
        indent = m.group(1)
        return m.group(0) + "\n" + indent + 'category: "' + category + '",'

    new_content, n = LEVEL_LINE_RE.subn(repl, content)
    with open(path, "w", encoding="utf-8") as f:
        f.write(new_content)
    return n


def main():
    dry_run = "--dry-run" in sys.argv
    total = 0
    for fname, category in FILE_CATEGORY.items():
        path = os.path.join(DATA_DIR, fname)
        if not os.path.exists(path):
            print(f"[missing] {fname} not found, skipping")
            continue
        if dry_run:
            with open(path, encoding="utf-8") as f:
                content = f.read()
            n = len(LEVEL_LINE_RE.findall(content))
            print(f"[dry-run] {fname}: would add category=\"{category}\" to {n} entries")
            total += n
        else:
            n = add_category(path, category)
            print(f"[write] {fname}: added category=\"{category}\" to {n} entries")
            total += n
    print(f"\nTOTAL entries {'would be ' if dry_run else ''}updated: {total}")


if __name__ == "__main__":
    main()
