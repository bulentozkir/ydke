# -*- coding: utf-8 -*-
"""Hand-fix the ten fields fix_double_separator.py could not resolve safely.

Each was inspected individually (scripts/_diag_show_double.py). In every case
the FIRST " - " is the real native/Turkish separator and the later ones are
ordinary dashes inside the Turkish half - an attribution, an apposition, or a
duplicated clause. They are rewritten as em dashes, or the duplicate is
dropped, so exactly one " - " remains.

Run: python scripts/fix_double_separator_manual.py --apply
"""
import io
import os
import sys

DATA = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "data")

# (file, old fragment, new fragment)
FIXES = [
    # separator here is ";" (synant format); both " - " are in-sentence
    ("synantde.js",
     "Babası - benim büyükbabam - bir aşçıydı",
     "Babası — benim büyükbabam — bir aşçıydı"),

    # "; - applied to..." is a continuation dash, not a separator
    # (same definition appears on abrogated / abrogates / abrogating)
    ("wordsb2.js",
     "yürürlükten kaldırmak için; - kanunların",
     "yürürlükten kaldırmak için; — kanunların"),

    # quotation attribution
    ("wordsb2.js",
     "gözümüze getirirler. - Leigh Hunt",
     "gözümüze getirirler. — Leigh Hunt"),

    # apposition inside the Turkish gloss
    ("wordsb2fr.js",
     "muhbir, muhbir - bilgi veren kimse",
     "muhbir, muhbir — bilgi veren kimse"),

    # the Turkish clause was duplicated
    ("wordsb2fr.js",
     "Diyelim ki sana unutturabildi. - Diyelim ki sana unutturabildi.",
     "Diyelim ki sana unutturabildi."),

    # apposition inside the Turkish gloss
    ("wordsb2gode.js",
     "Bawırğalıq - eyer, dizgin.",
     "Bawırğalıq — eyer, dizgin."),

    # attribution on both halves
    ("wordsc2.js",
     "the easy chair- J.S.Perelman - rahat sandalyenin kolunda degage pozu "
     "benimsedi - J.S.Perelman",
     "the easy chair — J.S.Perelman - rahat sandalyenin kolunda degage pozu "
     "benimsedi — J.S.Perelman"),

    # was "French - English gloss - Turkish - Turkish"; replaced with a real
    # example sentence in the file's normal shape
    ("wordsc2fr.js",
     "faire de l'équitation - go horseriding - ata binmeye git - "
     "ata binmeye git",
     "J'aime faire de l'équitation le week-end. - "
     "Hafta sonları ata binmeyi severim."),
]


def main():
    apply_changes = "--apply" in sys.argv
    total, missing = 0, []
    for name, old, new in FIXES:
        path = os.path.join(DATA, name)
        text = io.open(path, encoding="utf-8").read()
        n = text.count(old)
        if not n:
            missing.append("%s: %s" % (name, old[:60]))
            continue
        total += n
        print("%-18s %d x  %s" % (name, n, old[:56]))
        if apply_changes:
            io.open(path, "w", encoding="utf-8", newline="\n").write(
                text.replace(old, new))
    print("\n%d replacements%s" % (total, "" if apply_changes else "  (DRY RUN)"))
    if missing:
        print("NOT FOUND:")
        for m in missing:
            print("  " + m)
    return 0


if __name__ == "__main__":
    sys.exit(main())
