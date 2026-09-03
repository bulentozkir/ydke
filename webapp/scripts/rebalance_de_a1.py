#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""rebalance_de_a1.py

Re-classifies the BULK-HARVESTED portion of data/wordsa1gode.js (German
"A1") by REAL frequency rank (hermitdave/FrequencyWords de_full.txt, the
same source already used elsewhere in this project) and relocates any
word too advanced for genuine A1 difficulty to its correct CEFR gode file
(A2/B1/B2/C1/C2), de-duplicating against each destination's existing
content. Root cause of the original problem: expand_words_de_cefr.py's
TARGETS list has NO per-level frequency-rank ceiling (unlike the English
equivalent script) -- it just sequentially fills each bucket's raw word-
count target from one shared frequency-ranked candidate stream, so filling
A1.1's target of 9575 + A1.2's target of 9042 words (since merged into
this file) required walking deep into the frequency tail, since only
~15-20% of raw candidates pass the accept filter. A genuine "beginner"
vocabulary is nowhere near 18,000+ words.

**Critical scoping decision, confirmed empirically before running for
real**: pure frequency rank is a BAD fit for the file's ORIGINAL 993
self-authored words (predating the telc merge) -- common A1-taught
CONCRETE/THEMATIC vocabulary (colors, food, weekdays, clothing: "der
Apfel", "blau", "der Dienstag", "der Schuh"...) frequently has a mediocre
rank in a general text-frequency corpus (corpora skew toward function
words/abstract verbs), and multi-word greetings ("Guten Morgen") aren't in
a single-word frequency list at all (rank=infinity) -- naively reclass-
ifying those by rank would have WRONGLY demoted well-curated, genuinely-
easy words to B2/C1/C2. **This script therefore EXEMPTS the original
993 self-authored words entirely** (identified via a pre-merge backup of
wordsa1gode.js from earlier this session, %TEMP%\\gode_merge_backup\\
wordsa1gode.js -- ONE-TIME/session-scoped, this script cannot be usefully
re-run in a later session once that backup is gone) and ONLY reclassifies
the ~11,279 bulk-harvested words that came from the telc-sourced
expand_words_de_cefr.py fetch (which had no ceiling to begin with, so
re-sorting THEM by frequency is at least internally consistent with how
they were harvested in the first place).

Level bands (by frequency rank; word absent from the frequency list is
treated as maximally rare, i.e. rank=infinity) -- applied ONLY to the
harvested subset:
  A1: rank <= 2500       (true beginner core)
  A2: 2500 < rank <= 6000
  B1: 6000 < rank <= 15000
  B2: 15000 < rank <= 35000
  C1: 35000 < rank <= 70000
  C2: rank > 70000, or absent from the frequency list entirely

Usage: python scripts/rebalance_de_a1.py [--dry-run]
"""
import os
import re
import sys
import urllib.request

DATA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")
TEMP = os.environ.get("TEMP", ".")
FREQ_SRC = os.path.join(TEMP, "de_full.txt")
FREQ_URL = "https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2018/de/de_full.txt"
HEADERS = {"User-Agent": "TopWordsApp/1.0 (educational; https://udsp.vercel.app)"}
ORIGINAL_BACKUP = os.path.join(TEMP, "gode_merge_backup", "wordsa1gode.js")

SOURCE_FILE = "wordsa1gode.js"
SOURCE_VARNAME = "WORDS_GODE_A1"

DEST_FILES = {
    "A2": ("wordsa2gode.js", "WORDS_GODE_A2"),
    "B1": ("wordsb1gode.js", "WORDS_GODE_B1"),
    "B2": ("wordsb2gode.js", "WORDS_GODE_B2"),
    "C1": ("wordsc1gode.js", "WORDS_GODE_C1"),
    "C2": ("wordsc2gode.js", "WORDS_GODE_C2"),
}

BANDS = [
    ("A1", 2500),
    ("A2", 6000),
    ("B1", 15000),
    ("B2", 35000),
    ("C1", 70000),
    ("C2", float("inf")),
]

ENTRY_RE = re.compile(
    r'\{\s*'
    r'word:\s*"((?:\\.|[^"\\])*)",\s*'
    r'pos:\s*"((?:\\.|[^"\\])*)",\s*'
    r'level:\s*"((?:\\.|[^"\\])*)",\s*'
    r'category:\s*"((?:\\.|[^"\\])*)",\s*'
    r'definition:\s*"((?:\\.|[^"\\])*)",\s*'
    r'example:\s*"((?:\\.|[^"\\])*)",?\s*'
    r'\}'
)

UNESCAPE_RE = re.compile(r"\\(.)")
ARTICLE_RE = re.compile(r"^(der|die|das)\s+", re.IGNORECASE)


def ensure_freq_file():
    if os.path.exists(FREQ_SRC):
        return
    print("[freq] downloading de_full.txt ...", flush=True)
    req = urllib.request.Request(FREQ_URL, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=120) as resp:
        data = resp.read()
    with open(FREQ_SRC, "wb") as f:
        f.write(data)


def load_rank_map():
    ensure_freq_file()
    rank = {}
    with open(FREQ_SRC, encoding="utf-8") as f:
        for i, line in enumerate(f):
            parts = line.split()
            if not parts:
                continue
            w = parts[0].strip().casefold()
            if w not in rank:
                rank[w] = i
    return rank


def parse_entries(path):
    text = open(path, encoding="utf-8").read()
    out = []
    for m in ENTRY_RE.finditer(text):
        out.append(
            {
                "word": m.group(1),
                "pos": m.group(2),
                "level": m.group(3),
                "category": m.group(4),
                "definition": m.group(5),
                "example": m.group(6),
            }
        )
    return out


def dedup_key(word_raw):
    w = UNESCAPE_RE.sub(r"\1", word_raw)
    bare = ARTICLE_RE.sub("", w)
    return bare.casefold()


def target_level(r):
    for level, ceiling in BANDS:
        if r <= ceiling:
            return level
    return "C2"


def format_entry(e):
    return (
        "  {\n"
        '    word: "%s",\n'
        '    pos: "%s",\n'
        '    level: "%s",\n'
        '    category: "%s",\n'
        '    definition: "%s",\n'
        '    example: "%s",\n'
        "  },"
    ) % (e["word"], e["pos"], e["level"], e["category"], e["definition"], e["example"])


def write_file(path, varname, header, entries):
    content = header + ("window.%s = [\n" % varname) + "\n".join(format_entry(e) for e in entries) + "\n];\n"
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(content)


def existing_header(path, varname):
    """Returns the file's current header comment block (everything before
    `window.VARNAME = [`), preserved verbatim so prior history notes in
    destination files are never discarded by this script."""
    text = open(path, encoding="utf-8").read()
    marker = "window.%s = [" % varname
    idx = text.find(marker)
    return text[:idx] if idx != -1 else ""


def main():
    dry_run = "--dry-run" in sys.argv

    if not os.path.exists(ORIGINAL_BACKUP):
        print("ERROR: %s not found -- cannot distinguish original self-authored\n"
              "words from bulk-harvested ones, refusing to run (see the module\n"
              "docstring; this script is only usable in the session where that\n"
              "backup was created)." % ORIGINAL_BACKUP)
        sys.exit(1)
    original_text = open(ORIGINAL_BACKUP, encoding="utf-8").read()
    original_words = re.findall(r'word:\s*"((?:\\.|[^"\\])*)"', original_text)
    original_keys = {dedup_key(w) for w in original_words}
    print("[original] %d pre-merge self-authored words exempted from reclassification" % len(original_keys))

    rank_map = load_rank_map()
    src_path = os.path.join(DATA_DIR, SOURCE_FILE)
    src_entries = parse_entries(src_path)
    print("[source] %s: %d entries parsed" % (SOURCE_FILE, len(src_entries)))

    keep = []
    moved = {lvl: [] for lvl in DEST_FILES}
    move_counts = {lvl: 0 for lvl in DEST_FILES}
    dupe_counts = {lvl: 0 for lvl in DEST_FILES}
    exempted = 0

    # Pre-load destination existing entries + their dedup-key sets.
    dest_existing = {}
    dest_seen = {}
    for lvl, (fname, _) in DEST_FILES.items():
        path = os.path.join(DATA_DIR, fname)
        entries = parse_entries(path)
        dest_existing[lvl] = entries
        dest_seen[lvl] = {dedup_key(e["word"]) for e in entries}

    for e in src_entries:
        key = dedup_key(e["word"])
        if key in original_keys:
            keep.append(e)
            exempted += 1
            continue
        r = rank_map.get(key, float("inf"))
        lvl = target_level(r)
        if lvl == "A1":
            keep.append(e)
            continue
        move_counts[lvl] += 1
        if key in dest_seen[lvl]:
            dupe_counts[lvl] += 1
            continue  # already exists at destination -- drop from A1, don't duplicate
        dest_seen[lvl].add(key)
        e2 = dict(e)
        e2["level"] = lvl
        moved[lvl].append(e2)

    print("\n[plan] A1 keeps: %d / %d (of which %d exempted-original, %d harvested-but-still-common-enough)" % (
        len(keep), len(src_entries), exempted, len(keep) - exempted
    ))
    for lvl in DEST_FILES:
        print(
            "[plan] -> %s: %d words identified, %d already existed at destination (dropped), %d newly appended"
            % (lvl, move_counts[lvl], dupe_counts[lvl], len(moved[lvl]))
        )
    total_moved = sum(len(v) for v in moved.values())
    total_dropped_dupe = sum(dupe_counts.values())
    print(
        "\n[summary] kept in A1: %d, relocated: %d, dropped as destination-duplicates: %d, total accounted: %d (should equal %d)"
        % (len(keep), total_moved, total_dropped_dupe, len(keep) + total_moved + total_dropped_dupe, len(src_entries))
    )

    if dry_run:
        print("\n[dry-run] no files written")
        return

    src_header = (
        "// German A1 vocabulary (self-authored CEFR base, merged with the former\n"
        "// telc sub-level word lists wordsa11de.js + wordsa12de.js -- de-duplicated by\n"
        "// casefolded bare word, der/die/das-stripped). Re-balanced by REAL frequency\n"
        "// rank (see scripts/rebalance_de_a1.py) -- words too advanced for genuine A1\n"
        "// difficulty were relocated to their correct CEFR gode file.\n"
    )
    write_file(src_path, SOURCE_VARNAME, src_header, keep)
    print("[write] %s: %d entries" % (SOURCE_FILE, len(keep)))

    for lvl, (fname, varname) in DEST_FILES.items():
        if not moved[lvl]:
            continue
        path = os.path.join(DATA_DIR, fname)
        combined = dest_existing[lvl] + moved[lvl]
        header = existing_header(path, varname) + (
            "// +%d word(s) relocated here from wordsa1gode.js by scripts/rebalance_de_a1.py\n"
            "// (their original telc sub-level label was too advanced for genuine A1\n"
            "// difficulty; reassigned by real frequency rank instead).\n"
        ) % len(moved[lvl])
        write_file(path, varname, header, combined)
        print("[write] %s: %d existing + %d relocated = %d entries" % (fname, len(dest_existing[lvl]), len(moved[lvl]), len(combined)))


if __name__ == "__main__":
    main()
