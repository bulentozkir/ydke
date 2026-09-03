"""Cross-reference our English wordlists against the CEFR-J vocabulary profile.

CEFR-J (https://github.com/openlanguageprofiles/olp-en-cefrj) is an open,
CEFR-aligned English vocabulary profile: ~7,800 headwords each tagged A1..C2.
It is the closest thing to "does this word appear on a real language test"
that is freely checkable, and this repo already uses it in
scripts/apply_cefrj_levels.py.

For a given data file this reports each entry as:
  OK          in CEFR-J at the file's level (or one adjacent level)
  MISLEVEL    in CEFR-J but at a clearly different level (gap >= 2)
  ABSENT      not in CEFR-J at all
and separately lists CEFR-J words for the level that our file is MISSING,
which is the pool to backfill from.

Usage: python scripts/_diag_cefrj_membership.py wordsa1.js A1
"""
import io
import os
import re
import sys
import csv
import tempfile
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")
CSV_URL = ("https://raw.githubusercontent.com/openlanguageprofiles/olp-en-cefrj/"
           "master/cefrj-vocabulary-profile-1.5.csv")
CSV_PATH = os.path.join(tempfile.gettempdir(), "cefrj-vocabulary-profile-1.5.csv")

LEVELS = ["A1", "A2", "B1", "B2", "C1", "C2"]
ENTRY_RE = re.compile(
    r'\{\s*word:\s*"((?:\\.|[^"\\])*)"\s*,\s*(?:pos:\s*"((?:\\.|[^"\\])*)"\s*,\s*)?'
    r'level:\s*"([^"]*)"[^}]*?definition:\s*"((?:\\.|[^"\\])*)"', re.S)
HARVEST_OK_START = re.compile(r"^[A-Z\"'(]")


def load_cefrj():
    if not os.path.exists(CSV_PATH):
        urllib.request.urlretrieve(CSV_URL, CSV_PATH)
    levels = {}
    with io.open(CSV_PATH, encoding="utf-8-sig", newline="") as fh:
        for row in csv.DictReader(fh):
            head = (row.get("headword") or "").strip().lower()
            lvl = (row.get("CEFR") or "").strip().upper()
            if not head or lvl not in LEVELS:
                continue
            # CEFR-J packs spelling variants into one cell:
            # "favorite/favourite", "airplane/aeroplane", "email/e-mail".
            # Register every variant so US spellings are not falsely "absent".
            for form in head.split("/"):
                form = form.strip().rstrip(".")
                if not form:
                    continue
                if form not in levels or LEVELS.index(lvl) < LEVELS.index(levels[form]):
                    levels[form] = lvl
    return levels


def lemma_candidates(w):
    """Cheap English de-inflection: enough to spot 'beaten'/'bigger'/'dozens'."""
    out = set()
    for suf, repl in (("s", ""), ("es", ""), ("ed", ""), ("ed", "e"), ("en", ""),
                      ("en", "e"), ("ing", ""), ("ing", "e"), ("er", ""), ("er", "e"),
                      ("est", ""), ("est", "e"), ("ly", ""), ("ies", "y"),
                      ("ied", "y"), ("ier", "y"), ("iest", "y")):
        if w.endswith(suf) and len(w) - len(suf) >= 3:
            out.add(w[: len(w) - len(suf)] + repl)
    if len(w) > 4 and w[-1] == w[-2]:          # stopped -> stop, bigger -> big
        out.add(w[:-1])
    for base in list(out):
        if len(base) > 3 and base[-1] == base[-2]:
            out.add(base[:-1])
    return {b for b in out if len(b) >= 2}


def main():
    fname, level = sys.argv[1], sys.argv[2].upper()
    path = os.path.join(DATA, fname)
    text = io.open(path, encoding="utf-8").read()
    cefrj = load_cefrj()
    print("CEFR-J headwords loaded: %d" % len(cefrj))

    ours, buckets = set(), {"OK": [], "MISLEVEL": [], "INFLECTION": [], "ABSENT": []}
    entries = []
    for m in ENTRY_RE.finditer(text):
        word, _pos, lvl, definition = m.groups()
        entries.append((word, definition))
        ours.add(word.strip().lower())

    for word, definition in entries:
        w = word.strip().lower()
        native = definition.split(" - ")[0].strip()
        harvested = not (HARVEST_OK_START.match(native) and native.endswith("."))
        ref = cefrj.get(w)
        if ref is not None:
            if abs(LEVELS.index(ref) - LEVELS.index(level)) >= 2:
                buckets["MISLEVEL"].append((word, harvested, ref))
            else:
                buckets["OK"].append((word, harvested))
            continue
        # Not a CEFR-J headword: is it just an inflected form of one we already
        # teach (either in CEFR-J or already present in this very file)?
        lemmas = lemma_candidates(w)
        hit = next((b for b in sorted(lemmas) if b in cefrj or b in ours), None)
        if hit:
            buckets["INFLECTION"].append((word, harvested, hit))
        else:
            buckets["ABSENT"].append((word, harvested))

    total = sum(len(v) for v in buckets.values())
    print("\n%s  (%d entries)" % (fname, total))
    for k in ("OK", "MISLEVEL", "INFLECTION", "ABSENT"):
        rows = buckets[k]
        harv = sum(1 for r in rows if r[1])
        print("  %-10s %5d   (%d of them bulk-harvested)" % (k, len(rows), harv))

    absent_harv = [w for w, h in buckets["ABSENT"] if h]
    print("\nABSENT + harvested = %d  <- primary removal candidates" % len(absent_harv))
    print("  " + ", ".join(sorted(absent_harv)))

    infl_harv = [(w, b) for w, h, b in buckets["INFLECTION"] if h]
    print("\nINFLECTION + harvested = %d  <- redundant with a word we already teach"
          % len(infl_harv))
    print("  " + ", ".join("%s(<-%s)" % r for r in sorted(infl_harv)[:80]))

    missing = sorted(w for w, lv in cefrj.items() if lv == level and w not in ours)
    print("\nCEFR-J %s words MISSING from our file: %d  <- backfill pool" % (level, len(missing)))
    print("  " + ", ".join(missing))


if __name__ == "__main__":
    main()
