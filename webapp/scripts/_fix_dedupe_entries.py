#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""One-time fix: remove duplicate entries (by casefolded word) from data files,
keeping the FIRST occurrence and preserving original order otherwise."""
import re
import sys

ROOT = r"c:\gitrepo\udsp\data"
FILES = sys.argv[1:]
ENTRY_RE = re.compile(r'\{\s*word:\s*"((?:\\.|[^"])*)"[^{}]*\}', re.DOTALL)

for fn in FILES:
    path = ROOT + "\\" + fn
    text = open(path, encoding="utf-8").read()
    matches = list(ENTRY_RE.finditer(text))
    first_brace = matches[0].start()
    close_bracket = text.rindex("]")
    header = text[:first_brace]
    footer = text[close_bracket:]  # starts with "];" -- NOT including trailing comma from last entry

    seen = set()
    kept = []
    removed = 0
    for m in matches:
        w = m.group(1).casefold()
        if w in seen:
            removed += 1
            continue
        seen.add(w)
        kept.append(m.group(0))

    new_body = ",\n  ".join(kept)
    new_text = header + new_body + ",\n" + footer
    open(path, "w", encoding="utf-8", newline="\n").write(new_text)
    print(f"{fn}: total={len(matches)} removed_dupes={removed} kept={len(kept)}")
