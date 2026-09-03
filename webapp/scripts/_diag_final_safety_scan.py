#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""_diag_final_safety_scan.py

FINAL broad content-safety re-scan across ALL 27 target data/*.js files'
ACTUAL WRITTEN CONTENT (not just fetch caches) -- per this project's
established lesson that keyword-based content filtering during harvesting
is reactive/incomplete-by-construction and needs at least one final
broad-net re-scan of the files themselves before trusting completeness.

Casts a wide net (word/definition/example text, case-insensitive) across
sexual/erotic/anatomical/crude-slang keyword families in English, German,
and French. Prints every match for manual triage (expect false positives
from legitimate biology/medical/clothing vocabulary -- do NOT auto-delete).

Usage: python scripts/_diag_final_safety_scan.py
"""
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

DATA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")

TARGET_FILES = [
    "wordsa1.js", "wordsa2.js", "wordsb1.js", "wordsb2.js", "wordsc1.js", "wordsc2.js",
    "wordsa1gode.js", "wordsa2gode.js", "wordsb1gode.js", "wordsb2gode.js", "wordsc1gode.js", "wordsc2gode.js",
    "wordsa1fr.js", "wordsa2fr.js", "wordsb1fr.js", "wordsb2fr.js", "wordsc1fr.js", "wordsc2fr.js",
    "phrasalverbsen.js", "toefl.js", "partikelverbde.js",
]

ENTRY_RE = re.compile(r'\{\s*word:\s*"((?:\\.|[^"])*)"\s*,\s*pos:\s*"((?:\\.|[^"])*)"[^{}]*?'
                      r'definition:\s*"((?:\\.|[^"])*)"[^{}]*?example:\s*"((?:\\.|[^"])*)"[^{}]*\}',
                      re.DOTALL)

# English + German + French keyword families, broad net (cast wide,
# triage manually -- see repo/session memory for the many false-positive
# patterns already identified in this project: "tit"=songbird, "poitrine"/
# breast=chest/food, vagabondage, penicillin, innate/congenital, robin
# redbreast, denuded=stripped-bare, cornichon/congénital substrings, etc.)
BROAD_PAT = re.compile(
    r"sexual|erotic|orgasm|penis|vagina|genital|masturbat|fetish|kinky|"
    r"prostitut|brothel|porn|stripper|bondage|breast|nude|naked|intercourse|"
    r"dildo|fellat|cunnilingus|clitoris|foreskin|circumcis|vulva|smegma|"
    r"fuck|shit\b|cunt|nigger|faggot|whore|molest|rape|pedophil|paedophil|"
    r"incest|bestiality|ejaculat|coitus|sodom|jack off|jerk off|"
    r"scheiss|scheiß|arschloch|fotze|wichs|hure\b|nutte|ficken|muschi|"
    r"vit\b|zizi|zob\b|nichon|foufoune|déconner|mouille|courtisane|pétasse|"
    r"masturber|fellation|coït\b|vibromasseur|inceste|circoncision|"
    r"clitoris|vaginal|homosexuel|hétéro\b|transsexuel|lesbienne",
    re.IGNORECASE,
)


def main():
    total_flagged = 0
    for fn in TARGET_FILES:
        path = os.path.join(DATA, fn)
        if not os.path.exists(path):
            continue
        text = open(path, encoding="utf-8").read()
        flagged = []
        for m in ENTRY_RE.finditer(text):
            word, pos, definition, example = m.groups()
            if BROAD_PAT.search(word) or BROAD_PAT.search(definition) or BROAD_PAT.search(example):
                flagged.append((word, definition[:90], example[:70]))
        if flagged:
            print(f"=== {fn}: {len(flagged)} flagged ===")
            for w, d, e in flagged:
                print(f"  {w!r} -> {d} || ex: {e}")
            total_flagged += len(flagged)
    print(f"\nTOTAL flagged across all files: {total_flagged}")


if __name__ == "__main__":
    main()
