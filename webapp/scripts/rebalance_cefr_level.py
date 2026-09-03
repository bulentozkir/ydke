#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""rebalance_cefr_level.py

Generalized version of rebalance_de_a1.py: scans ONE source CEFR level
file, ranks its words by real frequency, and relocates any word too
advanced for that level UP to a harder level's file (never down -- this
project's established pattern is that an easy word sitting in an advanced
list is much less pedagogically harmful than a hard word sitting in a
beginner list, so only the "too hard for this level" direction is
addressed). De-duplicates against each destination's existing content
using casefolded bare-word (article-stripped) keys.

Two operating modes:
- EXEMPT mode (--exempt-backup PATH): words present in the given pre-merge/
  pre-expansion backup file are trusted as deliberately-curated and are
  NEVER reclassified, regardless of rank (protects genuinely-simple
  thematic vocabulary -- concrete nouns/colors/weekdays -- that a general
  text-frequency corpus under-ranks relative to function words). Only
  words absent from the backup (i.e. bulk-harvested) are reclassified
  using the STRICT bands.
- NO-EXEMPT mode (no --exempt-backup): every word is a candidate, but
  uses much more GENEROUS "only move severe outliers" bands to minimize
  the risk of relocating a legitimately-simple word that just has a
  mediocre corpus rank.

Usage:
  python scripts/rebalance_cefr_level.py --lang de --level A2 \\
      --source wordsa2gode.js --varname WORDS_GODE_A2 \\
      --exempt-backup "%TEMP%\\gode_merge_backup\\wordsa2gode.js" \\
      [--dry-run]
"""
import argparse
import os
import re
import sys

DATA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")
TEMP = os.environ.get("TEMP", ".")

FREQ_FILES = {
    "de": os.path.join(TEMP, "de_full.txt"),
    "fr": os.path.join(TEMP, "fr_full.txt"),
}
ARTICLE_RE = {
    "de": re.compile(r"^(der|die|das)\s+", re.IGNORECASE),
    "fr": re.compile(r"^(le|la|les|l['\u2019])\s*", re.IGNORECASE),
}

CEFR_ORDER = ["A1", "A2", "B1", "B2", "C1", "C2"]

STRICT_BANDS = [("A1", 2500), ("A2", 6000), ("B1", 15000), ("B2", 35000), ("C1", 70000), ("C2", float("inf"))]
GENEROUS_BANDS = [("A1", 20000), ("A2", 40000), ("B1", 70000), ("B2", 120000), ("C1", 250000), ("C2", float("inf"))]

DEST_FILES = {
    "de": {
        "A1": ("wordsa1gode.js", "WORDS_GODE_A1"),
        "A2": ("wordsa2gode.js", "WORDS_GODE_A2"),
        "B1": ("wordsb1gode.js", "WORDS_GODE_B1"),
        "B2": ("wordsb2gode.js", "WORDS_GODE_B2"),
        "C1": ("wordsc1gode.js", "WORDS_GODE_C1"),
        "C2": ("wordsc2gode.js", "WORDS_GODE_C2"),
    },
    "fr": {
        "A1": ("wordsa1fr.js", "WORDS_FR_A1"),
        "A2": ("wordsa2fr.js", "WORDS_FR_A2"),
        "B1": ("wordsb1fr.js", "WORDS_FR_B1"),
        "B2": ("wordsb2fr.js", "WORDS_FR_B2"),
        "C1": ("wordsc1fr.js", "WORDS_FR_C1"),
        "C2": ("wordsc2fr.js", "WORDS_FR_C2"),
    },
}

ENTRY_RE = re.compile(
    r'\{\s*'
    r'word:\s*"((?:\\.|[^"\\])*)",\s*'
    r'pos:\s*"((?:\\.|[^"\\])*)",\s*'
    r'level:\s*"((?:\\.|[^"\\])*)",\s*'
    r'category:\s*"((?:\\.|[^"\\])*)",\s*'
    r'definition:\s*"((?:\\.|[^"\\])*)",\s*'
    r'example:\s*"((?:\\.|[^"\\])*)",?\s*'
    r'(?:synonyms:\s*"((?:\\.|[^"\\])*)",?\s*)?'
    r'(?:antonyms:\s*"((?:\\.|[^"\\])*)",?\s*)?'
    r'\}'
)
# A permissive fallback for finding just word occurrences (used only for a
# sanity total-count cross-check, never for the actual write).
WORD_ONLY_RE = re.compile(r'word:\s*"((?:\\.|[^"\\])*)"')

UNESCAPE_RE = re.compile(r"\\(.)")


def load_rank_map(lang):
    rank = {}
    with open(FREQ_FILES[lang], encoding="utf-8") as f:
        for i, line in enumerate(f):
            parts = line.split()
            if not parts:
                continue
            w = parts[0].strip().casefold()
            if w not in rank:
                rank[w] = i
    return rank


def dedup_key(word_raw, lang):
    w = UNESCAPE_RE.sub(r"\1", word_raw)
    if ARTICLE_RE.get(lang):
        w = ARTICLE_RE[lang].sub("", w)
    return w.casefold()


def parse_entries(path):
    text = open(path, encoding="utf-8").read()
    total_words = len(WORD_ONLY_RE.findall(text))
    out = []
    for m in ENTRY_RE.finditer(text):
        g = m.groups()
        out.append(
            {
                "word": g[0], "pos": g[1], "level": g[2], "category": g[3],
                "definition": g[4], "example": g[5],
                "synonyms": g[6], "antonyms": g[7],
            }
        )
    if len(out) != total_words:
        print(
            "WARNING: %s -- ENTRY_RE matched %d but %d 'word:' occurrences exist "
            "(schema mismatch would silently drop entries!) -- aborting."
            % (path, len(out), total_words)
        )
        sys.exit(1)
    return out


def format_entry(e):
    parts = [
        'word: "%s"' % e["word"],
        'pos: "%s"' % e["pos"],
        'level: "%s"' % e["level"],
        'category: "%s"' % e["category"],
        'definition: "%s"' % e["definition"],
        'example: "%s"' % e["example"],
    ]
    if e.get("synonyms") is not None:
        parts.append('synonyms: "%s"' % e["synonyms"])
    if e.get("antonyms") is not None:
        parts.append('antonyms: "%s"' % e["antonyms"])
    return "  {\n    " + ",\n    ".join(parts) + ",\n  },"


def existing_header(path, varname):
    text = open(path, encoding="utf-8").read()
    marker = "window.%s = [" % varname
    idx = text.find(marker)
    return text[:idx] if idx != -1 else ""


def write_file(path, varname, header, entries):
    content = header + ("window.%s = [\n" % varname) + "\n".join(format_entry(e) for e in entries) + "\n];\n"
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(content)


def target_level(r, bands):
    for level, ceiling in bands:
        if r <= ceiling:
            return level
    return bands[-1][0]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--lang", required=True, choices=["de", "fr"])
    ap.add_argument("--level", required=True, choices=CEFR_ORDER)
    ap.add_argument("--source", required=True)
    ap.add_argument("--varname", required=True)
    ap.add_argument("--exempt-backup", default=None)
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    lang = args.lang
    src_level = args.level
    src_idx = CEFR_ORDER.index(src_level)
    # Only levels STRICTLY HARDER than the source are valid relocation targets.
    harder_levels = CEFR_ORDER[src_idx + 1:]

    bands = STRICT_BANDS if args.exempt_backup else GENEROUS_BANDS
    mode = "EXEMPT+STRICT" if args.exempt_backup else "NO-EXEMPT+GENEROUS"
    print("[mode] %s bands, source level=%s, valid destinations=%s" % (mode, src_level, harder_levels))

    exempt_keys = set()
    if args.exempt_backup:
        backup_path = os.path.expandvars(args.exempt_backup)
        if not os.path.exists(backup_path):
            print("ERROR: exempt backup %s not found -- aborting." % backup_path)
            sys.exit(1)
        backup_words = WORD_ONLY_RE.findall(open(backup_path, encoding="utf-8").read())
        exempt_keys = {dedup_key(w, lang) for w in backup_words}
        print("[exempt] %d words loaded from backup, will never be reclassified" % len(exempt_keys))

    rank_map = load_rank_map(lang)
    src_path = os.path.join(DATA_DIR, args.source)
    src_entries = parse_entries(src_path)
    print("[source] %s: %d entries parsed" % (args.source, len(src_entries)))

    dest_existing = {}
    dest_seen = {}
    dest_paths = {}
    dest_varnames = {}
    for lvl in harder_levels:
        fname, varname = DEST_FILES[lang][lvl]
        path = os.path.join(DATA_DIR, fname)
        entries = parse_entries(path)
        dest_existing[lvl] = entries
        dest_seen[lvl] = {dedup_key(e["word"], lang) for e in entries}
        dest_paths[lvl] = path
        dest_varnames[lvl] = varname

    keep = []
    moved = {lvl: [] for lvl in harder_levels}
    move_counts = {lvl: 0 for lvl in harder_levels}
    dupe_counts = {lvl: 0 for lvl in harder_levels}
    exempted = 0

    for e in src_entries:
        key = dedup_key(e["word"], lang)
        if key in exempt_keys:
            keep.append(e)
            exempted += 1
            continue
        r = rank_map.get(key, float("inf"))
        lvl = target_level(r, bands)
        if lvl not in harder_levels:
            keep.append(e)
            continue
        move_counts[lvl] += 1
        if key in dest_seen[lvl]:
            dupe_counts[lvl] += 1
            continue
        dest_seen[lvl].add(key)
        e2 = dict(e)
        e2["level"] = lvl
        moved[lvl].append(e2)

    print(
        "\n[plan] %s keeps: %d / %d (of which %d exempted, %d common-enough-by-rank)"
        % (src_level, len(keep), len(src_entries), exempted, len(keep) - exempted)
    )
    total_moved = 0
    total_dupe = 0
    for lvl in harder_levels:
        print(
            "[plan] -> %s: %d identified, %d dupe-at-destination (dropped), %d newly appended"
            % (lvl, move_counts[lvl], dupe_counts[lvl], len(moved[lvl]))
        )
        total_moved += len(moved[lvl])
        total_dupe += dupe_counts[lvl]
    print(
        "[summary] kept %d + relocated %d + dropped-dupe %d = %d (should equal %d)"
        % (len(keep), total_moved, total_dupe, len(keep) + total_moved + total_dupe, len(src_entries))
    )

    if args.dry_run:
        print("\n[dry-run] no files written")
        return

    src_header = existing_header(src_path, args.varname) + (
        "// Re-balanced by scripts/rebalance_cefr_level.py (%s mode) -- %d word(s)\n"
        "// too advanced for genuine %s difficulty were relocated to a harder level.\n"
    ) % (mode, total_moved, src_level)
    write_file(src_path, args.varname, src_header, keep)
    print("[write] %s: %d entries" % (args.source, len(keep)))

    for lvl in harder_levels:
        if not moved[lvl]:
            continue
        combined = dest_existing[lvl] + moved[lvl]
        header = existing_header(dest_paths[lvl], dest_varnames[lvl]) + (
            "// +%d word(s) relocated here from %s by scripts/rebalance_cefr_level.py\n"
            "// (too advanced for genuine %s difficulty, reassigned by real frequency rank).\n"
        ) % (len(moved[lvl]), args.source, src_level)
        write_file(dest_paths[lvl], dest_varnames[lvl], header, combined)
        print(
            "[write] %s: %d existing + %d relocated = %d entries"
            % (os.path.basename(dest_paths[lvl]), len(dest_existing[lvl]), len(moved[lvl]), len(combined))
        )


if __name__ == "__main__":
    main()
