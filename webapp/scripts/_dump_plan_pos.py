import collections
import json

plan = json.load(open("scripts/_inflected_plan.json", encoding="utf-8"))
rows = [r for v in plan.values() for r in v]

c = collections.Counter()
for r in rows:
    c[(r["lang"], r["kind"], r["pos"], r["action"])] += 1
for k in sorted(c, key=lambda x: (x[0], x[1], x[2], x[3])):
    print(f"  {k[0]}  {k[1]:20} pos={k[2]:10} {k[3]:15} {c[k]:5}")
print("TOTAL", len(rows))

print()
print("### definition has EXTRA gloss beyond the form-of stub?")
extra = collections.Counter()
samples = collections.defaultdict(list)
for r in rows:
    eng = r["definition"].split(" - ")[0].strip()
    # strip the 'X of Y' clause and see what is left
    rest = eng
    for pat in ("gerund of", "present participle of", "perfect participle of",
                "past participle of", "plural of", "singular of", "inflection of"):
        i = rest.lower().find(pat)
        if i >= 0:
            tail = rest[i + len(pat):].lstrip()
            # drop the lemma token
            tail = tail.split(None, 1)
            rest = tail[1] if len(tail) > 1 else ""
            break
    rest = rest.strip(" .;:,\u201c\u201d()")
    has = bool(rest) and len(rest) > 2
    extra[(r["kind"], r["pos"], has)] += 1
    if has and len(samples[(r["kind"], r["pos"])]) < 8:
        samples[(r["kind"], r["pos"])].append(f"{r['word']}={rest[:40]}")
for k in sorted(extra, key=lambda x: (x[0], x[1], str(x[2]))):
    print(f"  {k[0]:20} pos={k[1]:10} extra={str(k[2]):5} {extra[k]:5}")
print()
for k in sorted(samples):
    print(" ", k, samples[k][:6])
