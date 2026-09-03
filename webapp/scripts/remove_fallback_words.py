"""Remove word entries whose definition is the honest 'no definition' fallback.

Across data/*.js, words that could not be matched to any dictionary were given
the placeholder definition:
  "No dictionary definition available for this word. - Bu kelime icin sozluk tanimi bulunamadi."
These are mostly junk / obscure inflected forms from the bulk frequency-list
imports and are not useful learning words. This script removes any entry object
whose block contains that fallback marker.

Safe for the multi-line entry format used by words*.js / *gode.js:
  {
    word: "...",
    pos: "...",
    level: "...",
    definition: "No dictionary definition available for this word. - ...",
    example: "...",
  },

Entry blocks are detected purely by indentation (a lone "  {" opener through
its matching "  }," / "  }" closer), so kept lines keep their exact original
text and line endings -- no reformatting / newline churn.
"""

import os

FALLBACK = "No dictionary definition available for this word"
DATA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")

# Only the files known to contain the fallback (all standard multi-line
# window.WORDS_* arrays). The single-line synant*/schema files use a different
# format and their own conventions, so they are intentionally excluded.
FILES = [
    "wordsa1gode.js",
    "wordsa2gode.js",
    "wordsb1gode.js",
    "wordsb2.js",
    "wordsb2gode.js",
    "wordsc1.js",
    "wordsc1gode.js",
    "wordsc2.js",
    "wordsc2gode.js",
]


def process(path):
    with open(path, "r", encoding="utf-8", newline="") as f:
        lines = f.readlines()

    out = []
    i = 0
    n = len(lines)
    removed = 0
    while i < n:
        if lines[i].rstrip() == "{":
            # Not an entry opener (would be at column 0); keep as-is.
            out.append(lines[i])
            i += 1
            continue
        if lines[i].rstrip() == "  {":
            # Collect the entry block up to and including its closing line.
            j = i
            while j < n and lines[j].rstrip() not in ("  },", "  }"):
                j += 1
            block = lines[i : j + 1]
            if any(FALLBACK in ln for ln in block):
                removed += 1
            else:
                out.extend(block)
            i = j + 1
        else:
            out.append(lines[i])
            i += 1

    with open(path, "w", encoding="utf-8", newline="") as f:
        f.writelines(out)
    return removed


def main():
    total = 0
    for fn in FILES:
        p = os.path.join(DATA_DIR, fn)
        if not os.path.exists(p):
            print(f"{fn}: SKIP (not found)")
            continue
        r = process(p)
        total += r
        print(f"{fn}: removed {r}")
    print(f"TOTAL removed: {total}")


if __name__ == "__main__":
    main()
