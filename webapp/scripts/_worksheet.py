import json
import sys

plan = json.load(open("scripts/_inflected_plan.json", encoding="utf-8"))
rows = [r for v in plan.values() for r in v]
action = sys.argv[1]
start = int(sys.argv[2]) if len(sys.argv) > 2 else 0
count = int(sys.argv[3]) if len(sys.argv) > 3 else 10**6

sel = [r for r in rows if r["action"] == action]
sel.sort(key=lambda r: (r["file"], r["word"]))
print(f"# {action}: {len(sel)} total, showing {start}..{start + count}")
for r in sel[start:start + count]:
    ex = r["example"].split(" - ")[0]
    print(f"{r['word']}\t{r['pos']}\t{r['level']}\t{r.get('lemma')}\t{ex[:95]}")
