#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Replace the two boilerplate "definitions" that aren't definitions (#2).

FAMILY A -- data/words*gode.js, 2,659 entries whose whole definition is the
grammar label "weak/mixed all-case plural".  These headwords are DECLINED
adjective forms ("mühsamen" for "mühsam").  Two sub-cases:

  * the lemma is already its own entry in the same German corpus -> the
    declined form is redundant, DELETE it (a vocabulary app should drill
    "mühsam", not one of its four inflected endings);
  * no lemma entry exists -> KEEP the word but give it a real bilingual
    definition (de->en / de->tr), so the card teaches meaning instead of
    reciting a declension table.

FAMILY B -- data/partikelverbde.js, 1,665 entries defined as "German
separable verb from an open-source word list."  That sentence is true of
every entry in the file, so it distinguishes nothing.  Replaced with a real
bilingual definition of the verb.

The plan is recomputed from LIVE file state on every invocation and is never
cached; only the (expensive, rate-limited) translations are cached, in
scripts/.cache/.  Caching a plan across runs is how a re-run silently
re-applies work that was already applied.

Usage:
    python scripts/fix_boilerplate_definitions.py --plan
    python scripts/fix_boilerplate_definitions.py --apply [--limit N]
"""
import argparse
import collections
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _udsp_data as U
import _udsp_translate as T
import _de_lemma as L

GODE_FILES = ["wordsa1gode.js", "wordsa2gode.js", "wordsb1gode.js",
              "wordsb2gode.js", "wordsc1gode.js", "wordsc2gode.js"]


def real_lemma_set():
    """German headwords that already carry a REAL definition."""
    ok = set()
    for name in GODE_FILES:
        _, entries = U.load(name)
        for e in entries:
            d = e.native("definition")
            if L.DECL_DEF_RE.match(d) or not d.strip():
                continue
            ok.add(U.bare(e["word"], "de"))
    return ok


def build_plan():
    """-> (deletes, redefines) recomputed from live files."""
    lemmas = real_lemma_set()
    deletes = collections.defaultdict(list)     # file -> [word]
    redefines = collections.defaultdict(list)   # file -> [word]
    for name in GODE_FILES:
        _, entries = U.load(name)
        for e in entries:
            if not L.DECL_DEF_RE.match(e.native("definition")):
                continue
            w = U.unescape(e["word"])
            hit = next((c for c in L.strip_ending(w)
                        if c.casefold() != w.casefold()
                        and U.bare(c, "de") in lemmas), None)
            if hit:
                deletes[name].append((w, hit))
            else:
                redefines[name].append(w)
    _, pv = U.load("partikelverbde.js")
    for e in pv:
        if L.PARTIKEL_DEF_RE.match(e.native("definition")):
            redefines["partikelverbde.js"].append(U.unescape(e["word"]))
    return deletes, redefines


def report(deletes, redefines):
    nd = sum(len(v) for v in deletes.values())
    nr = sum(len(v) for v in redefines.values())
    print("DELETE  declined form whose lemma is already an entry : %d" % nd)
    for k in sorted(deletes):
        print("   %6d  %s" % (len(deletes[k]), k))
    for k in sorted(deletes):
        for w, lem in deletes[k][:6]:
            print("      . %-22s (lemma %r present)" % (w, lem))
        break
    print("\nREDEFINE  boilerplate -> real bilingual definition   : %d" % nr)
    for k in sorted(redefines):
        print("   %6d  %s" % (len(redefines[k]), k))
    print("\ntranslate calls needed (en+tr, before cache): %d" % (nr * 2))


def apply(deletes, redefines, limit):
    cache = T.Cache("de_defs.json")
    words = sorted({w for v in redefines.values() for w in v})
    if limit:
        words = words[:limit]
    print("translating %d German headword(s)..." % len(words))
    en = T.translate_many(cache, words, "de", "en", progress=True)
    tr = T.translate_many(cache, words, "de", "tr", progress=True)
    cache.save()

    total_del = total_def = skipped = 0
    for name in GODE_FILES + ["partikelverbde.js"]:
        dels = dict(deletes.get(name, []))
        reds = set(redefines.get(name, []))
        if not dels and not reds:
            continue
        ed = U.Editor(name)
        for e in ed.entries:
            w = U.unescape(e["word"])
            if w in dels:
                ed.delete(e)
                continue
            if w not in reds:
                continue
            e_en = (en.get(w) or "").strip()
            e_tr = (tr.get(w) or "").strip()
            # Never write a "definition" that is just the word echoed back --
            # that is what the boilerplate already effectively did.
            if (not e_en or not e_tr
                    or e_en.casefold() == w.casefold()
                    or e_tr.casefold() == w.casefold()):
                skipped += 1
                continue
            # partikelverbde.js glosses verbs as "To leave." -- match it.
            if name == "partikelverbde.js" and not e_en.lower().startswith("to "):
                e_en = "to " + e_en[:1].lower() + e_en[1:]
            ed.set_bilingual(e, "definition",
                             T.ensure_period(T.titlecase_first(e_en)),
                             T.ensure_period(T.titlecase_first(e_tr)))
        ed.save()
        total_del += ed.deleted
        total_def += ed.changed_fields
        print("  %-20s  -%d deleted  ~%d redefined" % (name, ed.deleted, ed.changed_fields))
    print("\ndeleted %d, redefined %d, skipped %d (no usable translation)"
          % (total_del, total_def, skipped))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plan", action="store_true")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--limit", type=int, default=0)
    args = ap.parse_args()
    deletes, redefines = build_plan()
    if args.apply:
        apply(deletes, redefines, args.limit)
    else:
        report(deletes, redefines)


if __name__ == "__main__":
    main()
