#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Resolve cross-level duplicate headwords (finding #3).

Half the German A2 deck is repeated at another CEFR level; "ich" is taught at
A1 *and* C2.  A learner who picks B1 expects words they have not already been
drilled on at A1, so a duplicate makes the level selector mean nothing.

Rule: within one collection (the six CEFR files of a language), a headword is
kept at its LOWEST level -- that is where a learner first meets it -- and the
higher-level copies are removed.  Before removing a copy, its content is
merged DOWN if it is better than the survivor's: a real example sentence
beats a placeholder, and a real definition beats a stub.  So deduplication
also repairs the survivor rather than just deleting rows.

Duplicates *within* a single file are collapsed the same way.

Collections are language-scoped on purpose: "TOEFL" and "Phrasal Verbs" are
separate study collections the user chooses explicitly, so a word appearing
in both the CEFR list and TOEFL is not a defect.

Usage:
    python scripts/fix_cross_level_dupes.py --plan
    python scripts/fix_cross_level_dupes.py --apply
"""
import argparse
import collections
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _udsp_data as U
import _udsp_quality as Q

COLLECTIONS = {
    "en":   ["wordsa1.js", "wordsa2.js", "wordsb1.js", "wordsb2.js",
             "wordsc1.js", "wordsc2.js"],
    "de":   ["wordsa1gode.js", "wordsa2gode.js", "wordsb1gode.js",
             "wordsb2gode.js", "wordsc1gode.js", "wordsc2gode.js"],
    "fr":   ["wordsa1fr.js", "wordsa2fr.js", "wordsb1fr.js", "wordsb2fr.js",
             "wordsc1fr.js", "wordsc2fr.js"],
    "es":   ["wordsa1es.js", "wordsa2es.js", "wordsb1es.js", "wordsb2es.js",
             "wordsc1es.js", "wordsc2es.js"],
    "it":   ["wordsa1it.js", "wordsa2it.js", "wordsb1it.js", "wordsb2it.js",
             "wordsc1it.js", "wordsc2it.js"],
    "pt":   ["wordsa1pt.js", "wordsa2pt.js", "wordsb1pt.js", "wordsb2pt.js",
             "wordsc1pt.js", "wordsc2pt.js"],
    "toefl":    ["toefl.js"],
    "pv_en":    ["phrasalverbsen.js"],
    "pv_fr":    ["phrasalverbsfr.js"],
    "part_de":  ["partikelverbde.js"],
    "syn_en":   ["synanten.js"],
    "syn_de":   ["synantde.js"],
    "syn_fr":   ["synantfr.js"],
}


def quality(e, name):
    """Higher is better.  Used to pick which duplicate survives content-wise."""
    ex = e.native("example")
    df = e.native("definition")
    score = 0
    if ex and not Q.is_placeholder_example(ex):
        score += 2
        if Q.example_contains_word(U.unescape(e["word"]), ex, U.lang_of(name)):
            score += 1
    if df and not (Q.is_stub_definition(df) or Q.is_no_definition(df)):
        score += 2
    return score


def analyse(files):
    """-> {key: [(file, index, entry)]} for keys appearing more than once."""
    seen = collections.defaultdict(list)
    for name in files:
        lang = U.lang_of(name)
        _, entries = U.load(name)
        for i, e in enumerate(entries):
            seen[U.bare(e["word"], lang)].append((name, i, e))
    return {k: v for k, v in seen.items() if len(v) > 1}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plan", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    grand_del = grand_merge = 0
    for coll, files in COLLECTIONS.items():
        files = [f for f in files if os.path.exists(U.path_of(f))]
        dupes = analyse(files)
        if not dupes:
            continue
        order = {f: i for i, f in enumerate(files)}
        # per-file: entries to delete, and field upgrades to apply
        to_delete = collections.defaultdict(set)
        upgrades = collections.defaultdict(list)   # file -> (index, field, value)
        n_del = n_merge = 0
        for key, hits in dupes.items():
            hits.sort(key=lambda t: (order[t[0]], t[1]))
            keep_file, keep_idx, keep = hits[0]
            best = max(hits, key=lambda t: quality(t[2], t[0]))
            if best[2] is not keep:
                kw = U.unescape(keep["word"])
                klang = U.lang_of(keep_file)
                for field in ("definition", "example"):
                    cur = keep.native(field)
                    cand = best[2].native(field)
                    if field == "example":
                        cur_bad = (not cur or Q.is_placeholder_example(cur)
                                   or not Q.example_contains_word(kw, cur, klang))
                        cand_bad = (not cand or Q.is_placeholder_example(cand)
                                    or not Q.example_contains_word(kw, cand, klang))
                    else:
                        cur_bad = (not cur or Q.is_stub_definition(cur)
                                   or Q.is_no_definition(cur))
                        cand_bad = (not cand or Q.is_stub_definition(cand)
                                    or Q.is_no_definition(cand))
                    if cur_bad and not cand_bad:
                        upgrades[keep_file].append((keep_idx, field, best[2][field]))
                        n_merge += 1
            for name, idx, _e in hits[1:]:
                to_delete[name].add(idx)
                n_del += 1
        grand_del += n_del
        grand_merge += n_merge
        print("%-9s %5d duplicate copies removed, %4d content merges  (%d keys)"
              % (coll, n_del, n_merge, len(dupes)))
        if args.apply:
            for name in files:
                if name not in to_delete and name not in upgrades:
                    continue
                ed = U.Editor(name)
                for idx, field, raw in upgrades.get(name, []):
                    ed.set_field(ed.entries[idx], field, U.unescape(raw))
                for idx in sorted(to_delete.get(name, ())):
                    ed.delete(ed.entries[idx])
                ed.save()
    print("\n%s %d duplicate entries, %d content merges"
          % ("REMOVED" if args.apply else "WOULD REMOVE", grand_del, grand_merge))


if __name__ == "__main__":
    main()
