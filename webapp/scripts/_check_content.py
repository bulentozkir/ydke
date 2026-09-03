# -*- coding: utf-8 -*-
"""Check that _inflected_content.CONTENT covers every rewrite/redefine plan row."""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _inflected_content import CONTENT  # noqa: E402

PLAN = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_inflected_plan.json")

with open(PLAN, encoding="utf-8") as fh:
    plan = json.load(fh)

need = []
for fname, rows in plan.items():
    for row in rows:
        if row["action"] in ("rewrite", "redefine"):
            need.append("%s|%s" % (row["file"], row["word"]))

missing = [k for k in need if k not in CONTENT]
extra = [k for k in CONTENT if k not in set(need)]

print("plan rewrite/redefine rows: %d" % len(need))
print("CONTENT keys:               %d" % len(CONTENT))
print("missing:                    %d" % len(missing))
for k in missing:
    print("   MISSING", k)
print("extra:                      %d" % len(extra))
for k in extra:
    print("   EXTRA  ", k)

# sanity: no empty definition, no unescaped issues
bad = [k for k, v in CONTENT.items() if not v[0] or not v[1] or not v[2]]
print("incomplete entries:         %d" % len(bad))
for k in bad:
    print("   INCOMPLETE", k)
