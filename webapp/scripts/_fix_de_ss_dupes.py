#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""_fix_de_ss_dupes.py

Removes redundant ss-spelled duplicate entries from German data files --
Python's str.casefold() maps sz-ligature ("\u00df") to "ss" (correct per
Unicode casefolding rules), which is how these were DISCOVERED (a
casefold-based dupe scan), but the underlying issue is real: the German
frequency-list source (de_full.txt) contains BOTH the standard German
sz-ligature spelling (e.g. "gro\u00df") and a plain "ss" transliteration
(e.g. "gross" -- Swiss-standard or simplified-ASCII rendering) as SEPARATE
frequency entries for the SAME word. expand_words_de_cefr.py's exclusion
check used to compare via .casefold() on one side but .lower() on the
other (a real bug, now fixed to use casefold consistently on both sides)
-- this script cleans up entries already written before that fix.
Keeps the FIRST occurrence (typically the pre-existing/original entry,
almost always the correct \u00df-spelled standard form), removes later
duplicate(s) regardless of which spelling they use.
"""
import os
import re
import sys

DATA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")
ENTRY_RE = re.compile(r'\{\s*word:\s*"((?:\\.|[^"])*)"[^{}]*\}', re.DOTALL)


def strip_gender(w):
    return re.sub(r"^(der|die|das)\s+", "", w, flags=re.IGNORECASE)


def main():
    files = sys.argv[1:]
    for fname in files:
        path = os.path.join(DATA, fname)
        text = open(path, encoding="utf-8").read()
        matches = list(ENTRY_RE.finditer(text))
        seen = set()
        keep_spans = []
        removed = []
        for m in matches:
            key = strip_gender(m.group(1)).casefold()
            if key in seen:
                removed.append(m.group(1))
                continue
            seen.add(key)
            keep_spans.append(m)
        if not removed:
            print(f"{fname}: no ss/sz duplicate pairs found")
            continue
        first_brace = matches[0].start()
        close_bracket = text.rindex("]")
        header = text[:first_brace]
        footer = text[close_bracket:]
        new_body = ",\n  ".join(m.group(0) for m in keep_spans)
        new_text = header + new_body + ",\n" + footer
        open(path, "w", encoding="utf-8", newline="\n").write(new_text)
        print(f"{fname}: removed {len(removed)} duplicate(s): {removed}")


if __name__ == "__main__":
    main()
