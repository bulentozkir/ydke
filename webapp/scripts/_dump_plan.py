import collections
import json
import sys

plan = json.load(open("scripts/_inflected_plan.json", encoding="utf-8"))
rows = [r for v in plan.values() for r in v]
want = sys.argv[1:] or ["rewrite", "delete-nolemma"]
for act in want:
    groups = collections.defaultdict(list)
    for r in rows:
        if r["action"] == act:
            groups[r["kind"]].append(r)
    for kind in sorted(groups):
        items = groups[kind]
        print(f"===== {act} / {kind} ({len(items)})")
        for r in items:
            lvl = r["file"][5:-3]
            print(f"  {lvl:8} {r['word']:26} -> {r.get('lemma')}   | {r['definition'][:55]}")
