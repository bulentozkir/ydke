"""Emit a content template for every rewrite/redefine entry in the plan."""
import json

plan = json.load(open("scripts/_inflected_plan.json", encoding="utf-8"))
rows = [r for v in plan.values() for r in v if r["action"] in ("rewrite", "redefine")]
rows.sort(key=lambda r: (r["file"], r["level"], r["word"]))

seen = {}
for r in rows:
    key = f"{r['file']}|{r['word']}"
    if key in seen:
        print("!! DUPLICATE KEY", key)
    seen[key] = r

print(f"# {len(rows)} entries")
cur = None
for r in rows:
    if r["file"] != cur:
        cur = r["file"]
        print(f"\n# ===== {cur}")
    ex = r["example"].split(" - ")[0]
    tgt = r["lemma"] if r["action"] == "rewrite" else r["word"]
    print(f'    "{r["file"]}|{r["word"]}": ("{tgt}", "{r["pos"]}", "", ""),'
          f'   # {r["action"]} {r["kind"]} :: {ex[:60]}')
