#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Analyse the two boilerplate-definition families (finding #2).

  GODE_DECL   data/words*gode.js entries whose definition is the bare grammar
              label "weak/mixed all-case plural".  These headwords are not
              lemmas -- they are DECLINED adjective forms ("mühsamen" for
              "mühsam").  The example sentence shows the base form, so the
              word being drilled never appears in its own example.
  PARTIKEL    data/partikelverbde.js entries defined as "German separable
              verb from an open-source word list."

For GODE_DECL the lemma is recovered from the entry's own example sentence:
the token sharing the longest common prefix with the headword.  That is far
safer than stripping a suffix blindly, because it is verified against real
text that the earlier import already paired with this word.
"""
import collections
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _udsp_data as U
import _de_lemma as L

GODE_FILES = ["wordsa1gode.js", "wordsa2gode.js", "wordsb1gode.js",
              "wordsb2gode.js", "wordsc1gode.js", "wordsc2gode.js"]

stats = collections.Counter()
samples = []
no_lemma = []

corpus = set()
for name in GODE_FILES:
    _, entries = U.load(name)
    for e in entries:
        corpus.add(U.bare(e["word"], "de"))

for name in GODE_FILES:
    _, entries = U.load(name)
    for e in entries:
        if not L.DECL_DEF_RE.match(e.native("definition")):
            continue
        stats["total"] += 1
        w = U.unescape(e["word"])
        lemma = L.lemma_from_example(w, e.native("example"))
        if not lemma:
            stats["no lemma in example"] += 1
            no_lemma.append((name, w, e.native("example")[:60]))
            continue
        if lemma.casefold() == w.casefold():
            stats["headword already the lemma"] += 1
        elif lemma.casefold() in corpus:
            stats["lemma already a separate entry -> delete"] += 1
        else:
            stats["rename to lemma"] += 1
            if len(samples) < 20:
                samples.append((w, lemma, e.native("example")[:55]))

print("== GODE_DECL")
for k, v in stats.most_common():
    print("   %6d  %s" % (v, k))
print("\n   rename samples:")
for w, lem, ex in samples:
    print("     %-24s -> %-20s %s" % (w, lem, ex))
print("\n   no-lemma samples:")
for n, w, ex in no_lemma[:12]:
    print("     %-14s %-22s %s" % (n, w, ex))

_, pv = U.load("partikelverbde.js")
n = sum(1 for e in pv if L.PARTIKEL_DEF_RE.match(e.native("definition")))
print("\n== PARTIKEL  %d of %d entries carry the boilerplate definition" % (n, len(pv)))
for e in pv[:0]:
    pass
shown = 0
for e in pv:
    if L.PARTIKEL_DEF_RE.match(e.native("definition")) and shown < 8:
        print("     %-22s ex: %s" % (U.unescape(e["word"]), e.native("example")[:60]))
        shown += 1
