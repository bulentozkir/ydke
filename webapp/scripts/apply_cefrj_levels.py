#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""apply_cefrj_levels.py  (PERMANENT)

Corrects English CEFR word levels against the open, authoritative CEFR-J
vocabulary profile (https://github.com/openlanguageprofiles/olp-en-cefrj),
replacing the pure frequency-rank heuristic for the ~5k common words it
covers.

Direction is DOWNWARD-ONLY by default: only move a word to an EASIER level
when CEFR-J rates it easier than its current file. Rationale -- CEFR-J grades
PRODUCTIVE mastery (conservative/high), so if even that source rates a word
easier than we do, our level is definitely too hard; the reverse (moving a
word UP because CEFR-J's productive level is higher) is unsafe for a
RECEPTIVE vocabulary app and is skipped. `--min-gap` bounds how big the
level gap must be (default 2 = only clear errors like arm/just/adjective
sitting at C1/C2).

Moving a word = physically relocating its entry block from the source
wordsXX.js to the destination level file (the file IS the level here) and
rewriting its `level:` field. Blocks are detected by indentation (a lone
"  {" .. "  }," ), so kept lines are byte-identical (read/write newline="").
De-dups against the destination. Verifies total word count is unchanged.

    python scripts/apply_cefrj_levels.py               # dry run (report)
    python scripts/apply_cefrj_levels.py --apply       # rewrite files
    python scripts/apply_cefrj_levels.py --min-gap 1   # include +/-1 refinements
"""
import os
import re
import sys
import csv
import urllib.request
import collections

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")
TEMP = os.environ.get("TEMP", ".")
CSV_URL = "https://raw.githubusercontent.com/openlanguageprofiles/olp-en-cefrj/master/cefrj-vocabulary-profile-1.5.csv"
CSV_PATH = os.path.join(TEMP, "cefrj-1.5.csv")

ORDER = {"A1": 1, "A2": 2, "B1": 3, "B2": 4, "C1": 5, "C2": 6}
INV = {v: k for k, v in ORDER.items()}
LEVEL_FILE = {"A1": "wordsa1.js", "A2": "wordsa2.js", "B1": "wordsb1.js",
              "B2": "wordsb2.js", "C1": "wordsc1.js", "C2": "wordsc2.js"}
FILE_LEVEL = {v: k for k, v in LEVEL_FILE.items()}

WORD_LINE = re.compile(r'^\s*word:\s*"((?:\\.|[^"\\])*)"')
LEVEL_LINE = re.compile(r'^(\s*level:\s*")[^"]*(",?\s*)$')


def load_cefrj():
    if not os.path.exists(CSV_PATH):
        urllib.request.urlretrieve(CSV_URL, CSV_PATH)
    m = {}
    with open(CSV_PATH, encoding="utf-8") as f:
        for row in csv.DictReader(f):
            hw = (row.get("headword") or "").strip().lower()
            lv = (row.get("CEFR") or "").strip().upper()
            if not hw or lv not in ORDER:
                continue
            for part in re.split(r"[/,]", hw):
                p = part.strip()
                if p:
                    m[p] = min(m.get(p, 9), ORDER[lv])
    return m


def read_blocks(path):
    """Return (lines, blocks) where blocks is a list of (start,end,word) for
    each top-level entry (a lone '  {' through its '  },'/'  }' closer)."""
    with open(path, "r", encoding="utf-8", newline="") as f:
        lines = f.readlines()
    blocks = []
    i, n = 0, len(lines)
    while i < n:
        if lines[i].rstrip() == "  {":
            j = i
            while j < n and lines[j].rstrip() not in ("  },", "  }"):
                j += 1
            word = None
            for k in range(i, min(j + 1, n)):
                wm = WORD_LINE.match(lines[k])
                if wm:
                    word = wm.group(1)
                    break
            blocks.append((i, j, word))
            i = j + 1
        else:
            i += 1
    return lines, blocks


def main():
    apply = "--apply" in sys.argv
    min_gap = 2
    if "--min-gap" in sys.argv:
        min_gap = int(sys.argv[sys.argv.index("--min-gap") + 1])
    cefrj = load_cefrj()
    print(f"CEFR-J: {len(cefrj)} headword forms; min-gap={min_gap}; "
          f"mode={'APPLY' if apply else 'DRY RUN'}")

    # Existing words per level file (lowercased) for de-dup.
    existing = {}
    file_lines = {}
    file_blocks = {}
    total_before = 0
    for lvl, fn in LEVEL_FILE.items():
        p = os.path.join(DATA, fn)
        lines, blocks = read_blocks(p)
        file_lines[fn] = lines
        file_blocks[fn] = blocks
        existing[fn] = set(b[2].lower() for b in blocks if b[2])
        total_before += len(blocks)

    # Plan moves: source file -> list of (block, dest_level)
    moves = collections.Counter()
    to_remove = collections.defaultdict(list)   # fn -> set of block start indices
    to_add = collections.defaultdict(list)      # dest_fn -> list of new block text
    examples = collections.defaultdict(list)
    for lvl, fn in LEVEL_FILE.items():
        cur = ORDER[lvl]
        lines = file_lines[fn]
        for (s, e, word) in file_blocks[fn]:
            if not word:
                continue
            tgt = cefrj.get(word.lower())
            if not tgt:
                continue
            gap = cur - tgt          # >0 means we rate it harder than CEFR-J
            if gap < min_gap:        # downward-only, at least min_gap easier
                continue
            dest_lvl = INV[tgt]
            dest_fn = LEVEL_FILE[dest_lvl]
            if word.lower() in existing[dest_fn]:
                continue             # already there -> don't duplicate
            block = lines[s:e + 1]
            block = [LEVEL_LINE.sub(r"\1" + dest_lvl + r"\2", ln) for ln in block]
            to_remove[fn].append(s)
            to_add[dest_fn].append("".join(block))
            existing[dest_fn].add(word.lower())
            moves[(lvl, dest_lvl)] += 1
            if len(examples[(lvl, dest_lvl)]) < 5:
                examples[(lvl, dest_lvl)].append(word)

    print("\n=== planned downward re-levels (from -> to: count) ===")
    grand = 0
    for (frm, to), n in sorted(moves.items(), key=lambda kv: -kv[1]):
        grand += n
        print(f"  {frm} -> {to} (-{ORDER[frm]-ORDER[to]}): {n:5}   e.g. {', '.join(examples[(frm,to)])}")
    print(f"TOTAL moves: {grand}")

    if not apply:
        return

    # Rewrite each file: drop removed blocks, append added blocks before ']'.
    total_after = 0
    for fn in LEVEL_FILE.values():
        p = os.path.join(DATA, fn)
        lines = file_lines[fn]
        remove_starts = set(to_remove.get(fn, []))
        blocks = file_blocks[fn]
        drop_ranges = [(s, e) for (s, e, w) in blocks if s in remove_starts]
        drop_line = set()
        for (s, e) in drop_ranges:
            for k in range(s, e + 1):
                drop_line.add(k)
        kept = [ln for idx, ln in enumerate(lines) if idx not in drop_line]
        added = to_add.get(fn, [])
        if added:
            # insert before the last line that is the closing "];"
            close_idx = None
            for idx in range(len(kept) - 1, -1, -1):
                if kept[idx].rstrip() == "];":
                    close_idx = idx
                    break
            assert close_idx is not None, f"{fn}: no closing ]; found"
            kept = kept[:close_idx] + added + kept[close_idx:]
        with open(p, "w", encoding="utf-8", newline="") as f:
            f.writelines(kept)
        # recount
        _, nb = read_blocks(p)
        total_after += len(nb)
        print(f"[apply] {fn}: -{len(drop_ranges)} +{len(added)} -> {len(nb)} words")

    print(f"\nTOTAL words before={total_before} after={total_after} "
          f"({'OK' if total_before == total_after else 'MISMATCH!'})")


if __name__ == "__main__":
    main()
