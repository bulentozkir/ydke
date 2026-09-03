#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Repair the "long tail" data defects (finding #7 of the content audit).

  1. MISSING_CATEGORY  data/phrasalverbsfr.js -- all 106 entries lack the
     `category` field every other word file has, so the Category selector in
     the header silently drops the whole file.  Classified with the same
     keyword classifier used for the rest of the corpus
     (scripts/classify_word_categories.py), read from the English half of the
     definition.
  2. SLASH_FORM  97 German headwords written as a gendered/variant PAIR
     ("Leser/in", "Bankkaufmann/Bankkauffrau", "der Ellbogen/Ellenbogen").
     A learner is shown "Leser/in" but every example sentence and every game
     (Hangman, Scramble, Dictation, Cloze) treats the slash as a letter.
     Rewritten to the base form ("Leser"), or deleted when the base form is
     already a separate entry in the same file.
  3. JUNK_HEADWORD  "#n/a" and two stray single letters in synanten.js --
     import artefacts, deleted.
  4. MOJIBAKE  wordsc2.js "fa?ade" -> "façade" (cedilla lost in an early
     import).

Usage:  python scripts/fix_longtail.py [--dry-run]
"""
import argparse
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _udsp_data as U
from classify_word_categories import classify

SLASH_RE = re.compile(r"^(?P<base>[^/]+)/(?P<tail>[^/]+)$")
JUNK_RE = re.compile(r"^(?:#n/a|n/a|-+|\d+|[^\W\d_])$", re.I)
# Real words that legitimately contain a slash -- never rewrite these.
SLASH_KEEP = {"GNU/Linux", "km/h"}

MOJIBAKE_FIXES = {"fa?ade": "façade"}


def fix_categories(dry_run, log):
    ed = U.Editor("phrasalverbsfr.js")
    counts = {}
    for e in ed.entries:
        if "category" in e:
            continue
        cat = classify(e.native("definition"))
        ed.insert_field(e, "category", cat, after="level")
        counts[cat] = counts.get(cat, 0) + 1
    ed.save(dry_run=dry_run)
    log.append("phrasalverbsfr.js  +category on %d entries  %s"
               % (ed.changed_fields, sorted(counts.items(), key=lambda t: -t[1])))
    return ed.changed_fields


def fix_slash_forms(dry_run, log):
    renamed = deleted = 0
    for name in ("wordsc2gode.js", "synantde.js"):
        ed = U.Editor(name)
        lang = U.lang_of(name)
        present = set(U.bare(e["word"], lang) for e in ed.entries)
        for e in ed.entries:
            w = U.unescape(e["word"])
            if w in SLASH_KEEP or "/" not in w:
                continue
            m = SLASH_RE.match(w)
            if not m:
                continue
            base = m.group("base").strip()
            if not base or "/" in base:
                continue
            key = U.bare(base, lang)
            # The base form already has its own entry -> this pair is a dupe.
            if key in present and key != U.bare(w, lang):
                ed.delete(e)
                deleted += 1
                log.append("  del  %-34s (base %r already present)" % (w, base))
            else:
                ed.set_field(e, "word", base)
                present.add(key)
                renamed += 1
                log.append("  ren  %-34s -> %s" % (w, base))
        ed.save(dry_run=dry_run)
    log.append("slash forms: %d renamed, %d deleted" % (renamed, deleted))
    return renamed + deleted


def fix_junk(dry_run, log):
    ed = U.Editor("synanten.js")
    for e in list(ed.entries):
        w = U.unescape(e["word"]).strip()
        if not w or JUNK_RE.match(w):
            ed.delete(e)
            log.append("  del junk headword %r" % w)
    ed.save(dry_run=dry_run)
    log.append("junk headwords deleted: %d" % ed.deleted)
    return ed.deleted


def fix_mojibake(dry_run, log):
    n = 0
    for name in U.word_files():
        ed = U.Editor(name)
        for e in ed.entries:
            for field in ("word", "definition", "example"):
                if field not in e:
                    continue
                v = U.unescape(e[field])
                new = v
                for bad, good in MOJIBAKE_FIXES.items():
                    new = new.replace(bad, good)
                if new != v:
                    ed.set_field(e, field, new)
                    log.append("  moji %s %s: %r -> %r" % (name, field, v[:40], new[:40]))
                    n += 1
        if ed.changed_fields:
            ed.save(dry_run=dry_run)
    log.append("mojibake fixed: %d" % n)
    return n


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()
    log = []
    total = 0
    total += fix_categories(args.dry_run, log)
    total += fix_slash_forms(args.dry_run, log)
    total += fix_junk(args.dry_run, log)
    total += fix_mojibake(args.dry_run, log)
    for line in log:
        print(line)
    print("\n%s %d change(s)" % ("WOULD MAKE" if args.dry_run else "MADE", total))


if __name__ == "__main__":
    main()
