"""Scan German words*gode.js for grammar-label definitions (inflected forms
that were imported as if they were headwords)."""
import os
import re
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")

# A grammar label, not a meaning: no verb, just case/number/degree/person terms.
LABEL = re.compile(
    r"^(weak|strong|mixed|weak/mixed|strong/mixed)?[\w/ ]*\b("
    r"all-case|nominative|accusative|dative|genitive|singular|plural|"
    r"comparative|superlative|past tense|present tense|subjunctive|imperative|"
    r"participle|infinitive|declension|feminine|masculine|neuter|"
    r"first-person|second-person|third-person)\b[\w/ ,-]*$",
    re.I,
)

files = sorted(f for f in os.listdir(DATA) if re.match(r"words[abc][12]gode\.js$", f))
files += ["partikelverbde.js", "synantde.js"]

grand = 0
for fn in files:
    path = os.path.join(DATA, fn)
    txt = open(path, encoding="utf-8").read()
    defs = re.findall(r'definition:\s*"((?:[^"\\]|\\.)*)"', txt)
    if not defs:  # compact schema
        defs = re.findall(r'definition:"((?:[^"\\]|\\.)*)"', txt)
    sep = ";" if fn.startswith("synant") else " - "
    hits = Counter()
    for d in defs:
        native = d.split(sep)[0].strip()
        if LABEL.match(native):
            hits[native] += 1
    n = sum(hits.values())
    grand += n
    print(f"{n:6,} / {len(defs):6,} ({n * 100 // max(len(defs), 1):3d}%)  {fn}")
    for lbl, c in hits.most_common(5):
        print(f"          x{c:<6} {lbl[:70]}")
print(f"\nTOTAL grammar-label definitions: {grand:,}")
