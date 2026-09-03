"""Quality scanner for data/words*.js entries.

Flags objectively-checkable defects (no judgement calls) so a human/agent can
review and fix the real problems instead of guessing where they are.

Defect codes
------------
POS_EMPTY    `pos` is missing or an empty string.
FALLBACK     definition/example still carries an "unavailable" fallback marker.
NO_TR        definition or example is missing its " - " Turkish half.
TR_ECHO      the Turkish half is byte-identical to the native half (untranslated).
NOT_IN_EX    single-token headword never appears in its own example sentence
             (exact or inflection-tolerant match).
PROPER       definition self-identifies the entry as a personal/place/brand name.
INFORMAL     definition self-identifies the entry as slang/an informal term.
SHORT_DEF    definition is suspiciously short (< 12 chars before the dash).
BAD_WORD     headword has digits/odd characters, or is a single letter.

Usage:  python scripts/_diag_quality.py [file.js ...]
        python scripts/_diag_quality.py --lang en
        python scripts/_diag_quality.py --lang en --codes NOT_IN_EX,PROPER --sample 30
"""
import argparse
import io
import os
import re
import sys
from collections import Counter, defaultdict

DATA = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "data")

LANG_FILES = {
    "en": ["wordsa1.js", "wordsa2.js", "wordsb1.js", "wordsb2.js", "wordsc1.js",
           "wordsc2.js", "toefl.js", "phrasalverbsen.js"],
    "de": ["wordsa1gode.js", "wordsa2gode.js", "wordsb1gode.js", "wordsb2gode.js",
           "wordsc1gode.js", "wordsc2gode.js", "partikelverbde.js"],
    "fr": ["wordsa1fr.js", "wordsa2fr.js", "wordsb1fr.js", "wordsb2fr.js",
           "wordsc1fr.js", "wordsc2fr.js", "phrasalverbsfr.js"],
    "it": ["wordsa1it.js", "wordsa2it.js", "wordsb1it.js", "wordsb2it.js",
           "wordsc1it.js", "wordsc2it.js"],
    "es": ["wordsa1es.js", "wordsa2es.js", "wordsb1es.js", "wordsb2es.js",
           "wordsc1es.js", "wordsc2es.js"],
}

# Tolerates both the multi-line indented schema and the single-line minified one.
FIELD = r'%s:\s*"((?:\\.|[^"\\])*)"'
ENTRY_RE = re.compile(
    r"\{\s*" + FIELD % "word" + r"\s*,\s*(?:" + FIELD % "pos" + r"\s*,\s*)?"
    + FIELD % "level" + r"\s*,\s*(?:" + FIELD % "category" + r"\s*,\s*)?"
    + FIELD % "definition" + r"\s*,\s*" + FIELD % "example",
    re.S,
)

FALLBACK_RE = re.compile(
    r"No example sentence available|No dictionary definition available|"
    r"Kein Beispielsatz|Aucune phrase d'exemple|Bu kelime için (örnek cümle|sözlük tanımı)",
    re.I,
)
PROPER_RE = re.compile(
    r"^(a |an |the )?(male|female|masculine|feminine)? ?(given |first |proper )?"
    r"(name|forename|surname)\b|"
    r"\b(a (city|town|village|river|state|province|county|region|island|mountain|port) (in|of)|"
    r"the capital of|a member of the [A-Z]|"
    r"United States (Navy|Army|Air Force)|"
    r"a (unit|coin) of .{0,30}(United Kingdom|United States))",
    re.I,
)
INFORMAL_RE = re.compile(
    r"\b(informal (term|form|word)s? (of|for)|"
    r"slang (for|term)|"
    r"a familiar term of address|"
    r"used as (a term of address|expletives)|"
    r"an informal (way|term|form))\b",
    re.I,
)
TOKEN_RE = re.compile(r"[^\W\d_]+", re.UNICODE)
BAD_WORD_RE = re.compile(r"[0-9_@#\\/]|^.$")

# Hand-authored entries in this project always write the native half of
# `definition` as a real sentence: capital letter, closing period. The
# bulk-harvested wordset-dictionary entries never do (lower-case start, no
# final period, machine-translated Turkish half). That style difference is the
# single most reliable, objective way to tell the two populations apart.
HARVEST_OK_START = re.compile(r"^[A-ZÄÖÜÀÂÇÉÈÊËÎÏÔÙÛŒÁÍÑÓÚÜ\"'(]")


def is_harvested(definition):
    native = definition.split(" - ")[0].split(";")[0].strip()
    if not native:
        return False
    return not (HARVEST_OK_START.match(native) and native.endswith("."))

ARTICLES = re.compile(
    r"^(der|die|das|le|la|les|l['\u2019]|il|lo|gli|i|el|los|las|un|une|to)\s+", re.I
)


def strip_article(w):
    return ARTICLES.sub("", w.strip()).strip()


def fuzzy_in(word, tokens):
    """Inflection-tolerant membership test (same heuristic as shared.js czFindSpan)."""
    w = word.casefold()
    if w in tokens:
        return True
    if len(w) < 5:
        return False
    for t in tokens:
        if abs(len(t) - len(w)) > 5:
            continue
        common = 0
        for a, b in zip(t, w):
            if a != b:
                break
            common += 1
        if common >= max(5, int(len(w) * 0.65)):
            return True
    return False


def scan_entry(word, pos, level, definition, example):
    codes = []
    bare = strip_article(word)

    if not pos or not pos.strip():
        codes.append("POS_EMPTY")
    if BAD_WORD_RE.search(word):
        codes.append("BAD_WORD")
    if FALLBACK_RE.search(definition) or FALLBACK_RE.search(example):
        codes.append("FALLBACK")

    for field in (definition, example):
        if " - " not in field and ";" not in field:
            codes.append("NO_TR")
            break
    else:
        for field in (definition, example):
            if " - " in field:
                a, b = field.split(" - ", 1)
                if a.strip().casefold() == b.strip().casefold():
                    codes.append("TR_ECHO")
                    break

    native_def = definition.split(" - ")[0]
    if is_harvested(definition):
        codes.append("HARVEST")
    if len(native_def.strip()) < 12:
        codes.append("SHORT_DEF")
    if PROPER_RE.search(native_def):
        codes.append("PROPER")
    if INFORMAL_RE.search(native_def):
        codes.append("INFORMAL")

    if " " not in bare and len(bare) >= 3:
        native_ex = example.split(" - ")[0]
        tokens = {t.casefold() for t in TOKEN_RE.findall(native_ex)}
        if not fuzzy_in(bare, tokens):
            codes.append("NOT_IN_EX")

    return codes


def scan_file(path):
    text = io.open(path, encoding="utf-8").read()
    rows = []
    for m in ENTRY_RE.finditer(text):
        word, pos, level, category, definition, example = m.groups()
        rows.append((word, pos or "", level, definition, example,
                     scan_entry(word, pos, level, definition, example)))
    declared = len(re.findall(r'\bword:\s*"', text))
    return rows, declared


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="*")
    ap.add_argument("--lang")
    ap.add_argument("--codes")
    ap.add_argument("--sample", type=int, default=0)
    args = ap.parse_args()

    names = args.files or LANG_FILES.get(args.lang or "", [])
    if not names:
        print("give files or --lang " + "/".join(LANG_FILES))
        return 1
    want = set(args.codes.split(",")) if args.codes else None

    grand = Counter()
    samples = defaultdict(list)
    total = 0
    for name in names:
        path = name if os.path.sep in name else os.path.join(DATA, name)
        if not os.path.exists(path):
            print("MISSING %s" % path)
            continue
        rows, declared = scan_file(path)
        if len(rows) != declared:
            print("!! %s parse mismatch: matched %d of %d entries"
                  % (os.path.basename(path), len(rows), declared))
        counts = Counter()
        for word, pos, level, definition, example, codes in rows:
            for c in codes:
                counts[c] += 1
                grand[c] += 1
                if want is None or c in want:
                    samples[c].append((os.path.basename(path), word, level,
                                       definition[:90], example[:90]))
        total += len(rows)
        flagged = sum(1 for r in rows if r[5])
        print("%-22s %6d entries  %5d flagged (%4.1f%%)  %s"
              % (os.path.basename(path), len(rows), flagged,
                 100.0 * flagged / max(1, len(rows)),
                 " ".join("%s=%d" % kv for kv in counts.most_common())))

    print("\nTOTAL entries scanned: %d" % total)
    for code, n in grand.most_common():
        print("  %-10s %6d" % (code, n))

    if args.sample:
        for code in sorted(samples):
            print("\n===== %s (showing %d) =====" % (code, args.sample))
            for row in samples[code][: args.sample]:
                print("  [%s] %-18s %-6s | %s | %s" % row)
    return 0


if __name__ == "__main__":
    sys.exit(main())
