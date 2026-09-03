#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Locate the "long tail" data defects (finding #7 of the content audit).

  MISSING_CATEGORY  entries with no `category` field at all
  SLASH_FORM        German headwords written as a gendered pair ("Leser/in")
  JUNK_HEADWORD     "#n/a", single letters, digits, empty strings
  MOJIBAKE          UTF-8 read as latin-1 then re-encoded ("faÃ§ade", "fa?ade")

Read-only.  scripts/fix_longtail.py applies the repairs.
"""
import os
import re
import sys
import collections

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _udsp_data as U

SLASH_RE = re.compile(r"^[^/\s]+/[^/\s]+$")
JUNK_RE = re.compile(r"^(?:#n/a|n/a|-+|\d+|[^\W\d_])$", re.I)
# Classic UTF-8-as-latin1 sequences, plus the U+FFFD replacement char and a
# literal "?" standing in for a dropped accent inside a word.
MOJIBAKE_RE = re.compile(r"[ÃÂ][\u0080-\u00bf]|\ufffd|\w\?\w")

found = collections.defaultdict(list)

for name in U.word_files():
    text, entries = U.load(name)
    lang = U.lang_of(name)
    for e in entries:
        w = U.unescape(e.get("word"))
        if "category" not in e:
            found["MISSING_CATEGORY"].append((name, w))
        if lang == "de" and SLASH_RE.match(w):
            found["SLASH_FORM"].append((name, w))
        if JUNK_RE.match(w.strip()) or not w.strip():
            found["JUNK_HEADWORD"].append((name, w))
        for field in ("word", "definition", "example"):
            v = U.unescape(e.get(field) or "")
            if MOJIBAKE_RE.search(v):
                found["MOJIBAKE"].append((name, "%s: %s" % (field, v[:70])))

for rule in ("MISSING_CATEGORY", "SLASH_FORM", "JUNK_HEADWORD", "MOJIBAKE"):
    hits = found[rule]
    print("== %-18s total %d" % (rule, len(hits)))
    per = collections.Counter(n for n, _ in hits)
    for n, c in per.most_common():
        print("   %6d  %s" % (c, n))
    for n, w in hits[:8]:
        print("      . %s: %s" % (n, w))
    print("")
