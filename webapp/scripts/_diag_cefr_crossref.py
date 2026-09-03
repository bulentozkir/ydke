#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""_diag_cefr_crossref.py -- cross-reference our English CEFR word levels
against the open CEFR-J vocabulary profile (authoritative A1-C2 grading).
Measures agreement / mismatch volume before any re-leveling.
"""
import os, re, sys, csv, io, urllib.request, collections
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")
TEMP = os.environ.get("TEMP", ".")
CSV_URL = "https://raw.githubusercontent.com/openlanguageprofiles/olp-en-cefrj/master/cefrj-vocabulary-profile-1.5.csv"
CSV_PATH = os.path.join(TEMP, "cefrj-1.5.csv")
ORDER = {"A1": 1, "A2": 2, "B1": 3, "B2": 4, "C1": 5, "C2": 6}
FILE_LEVEL = {"wordsa1.js": "A1", "wordsa2.js": "A2", "wordsb1.js": "B1",
              "wordsb2.js": "B2", "wordsc1.js": "C1", "wordsc2.js": "C2"}

if not os.path.exists(CSV_PATH):
    urllib.request.urlretrieve(CSV_URL, CSV_PATH)

cefrj = {}  # headword_lower -> min CEFR order
with open(CSV_PATH, encoding="utf-8") as f:
    for row in csv.DictReader(f):
        hw = (row.get("headword") or "").strip().lower()
        lv = (row.get("CEFR") or "").strip().upper()
        if not hw or lv not in ORDER:
            continue
        for part in re.split(r"[/,]", hw):
            p = part.strip()
            if p:
                cefrj[p] = min(cefrj.get(p, 9), ORDER[lv])
print(f"CEFR-J: {len(cefrj)} headword forms loaded")

WORDF = re.compile(r'word:\s*"((?:\\.|[^"\\])*)"')
inv = {v: k for k, v in ORDER.items()}
total = covered = same = 0
move = collections.Counter()  # (from,to) -> n
examples = collections.defaultdict(list)
for fn, flvl in FILE_LEVEL.items():
    t = open(os.path.join(DATA, fn), encoding="utf-8").read()
    for m in WORDF.finditer(t):
        w = m.group(1).strip().lower()
        total += 1
        tgt = cefrj.get(w)
        if not tgt:
            continue
        covered += 1
        cur = ORDER[flvl]
        if tgt == cur:
            same += 1
        else:
            key = (flvl, inv[tgt])
            move[key] += 1
            if len(examples[key]) < 4:
                examples[key].append(w)
print(f"English words: {total} total, {covered} covered by CEFR-J, {same} same level, {covered-same} differ")
print("\n=== mismatches (current_file_level -> cefrj_level): count ===")
for (frm, to), n in sorted(move.items(), key=lambda kv: -kv[1]):
    d = ORDER[to] - ORDER[frm]
    print(f"  {frm} -> {to}  ({'+' if d>0 else ''}{d}): {n:5}   e.g. {', '.join(examples[(frm,to)])}")
