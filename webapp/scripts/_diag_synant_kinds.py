"""Breakdown of form-of definition kinds in synantde.js / toefl.js /
partikelverbde.js, plus lemma-existence check."""
import collections
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _diag_formof_other as d  # noqa: E402

KINDS = [
    ("gerundium", "Gerundium von"),
    ("part-perfekt", "Partizip Perfekt von"),
    ("part-perfekt2", "Perfektes Partizip von"),
    ("part-praesens", "Partizip Präsens von"),
    ("part-other", "Partizip"),
    ("plural", "Plural von"),
    ("singular", "Singular von"),
    ("komparativ", "Komparativ von"),
    ("superlativ", "Superlativ von"),
    ("praeteritum", "Präteritum von"),
    ("flexion", "Flexion von"),
    ("grundform", "Grundform von"),
    ("infinitiv", "Infinitiv von"),
]


def kind_of(text):
    for name, needle in KINDS:
        if text.lower().startswith(needle.lower()):
            return name
    for name, needle in KINDS:
        if needle.lower() in text.lower():
            return name + "*"
    return "other"


def main():
    path = os.path.join(d.DATA, "synantde.js")
    _, entries = d.parse(path)
    byword = {e["word"]: e for e in entries}
    fold = collections.defaultdict(list)
    for e in entries:
        fold[e["word"].casefold()].append(e["word"])

    counts = collections.Counter()
    lemma_present = collections.Counter()
    rows = []
    for e in entries:
        native = (e.get("definition") or "").split(";")[0]
        if not d.DE_FORM_OF.search(native):
            continue
        k = kind_of(native)
        counts[k] += 1
        rows.append((k, e["word"], native, e.get("level", "")))
        # lemma after "von "
        m = native.split(" von ", 1)
        lem = m[1].strip(" :,.\u201e\u201c") if len(m) > 1 else ""
        lem = lem.split(" ")[0].strip("\u201e\u201c(),.:")
        hit = lem in byword or lem.casefold() in fold
        lemma_present[(k, hit)] += 1
    print("KIND COUNTS")
    for k, v in sorted(counts.items()):
        print(f"  {k:16} {v}")
    print("TOTAL", sum(counts.values()))
    print()
    print("LEMMA PRESENT IN FILE?")
    for (k, hit), v in sorted(lemma_present.items()):
        print(f"  {k:16} lemma_in_file={hit!s:5} {v}")
    print()
    print("SAMPLE 'other':")
    n = 0
    for k, w, native, lvl in rows:
        if k == "other" or k.endswith("*"):
            print(f"  [{k}] {w:24} {native[:80]}")
            n += 1
            if n > 40:
                break


if __name__ == "__main__":
    main()
