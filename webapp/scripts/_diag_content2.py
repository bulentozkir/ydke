#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Scope the remaining content defects (#1, #4, #5, #6).

  EX_MISSING   example is a placeholder / "no example available" fallback
  EX_NOWORD    example is real but does not contain the headword at all
  EX_RECYCLED  one example sentence serving several unrelated headwords
  DEF_STUB     definition is a synonym list ("Ähnlich wie: ...")
  DEF_COGNATE  definition's native half is just the headword echoed back
  DEF_NONE     honest "no dictionary definition available" fallback
  DEF_POSMISMATCH  definition opens with a grammar gloss for a different POS
"""
import collections
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _udsp_data as U
import _udsp_quality as Q

rules = collections.defaultdict(collections.Counter)
samples = collections.defaultdict(list)
ex_index = collections.defaultdict(list)


def note(rule, name, detail):
    rules[rule][name] += 1
    if len(samples[rule]) < 14:
        samples[rule].append((name, detail))


for name in U.word_files():
    _, entries = U.load(name)
    lang = U.lang_of(name)
    for e in entries:
        w = U.unescape(e["word"])
        ex = e.native("example")
        df = e.native("definition")
        if not ex or Q.is_placeholder_example(ex):
            note("EX_MISSING", name, w)
        else:
            if not Q.example_contains_word(w, ex, lang):
                note("EX_NOWORD", name, "%s :: %s" % (w, ex[:58]))
            ex_index[(name, Q.fold(ex))].append(w)
        if Q.is_stub_definition(df):
            note("DEF_STUB", name, "%s :: %s" % (w, df[:50]))
        elif Q.is_no_definition(df):
            note("DEF_NONE", name, w)
        else:
            core = df.strip().rstrip(".").strip()
            if Q.fold(core) == Q.fold(w) or Q.fold(core) == Q.fold(
                    U.ARTICLE_RE[lang].sub("", w) if lang in U.ARTICLE_RE else w):
                note("DEF_COGNATE", name, "%s -> %r" % (w, df[:40]))

for (name, _), words in ex_index.items():
    if len(words) > 1:
        for w in words:
            note("EX_RECYCLED", name, "%s (x%d)" % (w, len(words)))

for rule in ("EX_MISSING", "EX_NOWORD", "EX_RECYCLED", "DEF_STUB",
             "DEF_COGNATE", "DEF_NONE"):
    c = rules[rule]
    print("== %-12s total %d" % (rule, sum(c.values())))
    for n, k in c.most_common(9):
        print("     %6d  %s" % (k, n))
    for n, d in samples[rule][:6]:
        print("        . %-18s %s" % (n, d))
    print("")
