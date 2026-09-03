"""Print sample hits per rule from the JS audit's JSON output."""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
with open(os.path.join(ROOT, "scripts", "_wordquality.json"), encoding="utf-8") as fh:
    d = json.load(fh)

rules = sys.argv[1:] or sorted(d["samples"], key=lambda r: -d["byRule"].get(r, 0))
for r in rules:
    print(f"\n===== {r}   total {d['byRule'].get(r, 0):,}")
    for s in d["samples"].get(r, [])[:10]:
        print(f"   {s}")
