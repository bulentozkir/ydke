# -*- coding: utf-8 -*-
"""Find hyphen/separator problems in the vocabulary data.

The app splits `definition` and `example` on the literal separator " - " to
get the native half and the Turkish half. Anything that puts a second " - "
(or a stray "--") into a field breaks that split, so the card shows the wrong
text on one side.

Reports:
  DOUBLE_SEP   field contains " - " more than once
  DASH_DASH    field contains "--" or an en/em dash used as the separator
  HYPHEN_WORD  headword itself is hyphenated (informational: these are fine,
               but they are what makes a naive hyphen-split dangerous)
  LEAD_TRAIL   a half is empty after splitting

Run: python scripts/_diag_hyphens.py
"""
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")

FIELD_RE = {k: re.compile(r'%s:\s*"((?:\\.|[^"\\])*)"' % k)
            for k in ("word", "definition", "example")}
ENTRY_RE = re.compile(r"\{[^{}]*\}", re.S)

SEP = " - "
DASHDASH_RE = re.compile(r"--|\s[\u2013\u2014]\s")


def main():
    counts, samples = {}, {}
    total = 0
    for name in sorted(os.listdir(DATA)):
        if not name.endswith(".js"):
            continue
        text = io.open(os.path.join(DATA, name), encoding="utf-8").read()
        for block in ENTRY_RE.findall(text):
            get = lambda k: (FIELD_RE[k].search(block).group(1)
                             if FIELD_RE[k].search(block) else "")
            word, definition, example = get("word"), get("definition"), get("example")
            if not word:
                continue
            total += 1
            for label, value in (("definition", definition), ("example", example)):
                if not value:
                    continue
                if value.count(SEP) > 1:
                    tag = "DOUBLE_SEP"
                elif DASHDASH_RE.search(value):
                    tag = "DASH_DASH"
                elif SEP in value and not all(p.strip() for p in value.split(SEP, 1)):
                    tag = "LEAD_TRAIL"
                else:
                    continue
                key = (tag, name)
                counts[key] = counts.get(key, 0) + 1
                samples.setdefault(tag, []).append((name, word, label, value[:100]))
            if "-" in word:
                counts[("HYPHEN_WORD", name)] = counts.get(("HYPHEN_WORD", name), 0) + 1
                samples.setdefault("HYPHEN_WORD", []).append((name, word, "word", definition[:70]))

    print("entries scanned: %d\n" % total)
    by_tag = {}
    for (tag, name), n in counts.items():
        by_tag.setdefault(tag, {})[name] = n
    for tag in sorted(by_tag):
        tot = sum(by_tag[tag].values())
        print("%-12s %5d   %s" % (tag, tot, ", ".join(
            "%s=%d" % kv for kv in sorted(by_tag[tag].items(),
                                          key=lambda kv: -kv[1])[:8])))
    for tag in sorted(samples):
        if tag == "HYPHEN_WORD":
            continue
        print("\n===== %s (first 25) =====" % tag)
        for row in samples[tag][:25]:
            print("  [%s] %-22s %-10s %s" % row)
    return 0


if __name__ == "__main__":
    sys.exit(main())
