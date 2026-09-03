"""Second-pass triage for gerund/participle headwords.

1. Headwords ending in a gerund/participle suffix whose ENGLISH definition half
   starts with a verb-infinitive marker ("To ...") but whose pos is NOT verb.
2. For the confirmed English verb gerunds, report whether the lemma already
   exists anywhere in the English corpus (would create a duplicate on rename).
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _diag_gerunds as g  # noqa: E402
from _diag_gerund_verbs import PATS, strip_article  # noqa: E402

VERB_POS = {"verb", "verbe", "v", "v.", "verbo"}
TO_RE = re.compile(r"^\(?[^)]{0,40}\)?\s*[Tt]o\s+\w")


def load():
    files = sorted(f for f in os.listdir(g.DATA) if f.startswith("words") and f.endswith(".js"))
    parsed = {}
    for f in files:
        parsed[f] = g.parse_file(os.path.join(g.DATA, f))
    return parsed


def main():
    parsed = load()
    by_lang_words = {}
    for f, entries in parsed.items():
        lang = g.lang_of(f)
        by_lang_words.setdefault(lang, {})
        for e in entries:
            by_lang_words[lang].setdefault(strip_article(e["word"]), []).append(f)

    print("### 1. participle headword + verb-style definition but non-verb pos")
    n = 0
    for f, entries in parsed.items():
        lang = g.lang_of(f)
        for e in entries:
            w = strip_article(e["word"])
            pos = (e.get("pos") or "").strip().lower()
            if " " in w or "-" in w or len(w) < 6:
                continue
            if not PATS[lang].search(w):
                continue
            if pos in VERB_POS:
                continue
            eng = (e.get("definition") or "").split(" - ")[0]
            if TO_RE.match(eng):
                print(f"  {lang} {f[5:-3]:8} {e['word']:22} [{pos}] | {eng[:70]}")
                n += 1
    print(f"  total: {n}")

    print()
    print("### 2. English verb gerunds - does the lemma already exist?")
    en_words = by_lang_words["en"]
    dup, free = [], []
    for f, entries in sorted(parsed.items()):
        if g.lang_of(f) != "en":
            continue
        for e in entries:
            w = strip_article(e["word"])
            pos = (e.get("pos") or "").strip().lower()
            if pos not in VERB_POS or not w.endswith("ing") or " " in w or len(w) < 6:
                continue
            bases = g.en_bases(w)
            found = [(b, en_words[b]) for b in bases if b in en_words]
            if found:
                dup.append((f, w, found))
            else:
                free.append((f, w, bases))
    print(f"  lemma ALREADY EXISTS (must delete gerund, not rename): {len(dup)}")
    for f, w, found in dup:
        print(f"    {f[5:-3]:4} {w:20} -> {found}")
    print(f"  lemma free (safe to rename): {len(free)}")
    for f, w, bases in free:
        print(f"    {f[5:-3]:4} {w:20} -> {bases}")


if __name__ == "__main__":
    main()
