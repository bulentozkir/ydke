#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""_diag_headword_scan.py

High-precision scan: flags entries whose HEADWORD ITSELF (not just
definition/example text) matches an explicit-content/slur pattern -- much
lower false-positive rate than the broad definition-text scan, since it
doesn't get tripped up by legitimate biology/medical/clothing words that
merely MENTION a keyword in their definition.
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

ENTRY_RE = re.compile(r'\{\s*word:\s*"((?:\\.|[^"])*)"[^{}]*?definition:\s*"((?:\\.|[^"])*)"[^{}]*\}', re.DOTALL)

# Headword-only, high-precision explicit-content/slur list (English +
# German + French bare forms, casefold-normalized comparison).
HEADWORD_BAD = {
    "fuck", "fucking", "fucked", "fucker", "shit", "shitty", "bitch", "bastard",
    "asshole", "arsehole", "cunt", "dick", "cock", "pussy", "prick", "wanker",
    "whore", "slut", "bollocks", "bugger", "twat", "nigger", "nigga",
    "faggot", "fag", "spic", "chink", "kike", "wop", "coon", "dyke", "tranny",
    "retard", "retarded", "fellatio", "fellate", "cunnilingus", "dildo",
    "vibrator", "clitoris", "clitoral", "foreskin", "circumcision",
    "circumcised", "labia", "vulva", "smegma", "buttplug", "strapon",
    "blowjob", "handjob", "ballsack", "dominatrix", "gigolo", "voyeurism",
    "exhibitionist", "hooker", "bondage", "sadomasochism", "sadomasochist",
    "pecker", "cuckold", "pubes", "molester", "succubus", "nymphomaniac",
    "nympho", "midget", "rape", "raping", "raped", "rapist", "glans",
    "jack off", "jerk off", "wank off", "beat off",
    "scheisse", "scheiße", "arschloch", "fotze", "wichser", "hure", "nutte",
    "schwanz", "ficken", "muschi", "titten", "hurensohn", "schlampe",
    "bumsen", "wichsen", "klitoris", "vorhaut", "beschneidung",
    "beschnitten", "vulva", "schamlippen", "versaut",
    "merde", "putain", "connard", "connasse", "salope", "encule", "enculer",
    "bite", "couille", "vaginal", "clitoris", "godemichet", "gode",
    "fellation", "cunnilingus", "masturbation", "masturber", "prostituée",
    "prostitué", "prostitution", "bordel", "shit", "pornographe", "molester",
    "vagin", "penis", "pénis", "porno", "pornographie", "pornographique",
}


def strip_prefix(w):
    return re.sub(r"^(der|die|das|le|la|l\u2019|l'|un|une)\s+", "", w, flags=re.IGNORECASE)


def main():
    total = 0
    for fn in TARGET_FILES:
        path = os.path.join(DATA, fn)
        if not os.path.exists(path):
            continue
        text = open(path, encoding="utf-8").read()
        flagged = []
        for m in ENTRY_RE.finditer(text):
            word, definition = m.groups()
            bare = strip_prefix(word).casefold()
            if bare in HEADWORD_BAD:
                flagged.append((word, definition[:80]))
        if flagged:
            print(f"=== {fn}: {len(flagged)} flagged headwords ===")
            for w, d in flagged:
                print(f"  {w!r} -> {d}")
            total += len(flagged)
    print(f"\nTOTAL flagged headwords: {total}")


if __name__ == "__main__":
    main()
