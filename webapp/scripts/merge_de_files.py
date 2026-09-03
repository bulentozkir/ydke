#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""merge_de_files.py

Merges the telc German sub-level word lists into their corresponding
self-authored "gode" CEFR files, de-duplicating by casefolded bare word
(der/die/das-stripped, same convention as expand_words_de_cefr.py's
existing_words()):

  wordsa11de.js + wordsa12de.js  -> wordsa1gode.js (WORDS_GODE_A1)
  wordsa21de.js + wordsa22de.js  -> wordsa2gode.js (WORDS_GODE_A2)
  wordsb11de.js + wordsb12de.js  -> wordsb1gode.js (WORDS_GODE_B1)

The destination file's EXISTING entries are kept first, byte-for-byte
unchanged (just reformatted through the same 2-space-indent template,
which is already their own format); each source file is then walked in
its existing order, appending any word not already present (by dedup
key). Merged-in entries have their `level` field REWRITTEN from their
old telc sub-level (e.g. "A1.1"/"A1.2") to the destination's plain CEFR
level ("A1"/"A2"/"B1") -- those sub-levels will no longer exist as
selectable levels after this merge (see shared.js LANGS.de.levels), and
`w.level` is shown as a user-facing badge on Flashcards/Review, so leaving
a stale sub-level string would look like broken data.

Does NOT delete the source files -- that's a deliberate separate step
(see the caller / do it manually) so a merge can be inspected before the
originals are removed.

Usage: python scripts/merge_de_files.py
"""
import os
import re

DATA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")

ENTRY_RE = re.compile(
    r'\{\s*'
    r'word:\s*"((?:\\.|[^"\\])*)",\s*'
    r'pos:\s*"((?:\\.|[^"\\])*)",\s*'
    r'level:\s*"((?:\\.|[^"\\])*)",\s*'
    r'category:\s*"((?:\\.|[^"\\])*)",\s*'
    r'definition:\s*"((?:\\.|[^"\\])*)",\s*'
    r'example:\s*"((?:\\.|[^"\\])*)",?\s*'
    r'\}'
)

MERGES = [
    {
        "dest": "wordsa1gode.js",
        "dest_varname": "WORDS_GODE_A1",
        "dest_level": "A1",
        "sources": ["wordsa11de.js", "wordsa12de.js"],
    },
    {
        "dest": "wordsa2gode.js",
        "dest_varname": "WORDS_GODE_A2",
        "dest_level": "A2",
        "sources": ["wordsa21de.js", "wordsa22de.js"],
    },
    {
        "dest": "wordsb1gode.js",
        "dest_varname": "WORDS_GODE_B1",
        "dest_level": "B1",
        "sources": ["wordsb11de.js", "wordsb12de.js"],
    },
]

UNESCAPE_RE = re.compile(r"\\(.)")
ARTICLE_RE = re.compile(r"^(der|die|das)\s+", re.IGNORECASE)


def parse_entries(path):
    text = open(path, encoding="utf-8").read()
    out = []
    for m in ENTRY_RE.finditer(text):
        out.append(
            {
                "word": m.group(1),
                "pos": m.group(2),
                "level": m.group(3),
                "category": m.group(4),
                "definition": m.group(5),
                "example": m.group(6),
            }
        )
    return out


def dedup_key(word_raw):
    w = UNESCAPE_RE.sub(r"\1", word_raw)
    bare = ARTICLE_RE.sub("", w)
    return bare.casefold()


def format_entry(e):
    return (
        "  {\n"
        '    word: "%s",\n'
        '    pos: "%s",\n'
        '    level: "%s",\n'
        '    category: "%s",\n'
        '    definition: "%s",\n'
        '    example: "%s",\n'
        "  },"
    ) % (e["word"], e["pos"], e["level"], e["category"], e["definition"], e["example"])


def main():
    for spec in MERGES:
        dest_path = os.path.join(DATA_DIR, spec["dest"])
        dest_entries = parse_entries(dest_path)
        if not dest_entries:
            print("[%s] 0 entries parsed -- ENTRY_RE may be stale, aborting" % spec["dest"])
            continue
        seen = {dedup_key(e["word"]) for e in dest_entries}

        merged_lines = [format_entry(e) for e in dest_entries]
        added = 0
        skipped = 0

        for src_name in spec["sources"]:
            src_path = os.path.join(DATA_DIR, src_name)
            src_entries = parse_entries(src_path)
            for e in src_entries:
                key = dedup_key(e["word"])
                if key in seen:
                    skipped += 1
                    continue
                seen.add(key)
                e = dict(e)
                e["level"] = spec["dest_level"]
                merged_lines.append(format_entry(e))
                added += 1

        header = (
            "// German %s vocabulary (self-authored CEFR base, merged with the former\n"
            "// telc sub-level word lists %s -- de-duplicated by\n"
            "// casefolded bare word, der/die/das-stripped; see scripts/merge_de_files.py).\n"
            "// Definitions & examples shown as English/German - Turkish.\n"
        ) % (spec["dest_level"], " + ".join(spec["sources"]))
        new_content = header + ("window.%s = [\n" % spec["dest_varname"]) + "\n".join(merged_lines) + "\n];\n"
        with open(dest_path, "w", encoding="utf-8", newline="\n") as f:
            f.write(new_content)
        print(
            "%s: kept %d existing + added %d new (skipped %d dupes) = %d total"
            % (spec["dest"], len(dest_entries), added, skipped, len(dest_entries) + added)
        )


if __name__ == "__main__":
    main()
