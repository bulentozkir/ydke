"""Report headwords that look like gerunds / present participles AND are tagged as verbs.

Usage:
    python scripts/_diag_gerund_verbs.py            # all languages
    python scripts/_diag_gerund_verbs.py en         # one language
"""
import collections
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _diag_gerunds as g  # noqa: E402

ARTICLE_RE = re.compile(r"^(der|die|das|le|la|les|l'|il|lo|i|gli|el|los|las|o|a|os|as)\s+", re.I)

PATS = {
    "en": re.compile(r"ing$"),
    "de": re.compile(r"end$"),
    "fr": re.compile(r"ant$"),
    "it": re.compile(r"(ando|endo)$"),
    "es": re.compile(r"(ando|iendo|yendo)$"),
    "pt": re.compile(r"(ando|endo|indo)$"),
}

VERB_POS = {"verb", "verbe", "v", "v.", "verbo"}


def strip_article(word):
    w = word.strip()
    w = ARTICLE_RE.sub("", w)
    w = re.sub(r"^l['\u2019]\s*", "", w, flags=re.I)
    return w.lower()


def main():
    want = sys.argv[1] if len(sys.argv) > 1 else None
    pos_counter = collections.Counter()
    hits = collections.defaultdict(list)
    all_words = collections.defaultdict(set)

    files = sorted(f for f in os.listdir(g.DATA) if f.startswith("words") and f.endswith(".js"))
    parsed = {}
    for f in files:
        entries = g.parse_file(os.path.join(g.DATA, f))
        parsed[f] = entries
        lang = g.lang_of(f)
        for e in entries:
            all_words[lang].add(strip_article(e["word"]))

    for f in files:
        lang = g.lang_of(f)
        if want and lang != want:
            continue
        for e in parsed[f]:
            w = strip_article(e["word"])
            pos = (e.get("pos") or "").strip()
            pos_counter[(lang, pos)] += 1
            if " " in w or "-" in w:
                continue
            if len(w) < 6:
                continue
            if PATS[lang].search(w) and pos.lower() in VERB_POS:
                hits[lang].append((f, e["word"], pos, (e.get("definition") or "")[:70]))

    for lang in sorted(hits):
        print("=" * 70)
        print(f"{lang.upper()}  verb-tagged participle/gerund headwords: {len(hits[lang])}")
        print("=" * 70)
        for f, w, pos, d in hits[lang]:
            print(f"  {f[5:-3]:8} {w:22} | {d}")

    print()
    print("--- pos values per language ---")
    per = collections.defaultdict(collections.Counter)
    for (lang, pos), n in pos_counter.items():
        per[lang][pos] += n
    for lang in sorted(per):
        print(lang, per[lang].most_common(12))


if __name__ == "__main__":
    main()
