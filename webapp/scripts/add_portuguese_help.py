# -*- coding: utf-8 -*-
"""Add Portuguese to the per-language sections inside help.html.

help.html carries a full guide in six languages, and each guide independently
enumerates the languages the app supports. Those lists were already stale
before Portuguese (the German and French sections still listed only three
languages when Italian was added), so they are fixed here in one pass.

Every replacement is anchored on the exact existing wording, and is skipped if
the Portuguese form is already present, so the script is safe to re-run.

Run: python scripts/add_portuguese_help.py --apply
"""
import io
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATH = os.path.join(ROOT, "help.html")

PAIRS = [
    # --- Turkish section ---
    ("<em>French</em>, <em>Italian</em> veya <em>Spanish</em> — çalışacağınız dili seçin.",
     "<em>French</em>, <em>Italian</em>, <em>Spanish</em> veya <em>Portuguese</em> — çalışacağınız dili seçin."),

    # --- English section ---
    ("<em>French</em>, <em>Italian</em> or <em>Spanish</em>.",
     "<em>French</em>, <em>Italian</em>, <em>Spanish</em> or <em>Portuguese</em>."),
    ("              Italian and Spanish —\n",
     "              Italian, Spanish and Portuguese —\n"),

    # --- German section ---
    ("<em>French</em>, <em>Italian</em> oder <em>Spanish</em>.",
     "<em>French</em>, <em>Italian</em>, <em>Spanish</em> oder <em>Portuguese</em>."),
    ("              Italienisch und Spanisch —\n",
     "              Italienisch, Spanisch und Portugiesisch —\n"),

    # --- French section ---
    ("vocabulaire <strong>anglais, allemand, français, italien et espagnol</strong>",
     "vocabulaire <strong>anglais, allemand, français, italien, espagnol et portugais</strong>"),
    ("<em>French</em>, <em>Italian</em> ou <em>Spanish</em>.",
     "<em>French</em>, <em>Italian</em>, <em>Spanish</em> ou <em>Portuguese</em>."),
    ("              italien et espagnol\n",
     "              italien, espagnol et portugais\n"),

    # --- Italian section ---
    ("vocabolario <strong>inglese, tedesco, francese, italiano e spagnolo</strong>",
     "vocabolario <strong>inglese, tedesco, francese, italiano, spagnolo e portoghese</strong>"),
    ("<em>French</em>, <em>Italian</em> o <em>Spanish</em>.\n",
     "<em>French</em>, <em>Italian</em>, <em>Spanish</em> o <em>Portuguese</em>.\n"),
    ("              francese, italiano e spagnolo — <em>A1–C2</em>.",
     "              francese, italiano, spagnolo e portoghese — <em>A1–C2</em>."),
]


def main():
    apply_changes = "--apply" in sys.argv
    text = io.open(PATH, encoding="utf-8").read()
    applied, skipped = 0, []

    for search, replace in PAIRS:
        if replace in text:
            skipped.append(search[:44] + "  (already done)")
            continue
        if search not in text:
            skipped.append(search[:44] + "  (NOT FOUND)")
            continue
        text = text.replace(search, replace, 1)
        applied += 1

    print("applied %d of %d" % (applied, len(PAIRS)))
    for s in skipped:
        print("  skip: " + s.replace("\n", ""))

    if apply_changes and applied:
        io.open(PATH, "w", encoding="utf-8", newline="\n").write(text)
        print("written")
    elif not apply_changes:
        print("DRY RUN - pass --apply to write")
    return 0


if __name__ == "__main__":
    sys.exit(main())
