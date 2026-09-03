#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Review helper for _bias.json: term frequency, then full text per term."""
import collections
import json
import sys

rows = json.load(open("scripts/_bias.json", encoding="utf-8"))

if "--term" in sys.argv:
    want = sys.argv[sys.argv.index("--term") + 1].lower()
    seen = set()
    for r in rows:
        if r["hit"].lower() != want:
            continue
        key = r["text"][:80]
        if key in seen:
            continue
        seen.add(key)
        print("[%s] %s (%s)\n    %s\n" % (r["file"], r["word"], r["field"], r["text"][:230]))
    sys.exit()

if "--cat" in sys.argv:
    want = sys.argv[sys.argv.index("--cat") + 1].upper()
    field = sys.argv[sys.argv.index("--field") + 1] if "--field" in sys.argv else None
    skip = sys.argv[sys.argv.index("--skip") + 1].lower().split(",") if "--skip" in sys.argv else []
    seen = set()
    for r in rows:
        if r["cat"] != want:
            continue
        if field and r["field"] != field:
            continue
        if any(s in r["hit"].lower() for s in skip):
            continue
        key = r["text"][:80]
        if key in seen:
            continue
        seen.add(key)
        print("[%s] %s (%s) <%s>\n    %s\n"
              % (r["file"], r["word"], r["field"], r["hit"], r["text"][:230]))
    sys.exit()

by_cat = collections.defaultdict(collections.Counter)
for r in rows:
    by_cat[r["cat"]][r["hit"].lower()] += 1
for cat in ("PROFANITY", "SEXUAL", "RELIGION", "POLITICS"):
    print("== %s" % cat)
    for term, n in by_cat[cat].most_common():
        print("   %4d  %s" % (n, term))
    print("")
