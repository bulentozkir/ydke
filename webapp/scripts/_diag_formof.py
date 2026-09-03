"""Find entries whose definition is a Wiktionary 'form-of' definition
(present participle of X, gerund of X, inflection of X, ...) -- these are
inflected forms masquerading as headwords.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _diag_gerunds as g  # noqa: E402

FORM_OF = re.compile(
    r"(present participle|past participle|gerund|inflection of|"
    r"plural of|singular of|comparative of|superlative of|"
    r"simple past|third-person singular|-ing form|participle of)",
    re.I,
)


def main():
    files = sorted(f for f in os.listdir(g.DATA) if f.endswith(".js"))
    total = 0
    for f in files:
        for e in g.parse_file(os.path.join(g.DATA, f)):
            d = e.get("definition") or ""
            eng = d.split(" - ")[0]
            if FORM_OF.search(eng):
                print(f"{f[:-3]:16} {e['word']:24} [{e.get('pos','')}] | {eng[:80]}")
                total += 1
    print(f"TOTAL: {total}")


if __name__ == "__main__":
    main()
