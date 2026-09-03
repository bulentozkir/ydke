"""Categorize inflected-form headwords ('X participle of Y', 'gerund of Y', 'plural of Y')
and report whether the referenced lemma already exists in the same language's corpus.
"""
import collections
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _diag_gerunds as g  # noqa: E402
from _diag_gerund_verbs import strip_article  # noqa: E402

CATS = [
    ("gerund", re.compile(r"\bgerund of\s+([^\s;:,.()]+)", re.I)),
    ("present-participle", re.compile(r"\bpresent participle of\s+([^\s;:,.()]+)", re.I)),
    ("perfect-participle", re.compile(r"\b(?:perfect|past) participle of\s+([^\s;:,.()]+)", re.I)),
    ("plural", re.compile(r"\bplural of\s+([^\s;:,.()]+)", re.I)),
    ("singular", re.compile(r"\bsingular of\s+([^\s;:,.()]+)", re.I)),
    ("comparative", re.compile(r"\bcomparative of\s+([^\s;:,.()'`]+)", re.I)),
    ("superlative", re.compile(r"\bsuperlative of\s+([^\s;:,.()'`]+)", re.I)),
    ("inflection", re.compile(r"\binflection of\s+([^\s;:,.()]+)", re.I)),
    ("bare-participle", re.compile(r"^\(?(?:perfect|past|present) participle\)?\.?$", re.I)),
]


def main():
    files = sorted(f for f in os.listdir(g.DATA) if f.endswith(".js"))
    parsed = {f: g.parse_file(os.path.join(g.DATA, f)) for f in files}

    lang_words = collections.defaultdict(set)
    for f, entries in parsed.items():
        if not f.startswith("words"):
            continue
        lang = g.lang_of(f)
        for e in entries:
            lang_words[lang].add(strip_article(e["word"]))

    per_file = collections.defaultdict(collections.Counter)
    lemma_state = collections.Counter()
    samples = collections.defaultdict(list)

    for f, entries in parsed.items():
        if not f.startswith("words"):
            continue
        lang = g.lang_of(f)
        for e in entries:
            eng = (e.get("definition") or "").split(" - ")[0].strip()
            for name, rx in CATS:
                m = rx.search(eng)
                if not m:
                    continue
                per_file[f][name] += 1
                lemma = m.group(1).lower().rstrip(".:;,") if m.groups() else ""
                exists = lemma in lang_words[lang] if lemma else None
                lemma_state[(lang, name, exists)] += 1
                if len(samples[(lang, name, exists)]) < 6:
                    samples[(lang, name, exists)].append(f"{e['word']}->{lemma or '?'}")
                break

    print("### counts per file")
    for f in sorted(per_file):
        tot = sum(per_file[f].values())
        print(f"  {f[:-3]:16} total={tot:5}  {dict(per_file[f])}")

    print()
    print("### lang / category / lemma-already-in-corpus")
    for (lang, name, exists), n in sorted(lemma_state.items(), key=lambda x: (x[0][0], x[0][1], str(x[0][2]))):
        lbl = {True: "LEMMA EXISTS", False: "lemma missing", None: "n/a"}[exists]
        print(f"  {lang} {name:20} {lbl:14} {n:5}   e.g. {samples[(lang, name, exists)][:4]}")

    print()
    print("GRAND TOTAL:", sum(sum(c.values()) for c in per_file.values()))


if __name__ == "__main__":
    main()
