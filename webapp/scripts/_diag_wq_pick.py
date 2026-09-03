"""Filter audit samples by rule and file substring."""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
with open(os.path.join(ROOT, "scripts", "_wordquality.json"), encoding="utf-8") as fh:
    d = json.load(fh)

rule = sys.argv[1]
needle = sys.argv[2] if len(sys.argv) > 2 else ""
limit = int(sys.argv[3]) if len(sys.argv) > 3 else 12
hits = [s for s in d["samples"].get(rule, []) if needle in s]
print(f"===== {rule} / '{needle}'  showing {min(limit, len(hits))} of {len(hits)} retained")
for s in hits[:limit]:
    print("  ", s)
