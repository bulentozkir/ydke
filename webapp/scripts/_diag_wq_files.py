"""Per-file breakdown of the top rules from the JS audit JSON."""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
with open(os.path.join(ROOT, "scripts", "_wordquality.json"), encoding="utf-8") as fh:
    d = json.load(fh)

meta = {f: v for f, v in d["perFile"].items()}
for r in sys.argv[1:]:
    bf = d["byRuleFile"].get(r, {})
    print(f"\n===== {r}   total {d['byRule'].get(r,0):,}")
    for f, n in sorted(bf.items(), key=lambda kv: -kv[1]):
        if not n:
            continue
        m = meta.get(f, {})
        tot = m.get("entries", 0) or 1
        print(f"   {n:6,} / {tot:6,} ({n*100//tot:3d}%)  {m.get('lang','')} {m.get('level','')}  {f}")
