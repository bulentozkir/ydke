"""One-off UX audit: payload weight, placeholder/fallback rates, render hazards."""
import os
import re
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")

# ---------- 1. payload weight ----------
sizes = []
for f in sorted(os.listdir(DATA)):
    if f.endswith(".js"):
        sizes.append((os.path.getsize(os.path.join(DATA, f)) / 1024, f))
sizes.sort(reverse=True)
total = sum(s for s, _ in sizes)
print(f"== data/ payload: {len(sizes)} files, {total/1024:.1f} MB total")
for s, f in sizes[:8]:
    print(f"   {s:8.0f} KB  {f}")

# which files does index.html load eagerly?
with open(os.path.join(ROOT, "index.html"), encoding="utf-8") as fh:
    html = fh.read()
srcs = re.findall(r'<script[^>]*src="(data/[^"]+)"', html)
eager = [s for s in srcs]
eager_kb = sum(os.path.getsize(os.path.join(ROOT, s)) / 1024 for s in eager if os.path.exists(os.path.join(ROOT, s)))
print(f"\n== index.html <script src=data/...>: {len(eager)} files, {eager_kb/1024:.2f} MB")
for s in eager:
    p = os.path.join(ROOT, s)
    if os.path.exists(p):
        print(f"   {os.path.getsize(p)/1024:8.0f} KB  {s}")

# ---------- 2. placeholder / low-quality content ----------
PAT = {
    "no-example-fallback": re.compile(r"No example sentence available|Örnek cümle (bulunmuyor|mevcut değil)", re.I),
    "no-definition": re.compile(r"No definition available|Tanım (bulunmuyor|mevcut değil)", re.I),
    "similar-to-stub": re.compile(r"Ähnlich wie:|Similar to:|Şuna benzer:", re.I),
    "see-also-stub": re.compile(r"\bsiehe auch\b|\bsee also\b", re.I),
}
print("\n== placeholder / stub patterns across data/*.js")
grand = Counter()
per_file = {}
for f in sorted(os.listdir(DATA)):
    if not f.endswith(".js"):
        continue
    with open(os.path.join(DATA, f), encoding="utf-8", newline="") as fh:
        t = fh.read()
    row = {k: len(p.findall(t)) for k, p in PAT.items()}
    if any(row.values()):
        per_file[f] = row
    for k, v in row.items():
        grand[k] += v
for k, v in grand.items():
    print(f"   {v:7,}  {k}")
print("   worst files:")
for f, row in sorted(per_file.items(), key=lambda kv: -sum(kv[1].values()))[:8]:
    print(f"      {sum(row.values()):6,}  {f}  {row}")

# ---------- 3. very long definitions (render hazard) ----------
FIELD = re.compile(r'\bdefinition\s*:\s*"((?:[^"\\]|\\.)*)"')
print("\n== definitions longer than 160 chars (overflow / unreadable on phone)")
tot_defs = tot_long = 0
worst = []
for f in sorted(os.listdir(DATA)):
    if not f.endswith(".js"):
        continue
    with open(os.path.join(DATA, f), encoding="utf-8", newline="") as fh:
        t = fh.read()
    defs = FIELD.findall(t)
    tot_defs += len(defs)
    lg = [d for d in defs if len(d) > 160]
    tot_long += len(lg)
    if lg:
        worst.append((len(lg), f, max(lg, key=len)[:120]))
worst.sort(reverse=True)
print(f"   {tot_long:,} / {tot_defs:,} ({100*tot_long/max(tot_defs,1):.1f}%)")
for n, f, sample in worst[:6]:
    print(f"      {n:6,}  {f}   e.g. {sample}...")
