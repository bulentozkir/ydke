#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""remove_inappropriate_words.py  (PERMANENT content-safety tool)

Removes genuinely vulgar / crude-slang / explicit / slur HEADWORD entries
from the data/*.js word lists, and scrubs example sentences that contain
hard profanity while keeping the (legitimate) word+definition.

This is a HIGH-PRECISION pass, NOT the broad net of
_diag_final_safety_scan.py: it deliberately keeps clinical / anatomical /
medical vocabulary ("sexuell", "homosexuell", clinical "penis"/"vagina" at
advanced levels, medical terms, "breast"/"nackt", etc.) -- those are
legitimate learner vocabulary. Only unambiguously crude/vulgar/explicit
slang and slurs are removed.

Design (mirrors remove_fallback_words.py's safe block handling):
  * Multi-line files: entry blocks detected purely by indentation (a lone
    "  {" through its "  }," / "  }" closer). Kept lines keep exact text /
    line endings (read/write newline="").
  * Minified synant*.js: one entry per line ({ ... },) -- matched line-wise.

Default = DRY RUN (report only). Pass --apply to actually rewrite files.

    python scripts/remove_inappropriate_words.py            # report
    python scripts/remove_inappropriate_words.py --apply    # rewrite
"""
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

DATA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")

# Standard multi-line word files (word/pos/level/category/definition/example).
STD_FILES = [
    "wordsa1.js", "wordsa2.js", "wordsb1.js", "wordsb2.js", "wordsc1.js", "wordsc2.js",
    "wordsa1gode.js", "wordsa2gode.js", "wordsb1gode.js", "wordsb2gode.js",
    "wordsc1gode.js", "wordsc2gode.js",
    "wordsa1fr.js", "wordsa2fr.js", "wordsb1fr.js", "wordsb2fr.js", "wordsc1fr.js", "wordsc2fr.js",
    "phrasalverbsen.js", "phrasalverbsfr.js", "toefl.js", "partikelverbde.js",
]
# Minified single-line Word Morph files ({ word:"..", level:"..", ... },).
SYN_FILES = ["synanten.js", "synantde.js", "synantfr.js"]


def lang_of(fn):
    if fn.endswith("fr.js") or fn == "phrasalverbsfr.js":
        return "fr"
    if "gode" in fn or fn == "partikelverbde.js" or fn.endswith("de.js"):
        return "de"
    return "en"


# Detection rules live in content_safety.py (shared with the expand_*.py
# harvest scripts). is_inappropriate(word, lang) -> bool for headword removal;
# EXAMPLE_CRUDE_RE for crude example scrubbing.
from content_safety import is_inappropriate, EXAMPLE_CRUDE_RE

FALLBACK = {
    "en": "No example sentence available for this word. - Bu kelime i\u00e7in \u00f6rnek c\u00fcmle bulunamad\u0131.",
    "de": "Kein Beispielsatz f\u00fcr dieses Wort verf\u00fcgbar. - Bu kelime i\u00e7in \u00f6rnek c\u00fcmle bulunamad\u0131.",
    "fr": "Aucune phrase d'exemple disponible pour ce mot. - Bu kelime i\u00e7in \u00f6rnek c\u00fcmle bulunamad\u0131.",
}
# synant*.js use a ';' (not ' - ') separator between native and Turkish halves.
SYN_FALLBACK = {
    "en": "No example sentence available for this word.;Bu kelime i\u00e7in \u00f6rnek c\u00fcmle bulunamad\u0131.",
    "de": "Kein Beispielsatz f\u00fcr dieses Wort verf\u00fcgbar.;Bu kelime i\u00e7in \u00f6rnek c\u00fcmle bulunamad\u0131.",
    "fr": "Aucune phrase d'exemple disponible pour ce mot.;Bu kelime i\u00e7in \u00f6rnek c\u00fcmle bulunamad\u0131.",
}

WORD_LINE = re.compile(r'^\s*word:\s*"((?:\\.|[^"\\])*)"')
EX_LINE = re.compile(r'^(\s*example:\s*")((?:\\.|[^"\\])*)(",?\s*)$')


def process_std(path, lang, apply):
    with open(path, "r", encoding="utf-8", newline="") as f:
        lines = f.readlines()
    out, i, n = [], 0, len(lines)
    removed, scrubbed = [], []
    while i < n:
        if lines[i].rstrip() == "  {":
            j = i
            while j < n and lines[j].rstrip() not in ("  },", "  }"):
                j += 1
            block = lines[i:j + 1]
            word = None
            for ln in block:
                m = WORD_LINE.match(ln)
                if m:
                    word = m.group(1)
                    break
            if word and is_inappropriate(word, lang):
                removed.append(word)
                i = j + 1
                continue
            # keep block, but scrub a crude example
            for k, ln in enumerate(block):
                em = EX_LINE.match(ln.rstrip("\n").rstrip("\r"))
                if em and EXAMPLE_CRUDE_RE.search(em.group(2)):
                    eol = ln[len(ln.rstrip("\r\n")):]
                    block[k] = em.group(1) + FALLBACK[lang] + em.group(3).rstrip() + eol
                    scrubbed.append(word or "?")
            out.extend(block)
            i = j + 1
        else:
            out.append(lines[i])
            i += 1
    if apply and (removed or scrubbed):
        with open(path, "w", encoding="utf-8", newline="") as f:
            f.writelines(out)
    return removed, scrubbed


def process_syn(path, lang, apply):
    with open(path, "r", encoding="utf-8", newline="") as f:
        lines = f.readlines()
    out, removed, scrubbed = [], [], []
    wl = re.compile(r'word:\s*"((?:\\.|[^"\\])*)"')
    exl = re.compile(r'(example:\s*")((?:\\.|[^"\\])*)(")')
    for ln in lines:
        s = ln.strip()
        if s.startswith("{") and '"' in ln and "word:" in ln:
            m = wl.search(ln)
            if m and is_inappropriate(m.group(1), lang):
                removed.append(m.group(1))
                continue
            em = exl.search(ln)
            if em and EXAMPLE_CRUDE_RE.search(em.group(2)):
                ln = ln[:em.start()] + em.group(1) + SYN_FALLBACK[lang] + em.group(3) + ln[em.end():]
                scrubbed.append(m.group(1) if m else "?")
        out.append(ln)
    if apply and (removed or scrubbed):
        with open(path, "w", encoding="utf-8", newline="") as f:
            f.writelines(out)
    return removed, scrubbed


def main():
    apply = "--apply" in sys.argv
    print("MODE:", "APPLY (writing files)" if apply else "DRY RUN (report only)")
    print("=" * 60)
    tot_rm = tot_sc = 0
    for fn in STD_FILES:
        p = os.path.join(DATA, fn)
        if not os.path.exists(p):
            continue
        rm, sc = process_std(p, lang_of(fn), apply)
        if rm or sc:
            print(f"\n### {fn}  (remove {len(rm)}, scrub-example {len(sc)})")
            for w in rm:
                print(f"   REMOVE: {w}")
            for w in sc:
                print(f"   SCRUB-EX: {w}")
        tot_rm += len(rm)
        tot_sc += len(sc)
    for fn in SYN_FILES:
        p = os.path.join(DATA, fn)
        if not os.path.exists(p):
            continue
        rm, sc = process_syn(p, lang_of(fn), apply)
        if rm or sc:
            print(f"\n### {fn}  (remove {len(rm)}, scrub-example {len(sc)})")
            for w in rm:
                print(f"   REMOVE: {w}")
            for w in sc:
                print(f"   SCRUB-EX: {w}")
        tot_rm += len(rm)
        tot_sc += len(sc)
    print("\n" + "=" * 60)
    print(f"TOTAL remove: {tot_rm}   scrub-example: {tot_sc}")


if __name__ == "__main__":
    main()
