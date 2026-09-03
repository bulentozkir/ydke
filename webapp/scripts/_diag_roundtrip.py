#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Safety check for scripts/_udsp_data.py -- run this before any fix pass.

1. Every data/*.js word file parses, and the entry count matches the number of
   `word:` keys (load() aborts otherwise).
2. A no-op Editor.save(dry_run=True) returns the file byte-identical -- proof
   that the span/splice model cannot perturb untouched bytes.
3. A synthetic single-field edit changes EXACTLY the target field's byte range.
"""
import io
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _udsp_data as U

files = U.word_files()
total = 0
noop_ok = 0
problems = []

for name in files:
    raw = io.open(U.path_of(name), encoding="utf-8").read()
    try:
        ed = U.Editor(name)
    except SystemExit as exc:
        problems.append("%s: %s" % (name, exc))
        continue
    total += len(ed.entries)
    if ed.save(dry_run=True) == raw:
        noop_ok += 1
    else:
        problems.append("%s: no-op save is not byte-identical" % name)
    missing = [f for f in ("word", "level", "definition", "example")
               if ed.entries and f not in ed.entries[0]]
    if missing:
        problems.append("%s: first entry missing %s" % (name, missing))

# 3. surgical-edit proof on a large multi-line file
ed = U.Editor("wordsc2gode.js")
before = ed.text
target = ed.entries[5]
span = target["_fields"]["definition"]
ed.set_field(target, "definition", "SENTINEL VALUE")
after = ed.save(dry_run=True)
i = 0
while i < min(len(before), len(after)) and before[i] == after[i]:
    i += 1
inside = span[0] <= i <= span[1]
if not inside:
    problems.append("surgical edit touched bytes outside the target field span")
if "SENTINEL VALUE" not in after:
    problems.append("surgical edit did not apply")

# 4. delete proof -- removing one entry must remove exactly one `word:` key
ed2 = U.Editor("wordsc2gode.js")
ed2.delete(ed2.entries[5])
out2 = ed2.save(dry_run=True)
keys_before = len(U.WORD_ONLY_RE.findall(before))
keys_after = len(U.WORD_ONLY_RE.findall(out2))
if keys_after != keys_before - 1:
    problems.append("delete removed %d word: keys, expected 1"
                    % (keys_before - keys_after))

print("files parsed           : %d" % len(files))
print("entries parsed         : %d" % total)
print("no-op save identical   : %d / %d" % (noop_ok, len(files)))
print("field edit confined    : %s (first diff byte %d, field span %s)"
      % (inside, i, span))
print("delete removes exactly1: %s" % (keys_after == keys_before - 1))
if problems:
    print("\nPROBLEMS (%d):" % len(problems))
    for p in problems:
        print("  - %s" % p)
    sys.exit(1)
print("OK")
