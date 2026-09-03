"""Coverage + sanity check for scripts/_synant_formof_content.py."""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from _synant_formof_content import CONTENT  # noqa: E402

with open(os.path.join(HERE, "_synant_plan.json"), encoding="utf-8") as fh:
    plan = json.load(fh)

need = [r["word"] for r in plan if r["action"] in ("redefine", "rewrite")]
missing = [w for w in need if w not in CONTENT]
extra = [w for w in CONTENT if w not in need]

bad_semi = []
empty = []
for w, t in CONTENT.items():
    if len(t) != 5:
        bad_semi.append((w, "tuple len != 5"))
        continue
    nw, dn, dt, xn, xt = t
    for label, s in (("def_de", dn), ("def_tr", dt), ("ex_de", xn), ("ex_tr", xt)):
        if ";" in s:
            bad_semi.append((w, f"{label} contains ';'"))
    if not dn.strip() or not dt.strip():
        empty.append(w)
    if bool(xn.strip()) != bool(xt.strip()):
        bad_semi.append((w, "example halves must both be set or both empty"))

print(f"need     : {len(need)}")
print(f"content  : {len(CONTENT)}")
print(f"missing  : {len(missing)} {missing[:25]}")
print(f"extra    : {len(extra)} {extra[:25]}")
print(f"empty    : {len(empty)} {empty[:25]}")
print(f"bad      : {len(bad_semi)} {bad_semi[:25]}")

dupe_targets = {}
for r in plan:
    if r["action"] == "rewrite":
        tgt = CONTENT.get(r["word"], ("",))[0] or r["word"]
        dupe_targets.setdefault(tgt, []).append(r["word"])
print("rewrite targets colliding:", {k: v for k, v in dupe_targets.items() if len(v) > 1})
