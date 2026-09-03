# -*- coding: utf-8 -*-
"""List entries whose harvested EXAMPLE or DEFINITION contains crude language.

These are good headwords (parachute, itchy, intestine) that were paired with
an unsuitable sentence by the bulk harvest. The word must be kept; only the
sentence needs replacing, so they are reported rather than deleted.

Run: python scripts/_diag_bad_context.py > bad_context.txt
"""
import io
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from clean_wordlists import CRUDE_RE, ENTRY_RE, FIELD_RE, LANG_FILES  # noqa: E402

DATA = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "data")


def main():
    total = 0
    for lang in ("en", "de", "fr", "it"):
        for name in LANG_FILES.get(lang, []):
            path = os.path.join(DATA, name)
            if not os.path.exists(path):
                continue
            text = io.open(path, encoding="utf-8").read()
            for block in ENTRY_RE.findall(text):
                get = lambda k: (FIELD_RE[k].search(block).group(1)
                                 if FIELD_RE[k].search(block) else "")
                word, definition, example = (get("word"), get("definition"),
                                             get("example"))
                if CRUDE_RE.search(word):
                    continue
                hit = CRUDE_RE.search(definition) or CRUDE_RE.search(example)
                if not hit:
                    continue
                total += 1
                where = "def" if CRUDE_RE.search(definition) else "ex"
                print("%-22s %-18s %-4s %s" % (name, word, where,
                                               (example if where == "ex"
                                                else definition)[:110]))
    print("\nTOTAL %d" % total)


if __name__ == "__main__":
    main()
