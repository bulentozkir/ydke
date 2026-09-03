# -*- coding: utf-8 -*-
"""Merge duplicate headwords in data/phrasalverbsen.js.

The harvest appended one row per SENSE, so "get up", "break down" and friends
appear several times with different definitions. Duplicate headwords are a
real defect for this app: the flashcard, quiz and matching modes key on the
headword, so a learner is shown what looks like the same card twice with two
different "correct" answers.

Merging, not deleting, is the right fix - the extra senses are genuine
content. For each headword the first row is kept and the additional senses
are folded into its definition as "sense 1; sense 2", capped so a card stays
readable. Rows whose definition is already contained in the kept one, or
which carry the "no definition available" fallback, are simply dropped.

Run: python scripts/fix_phrasalverb_dupes.py --apply
"""
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATH = os.path.join(ROOT, "data", "phrasalverbsen.js")

ENTRY_RE = re.compile(r'\n  \{\s*\n?(?:[^{}]|\{[^{}]*\})*?\n?  \},', re.S)
FIELD_RE = {k: re.compile(r'%s:\s*"((?:\\.|[^"\\])*)"' % k)
            for k in ("word", "pos", "level", "category", "definition", "example")}
FALLBACK_RE = re.compile(
    r"No (example sentence|dictionary definition) available|Bu kelime için", re.I)

MAX_SENSES = 3


def field(block, key):
    m = FIELD_RE[key].search(block)
    return m.group(1) if m else ""


def split_bilingual(text):
    if " - " in text:
        a, b = text.split(" - ", 1)
        return a.strip(), b.strip()
    return text.strip(), ""


def main():
    apply_changes = "--apply" in sys.argv
    text = io.open(PATH, encoding="utf-8").read()
    blocks = ENTRY_RE.findall(text)

    order, groups = [], {}
    for b in blocks:
        w = field(b, "word").strip().lower()
        if w not in groups:
            groups[w] = []
            order.append(w)
        groups[w].append(b)

    merged, dropped, folded = [], 0, 0
    for w in order:
        rows = groups[w]
        keep = rows[0]
        if len(rows) == 1:
            merged.append(keep)
            continue

        en, tr = split_bilingual(field(keep, "definition"))
        senses_en = [en] if en and not FALLBACK_RE.search(en) else []
        senses_tr = [tr] if tr and not FALLBACK_RE.search(tr) else []

        for extra in rows[1:]:
            e2, t2 = split_bilingual(field(extra, "definition"))
            if not e2 or FALLBACK_RE.search(e2):
                dropped += 1
                continue
            if any(e2.lower() == s.lower() for s in senses_en):
                dropped += 1
                continue
            if len(senses_en) >= MAX_SENSES:
                dropped += 1
                continue
            senses_en.append(e2)
            if t2 and not FALLBACK_RE.search(t2):
                senses_tr.append(t2)
            folded += 1

        if len(senses_en) > 1:
            def join(parts):
                out = []
                for p in parts:
                    p = p.strip().rstrip(".").strip()
                    if p:
                        out.append(p)
                return "; ".join(out) + "."
            new_def = join(senses_en)
            if senses_tr:
                new_def += " - " + join(senses_tr)
            keep = FIELD_RE["definition"].sub(
                lambda _m: 'definition: "%s"' % new_def.replace('"', "'"), keep, count=1)
        merged.append(keep)

    print("rows %d -> %d   (%d senses folded in, %d redundant rows dropped)"
          % (len(blocks), len(merged), folded, dropped))

    if not apply_changes:
        print("DRY RUN - pass --apply to write")
        return 0

    head = text[: text.index(blocks[0])]
    tail = text[text.rindex(blocks[-1]) + len(blocks[-1]):]
    out = head + "".join(merged) + tail
    if out.count("{") != out.count("}"):
        print("ABORT: brace imbalance")
        return 1
    io.open(PATH, "w", encoding="utf-8", newline="\n").write(out)
    print("written")
    return 0


if __name__ == "__main__":
    sys.exit(main())
