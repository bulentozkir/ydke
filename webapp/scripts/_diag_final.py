#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""One report covering every finding this remediation set out to fix.

Run after any data change:  python scripts/_diag_final.py
"""
import collections
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _udsp_data as U
import _udsp_quality as Q

SLASH_OK = {"GNU/Linux", "km/h"}
LEVEL_SUFFIX = {"": "en", "gode": "de", "fr": "fr", "es": "es",
                "it": "it", "pt": "pt"}


def level_family(name):
    """Which CEFR-graded deck a file belongs to, or None.

    Only these files promise "this word belongs to this level"; toefl.js and
    the synonym decks are separate study lists, so sharing a word with them is
    not the defect finding #3 describes.
    """
    if not name.startswith("words") or len(name) < 12:
        return None
    rest = name[len("words"):-3]              # e.g. "b1gode"
    return LEVEL_SUFFIX.get(rest[2:])


def main():
    files = U.word_files()
    totals = collections.Counter()
    per_file = collections.defaultdict(collections.Counter)
    seen_by_lang = collections.defaultdict(dict)   # lang -> bare word -> file

    for name in files:
        _, entries = U.load(name)
        lang = U.lang_of(name)
        totals["entries"] += len(entries)
        by_example = collections.defaultdict(int)

        for e in entries:
            w = U.unescape(e["word"])
            ex = e.native("example")
            d = e.native("definition")

            if not ex or Q.is_placeholder_example(ex):
                per_file[name]["ex_missing"] += 1
            else:
                by_example[Q.fold(ex)] += 1
                if not Q.example_contains_word(w, ex, lang):
                    per_file[name]["ex_noword"] += 1
            if not d or Q.is_no_definition(d):
                per_file[name]["def_none"] += 1
            elif Q.is_stub_definition(d):
                per_file[name]["def_stub"] += 1
            if not e.get("category"):
                per_file[name]["no_category"] += 1
            if "/" in w and w not in SLASH_OK:
                per_file[name]["slash_form"] += 1
            if "\ufffd" in w or "\ufffd" in (ex or "") or "\ufffd" in (d or ""):
                per_file[name]["mojibake"] += 1

            key = U.bare(w, lang)
            fam = level_family(name)
            if key and fam:
                seen_by_lang[fam].setdefault(key, []).append(name)

        per_file[name]["ex_recycled"] = sum(n - 1 for n in by_example.values() if n > 1)

    for name, c in per_file.items():
        for k, v in c.items():
            totals[k] += v

    dupes = 0
    for lang, words in seen_by_lang.items():
        for _w, names in words.items():
            if len(set(names)) > 1:
                dupes += len(names) - 1
    totals["cross_level_dupes"] = dupes

    order = ["entries", "ex_missing", "ex_noword", "ex_recycled", "def_none",
             "def_stub", "cross_level_dupes", "no_category", "slash_form",
             "mojibake"]
    print("files %d" % len(files))
    for k in order:
        print("  %-20s %7d" % (k, totals[k]))

    print("\nworst files")
    rank = sorted(per_file.items(),
                  key=lambda kv: -(kv[1]["ex_missing"] + kv[1]["ex_noword"]
                                   + kv[1]["ex_recycled"] + kv[1]["def_stub"]))
    for name, c in rank[:12]:
        bits = " ".join("%s=%d" % (k, v) for k, v in sorted(c.items()) if v)
        if bits:
            print("  %-20s %s" % (name, bits))


if __name__ == "__main__":
    main()
