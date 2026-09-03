# -*- coding: utf-8 -*-
"""Repair fields that contain more than one " - " bilingual separator.

Every `definition` / `example` is NATIVE + " - " + TURKISH, and the app splits
on the FIRST " - ". A handful of entries contain a second one - usually a dash
inside the sentence itself:

    "'BUCK UP - the taxi's waiting.' - Çabuk ol, taksi bekliyor."

Splitting that on the first separator yields native "'BUCK UP" and Turkish
"the taxi's waiting.' - Çabuk ol, ...", i.e. English text shown as the Turkish
translation.

The fix is to keep exactly one " - " (the real separator) and rewrite the
others as " \u2014 " (em dash), which reads the same but cannot be mistaken for
the separator.

Finding the real separator is done by locating the first segment that contains
a character the NATIVE language cannot produce but Turkish can. The marker set
depends on the file, because German legitimately uses äöüß and French uses ç:

    English files   any non-ASCII letter at all
    German files    ı İ ş Ş ğ Ğ ç Ç
    other files     ı İ ş Ş ğ Ğ

Entries where no marker is found are NOT touched; they are listed so they can
be looked at by hand rather than guessed at.

Run: python scripts/fix_double_separator.py --apply
"""
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")

SEP = " - "
EM = " \u2014 "

TR_ONLY = re.compile(r"[\u0131\u0130\u015f\u015e\u011f\u011e]")
TR_OR_DE = re.compile(r"[\u0131\u0130\u015f\u015e\u011f\u011e\u00e7\u00c7]")
NON_ASCII = re.compile(r"[^\x00-\x7f]")

FIELD_RE = re.compile(r'((?:definition|example):\s*")((?:\\.|[^"\\])*)(")')


def marker_for(name):
    if name in ("wordsa1.js", "wordsa2.js", "wordsb1.js", "wordsb2.js",
                "wordsc1.js", "wordsc2.js", "toefl.js", "phrasalverbsen.js",
                "synanten.js"):
        return NON_ASCII
    if "gode" in name or name in ("partikelverbde.js", "synantde.js"):
        return TR_OR_DE
    return TR_ONLY


def repair(value, marker):
    """Return (new_value, ok). ok=False means the separator was not identified."""
    parts = value.split(SEP)
    if len(parts) < 3:
        return value, True
    # first segment that looks Turkish marks the start of the translation
    idx = next((i for i, p in enumerate(parts) if marker.search(p)), None)
    if idx is None or idx == 0:
        return value, False
    native = EM.join(parts[:idx])
    turkish = EM.join(parts[idx:])
    return native + SEP + turkish, True


def main():
    apply_changes = "--apply" in sys.argv
    fixed = skipped = 0
    unresolved = []

    for name in sorted(os.listdir(DATA)):
        if not name.endswith(".js"):
            continue
        path = os.path.join(DATA, name)
        text = io.open(path, encoding="utf-8").read()
        marker = marker_for(name)
        stats = {"n": 0}

        def sub(m):
            head, value, tail = m.groups()
            if value.count(SEP) < 2:
                return m.group(0)
            new, ok = repair(value, marker)
            if not ok:
                unresolved.append((name, value[:95]))
                return m.group(0)
            if new == value:
                return m.group(0)
            stats["n"] += 1
            return head + new + tail

        out = FIELD_RE.sub(sub, text)
        if stats["n"]:
            fixed += stats["n"]
            print("%-22s %d fixed" % (name, stats["n"]))
            if apply_changes:
                if out.count("{") != out.count("}"):
                    print("  ABORT %s: brace imbalance" % name)
                    return 1
                io.open(path, "w", encoding="utf-8", newline="\n").write(out)

    print("\n%d fields fixed%s" % (fixed, "" if apply_changes else "  (DRY RUN)"))
    if unresolved:
        print("\n%d NOT auto-fixable, review by hand:" % len(unresolved))
        for name, val in unresolved[:30]:
            print("  [%s] %s" % (name, val))
    return 0


if __name__ == "__main__":
    sys.exit(main())
