#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Triage scripts/_bias.json into definitional (keep) vs gratuitous (fix).

The bias test that actually matters: an EXAMPLE is gratuitous when the entry's
own DEFINITION has nothing to do with the flagged category. A drill for
"plumber" invoking God is bias; a drill for "hellfire" invoking hell is a
definition. So the example is re-tested against the definition with the very
same category regex, and only unrelated hits are surfaced for review.

DEFINITIONS are always surfaced -- there are few of them and a definition that
drags in a political party or a slur is nearly always wrong.

  python scripts/_bias_triage.py            counts only
  python scripts/_bias_triage.py review     dump the rows needing a decision
  python scripts/_bias_triage.py keep       dump the auto-kept rows
"""
import collections
import json
import re
import sys

# reuse the exact category patterns from the scanner without running the scan
src = open("scripts/_diag_bias.py", encoding="utf-8").read()
ns = {}
exec(compile(src.split("CATS = collections.OrderedDict")[0], "_diag_bias", "exec"),
     {"collections": collections, "json": json, "re": re, "sys": sys,
      "__name__": "_patterns"}, ns)
CATS = collections.OrderedDict([
    ("PROFANITY", ns["PROFANITY"]),
    ("SEXUAL", ns["SEXUAL"]),
    ("RELIGION", ns["RELIGION"]),
    ("POLITICS", ns["POLITICS"]),
])
RX = {c: re.compile(r"\b(?:" + "|".join(p) + r")", re.I | re.U) for c, p in CATS.items()}

rows = json.load(open("scripts/_bias.json", encoding="utf-8"))

keep, review = [], []
for r in rows:
    rx = RX[r["cat"]]
    if r["field"] == "example" and rx.search(r.get("dfn", "")):
        keep.append(r)          # definition is on-topic -> the example belongs
    else:
        review.append(r)

mode = sys.argv[1] if len(sys.argv) > 1 else ""
if mode not in ("review", "keep"):
    print("total %d   definitional %d   needs-review %d"
          % (len(rows), len(keep), len(review)))
    for k, v in collections.Counter(r["cat"] for r in review).most_common():
        print("   review %-10s %4d" % (k, v))
    sys.exit(0)

out = review if mode == "review" else keep
out.sort(key=lambda r: (r["cat"], r["file"], r["word"]))
for r in out:
    print("[%s] %s | %s | %s | <%s>\n    EX: %s\n    DF: %s"
          % (r["cat"], r["file"], r["word"], r["field"], r["hit"],
             r["text"][:200], r.get("dfn", "")[:120]))
print("\n-- %d rows" % len(out))
