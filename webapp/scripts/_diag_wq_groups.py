"""Top shared definition/example groups across the whole corpus."""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
with open(os.path.join(ROOT, "scripts", "_wordquality.json"), encoding="utf-8") as fh:
    d = json.load(fh)

for rule in ("D_SHARED", "E_SHARED"):
    rows = []
    for s in d["samples"].get(rule, []):
        f, rest = s.split(": x", 1)
        n, txt = rest.split(" ", 1)
        rows.append((int(n), f, txt.split("] -> ", 1)[-1]))
    rows.sort(reverse=True)
    print(f"\n===== {rule}: largest groups")
    for n, f, txt in rows[:20]:
        print(f"   x{n:<5} {f:<22} {txt[:80]}")
