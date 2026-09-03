"""Scan the three NON-words*.js data files (synantde.js, toefl.js,
partikelverbde.js) for 'form-of' definitions -- inflected forms whose
definition is just a grammar stub instead of a real meaning.

synantde.js is German-language and compact one-object-per-line, with ';'
(not ' - ') as the bilingual separator, so it needs its own patterns.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")

FIELD_RE = re.compile(
    r'(?P<key>\bword|\bpos|\blevel|\bcategory|\bdefinition|\bexample|'
    r'\bsynonyms|\bantonyms)\s*:\s*"(?P<val>(?:[^"\\]|\\.)*)"'
)
ENTRY_RE = re.compile(r"\{(?P<body>[^{}]*)\}", re.S)

# English form-of patterns (toefl.js, partikelverbde.js)
EN_FORM_OF = re.compile(
    r"(present participle|past participle|perfect participle|gerund of|"
    r"inflection of|plural of|singular of|comparative of|superlative of|"
    r"simple past|third-person singular|-ing form|participle of|"
    r"^\(?(?:perfect|past|present) participle\)?\.?$)",
    re.I,
)

# German form-of patterns (synantde.js)
DE_FORM_OF = re.compile(
    r"(Gerundium von|Partizip\b|Plural von|Singular von|Komparativ von|"
    r"Superlativ von|Präteritum von|Flexion von|Grundform von|"
    r"substantivierter Infinitiv|Infinitiv von)",
    re.I,
)


def parse(path):
    with open(path, encoding="utf-8", newline="") as fh:
        text = fh.read()
    out = []
    for m in ENTRY_RE.finditer(text):
        rec = {"_start": m.start(), "_end": m.end()}
        for f in FIELD_RE.finditer(m.group("body")):
            rec[f.group("key")] = f.group("val")
        if "word" in rec:
            out.append(rec)
    return text, out


def main():
    targets = [
        ("synantde.js", DE_FORM_OF, ";"),
        ("toefl.js", EN_FORM_OF, " - "),
        ("partikelverbde.js", EN_FORM_OF, " - "),
    ]
    grand = 0
    for fname, pat, sep in targets:
        path = os.path.join(DATA, fname)
        _, entries = parse(path)
        words = {e["word"] for e in entries}
        fold = {}
        for e in entries:
            fold.setdefault(e["word"].casefold(), []).append(e["word"])
        hits = []
        for e in entries:
            d = e.get("definition") or ""
            native = d.split(sep)[0]
            if pat.search(native):
                hits.append((e, native))
        print(f"===== {fname}: {len(entries)} entries, {len(hits)} form-of")
        for e, native in hits:
            w = e["word"]
            # does a differently-cased twin exist?
            twins = [x for x in fold.get(w.casefold(), []) if x != w]
            tw = f"  <TWIN {twins}>" if twins else ""
            print(f"  {w:26} [{e.get('level','')}] {native[:64]}{tw}")
        grand += len(hits)
        # casefold duplicate check for the whole file
        dupes = {k: v for k, v in fold.items() if len(v) > 1}
        if dupes:
            print(f"  -- casefold duplicates in {fname}: {dupes}")
        print(f"  -- total words: {len(words)}")
    print(f"GRAND TOTAL form-of: {grand}")


if __name__ == "__main__":
    main()
