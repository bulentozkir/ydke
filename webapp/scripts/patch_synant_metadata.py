#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""patch_synant_metadata.py

One-off (but safely re-runnable) migration: assigns real CEFR levels and
adds a `category` field to the 3 existing data/synant*.js files, using
scripts/synant_metadata.py's frequency-rank level split + reused
classify_word_categories.py keyword classifier. Patches the files IN
PLACE, parsing the CURRENT content directly (does NOT regenerate from the
original raw source lists / gloss caches -- those may no longer be cached
in %TEMP%, and are not needed since the existing definition/example/
synonyms/antonyms content is already correct and must be preserved
byte-for-byte).

Usage: python scripts/patch_synant_metadata.py [en|de|fr|all]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from synant_metadata import assign_levels, assign_categories  # noqa: E402

DATA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")

FILES = {
    "en": ("synanten.js", "en"),
    "de": ("synantde.js", "de"),
    "fr": ("synantfr.js", "fr"),
}

ENTRY_RE = re.compile(
    r'\{word:"((?:\\.|[^"\\])*)",'
    r'level:"((?:\\.|[^"\\])*)",'
    r'definition:"((?:\\.|[^"\\])*)",'
    r'example:"((?:\\.|[^"\\])*)"'
    r'(?:,synonyms:"((?:\\.|[^"\\])*)")?'
    r'(?:,antonyms:"((?:\\.|[^"\\])*)")?\}'
)

UNESCAPE_RE = re.compile(r'\\(.)')


def unescape_js(s):
    return UNESCAPE_RE.sub(r"\1", s or "")


def native_half(definition_raw):
    """definition_raw is the RAW (still JS-escaped) captured group. `;` is
    never escaped by this project's js_escape(), so splitting the raw
    string on the first literal ';' is safe and gives the native-language
    half; unescape afterward to get real text for classification."""
    idx = definition_raw.find(";")
    piece = definition_raw[:idx] if idx != -1 else definition_raw
    return unescape_js(piece)


HEADER_SCHEMA_OLD = (
    "// Schema: { word, level, definition, example, synonyms, antonyms }. "
    'definition="<native definition>;<Turkish>", example="<native example>;<Turkish>".'
)
HEADER_SCHEMA_NEW = (
    "// Schema: { word, level, category, definition, example, synonyms, antonyms }. "
    'definition="<native definition>;<Turkish>", example="<native example>;<Turkish>".\n'
    "// `level` (A1..C2) is assigned by a frequency-rank sextile split (see\n"
    "// scripts/synant_metadata.py); `category` is a topical domain assigned by\n"
    "// scripts/classify_word_categories.py's keyword classifier (German definitions\n"
    "// are translated to English once, cache-only, purely for classification)."
)


def process_file(lang):
    fname, freq_lang = FILES[lang]
    path = os.path.join(DATA_DIR, fname)
    with open(path, encoding="utf-8") as f:
        content = f.read()

    matches = list(ENTRY_RE.finditer(content))
    if not matches:
        print("[%s] no entries matched -- ENTRY_RE may be stale, aborting" % fname)
        return
    words = [unescape_js(m.group(1)) for m in matches]
    native_defs = [native_half(m.group(3)) for m in matches]

    levels = assign_levels(words, freq_lang)
    categories = assign_categories(native_defs, freq_lang)

    def repl(m, i=[0]):
        idx = i[0]
        i[0] += 1
        word_raw, _old_level, def_raw, ex_raw, syn_raw, ant_raw = m.groups()
        parts = [
            'word:"%s"' % word_raw,
            'level:"%s"' % levels[idx],
            'category:"%s"' % categories[idx],
            'definition:"%s"' % def_raw,
            'example:"%s"' % ex_raw,
        ]
        if syn_raw is not None:
            parts.append('synonyms:"%s"' % syn_raw)
        if ant_raw is not None:
            parts.append('antonyms:"%s"' % ant_raw)
        return "{" + ",".join(parts) + "}"

    new_content = ENTRY_RE.sub(repl, content)

    if HEADER_SCHEMA_OLD in new_content:
        new_content = new_content.replace(HEADER_SCHEMA_OLD, HEADER_SCHEMA_NEW)
    else:
        # French file's header is hand-authored/multi-line and phrased
        # differently -- see the dedicated fr-specific header patch below.
        pass

    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(new_content)

    from collections import Counter
    level_dist = Counter(levels)
    cat_dist = Counter(categories)
    print("[%s] %d entries patched. Levels: %s" % (fname, len(matches), dict(sorted(level_dist.items()))))
    print("[%s] top categories: %s" % (fname, cat_dist.most_common(8)))


def patch_fr_header():
    path = os.path.join(DATA_DIR, "synantfr.js")
    with open(path, encoding="utf-8") as f:
        content = f.read()
    old = (
        '// - Schema: { word, level, definition, example, synonyms, antonyms } --\n'
        '//   note there is NO `pos` field here (unlike wordsXXfr.js), matching the\n'
        '//   EN/DE synant files exactly. `synonyms`/`antonyms` are OMITTED (not\n'
    )
    new = (
        '// - Schema: { word, level, category, definition, example, synonyms, antonyms }\n'
        '//   -- note there is NO `pos` field here (unlike wordsXXfr.js), matching the\n'
        '//   EN/DE synant files exactly. `level` (A1..C2) is a frequency-rank sextile\n'
        '//   split (see scripts/synant_metadata.py); `category` is a topical domain\n'
        '//   from scripts/classify_word_categories.py\'s keyword classifier, run\n'
        '//   directly against this file\'s (already-English) `definition` gloss.\n'
        '//   `synonyms`/`antonyms` are OMITTED (not\n'
    )
    if old in content:
        content = content.replace(old, new)
    content = content.replace(
        "// Used ONLY by Word Morph (wordmorph.js) if/when wired in -- not yet\n"
        "// referenced by any page (same as the 2 files it replaces).",
        "// Used by Word Morph (wordmorph.js), same as data/synanten.js / data/synantde.js.",
    )
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(content)


def main():
    which = sys.argv[1] if len(sys.argv) > 1 else "all"
    langs = ["en", "de", "fr"] if which == "all" else [which]
    for lang in langs:
        process_file(lang)
    if "fr" in langs:
        patch_fr_header()


if __name__ == "__main__":
    main()
