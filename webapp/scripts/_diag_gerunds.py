# -*- coding: utf-8 -*-
"""Find gerund / present-participle HEADWORDS in data/words*.js.

A headword should be the dictionary (base/lemma) form -- "abase", not
"abasing".  This scans every words*.js file, extracts the entries, and
flags headwords that look like a gerund / present participle in their
own language.

Usage:
    python scripts/_diag_gerunds.py            # summary + samples
    python scripts/_diag_gerunds.py --all      # dump every candidate
    python scripts/_diag_gerunds.py --lang en  # one language only
"""
import os
import re
import sys
import json
from collections import defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")

# ---------------------------------------------------------------- parsing
FIELD_RE = re.compile(
    r'(?P<key>\bword|\bpos|\blevel|\bcategory|\bdefinition|\bexample)\s*:\s*"(?P<val>(?:[^"\\]|\\.)*)"'
)
ENTRY_RE = re.compile(r"\{(?P<body>[^{}]*)\}", re.S)


def parse_file(path):
    with open(path, encoding="utf-8") as fh:
        text = fh.read()
    out = []
    for m in ENTRY_RE.finditer(text):
        body = m.group("body")
        if '"' not in body or "word" not in body:
            continue
        fields = {}
        for f in FIELD_RE.finditer(body):
            fields.setdefault(f.group("key"), f.group("val"))
        if "word" in fields:
            fields["_start"] = m.start()
            out.append(fields)
    return out


def lang_of(fname):
    stem = fname[:-3]  # strip .js
    for suf, lang in (("gode", "de"), ("fr", "fr"), ("it", "it"),
                      ("es", "es"), ("pt", "pt")):
        if stem.endswith(suf):
            return lang
    return "en"


# ------------------------------------------------------------- heuristics
EN_KEEP = set("""
morning evening building ceiling king ring thing spring string wing sting
during nothing something anything everything shopping clothing feeling
meeting painting drawing wedding pudding hearing sibling darling herring
lightning something siding railing lodging bearing offspring dumpling
stocking earring belongings surroundings winnings savings earnings
underlying outstanding demanding challenging misleading upbringing
undertaking wellbeing being human-being king thing sling swing wring
bring cling fling sing sting string thing wing zing ping ding ring
""".split())

# base-form candidates for an English -ing form
def en_bases(w):
    if not w.endswith("ing") or len(w) < 6:
        return []
    stem = w[:-3]
    cands = [stem, stem + "e"]
    # doubled final consonant: running -> run
    if len(stem) >= 3 and stem[-1] == stem[-2] and stem[-1] not in "aeiou":
        cands.append(stem[:-1])
    # -ying -> -ie (lying -> lie, dying -> die)
    if stem.endswith("y"):
        cands.append(stem[:-1] + "ie")
    return cands


def flag_en(word, pos, definition):
    w = word.lower().strip()
    if " " in w or "-" in w:
        return None
    if not w.endswith("ing") or len(w) < 6:
        return None
    if w in EN_KEEP:
        return None
    return en_bases(w)


FR_KEEP = set("""
""".split())


def flag_fr(word, pos, definition):
    w = re.sub(r"^(le|la|les|l')\s*", "", word.lower().strip())
    if " " in w or "-" in w:
        return None
    if not w.endswith("ant") and not w.endswith("ent"):
        return None
    if len(w) < 6:
        return None
    # only flag if pos looks verbal
    return [w[:-3] + "er", w[:-3] + "ir", w[:-3] + "re"]


def flag_de(word, pos, definition):
    w = re.sub(r"^(der|die|das)\s+", "", word.strip())
    if " " in w:
        return None
    if not w.endswith("end") or len(w) < 7:
        return None
    return [w[:-1]]  # laufend -> laufen


IT_G = re.compile(r"(ando|endo)$")
ES_G = re.compile(r"(ando|iendo|yendo)$")
PT_G = re.compile(r"(ando|endo|indo)$")


def flag_romance(rx):
    def inner(word, pos, definition):
        w = re.sub(r"^(il|lo|la|l'|i|gli|le|el|los|las|o|a|os|as|un|una)\s*", "",
                   word.lower().strip())
        if " " in w or len(w) < 6:
            return None
        return [w] if rx.search(w) else None
    return inner


FLAGGERS = {
    "en": flag_en,
    "fr": flag_fr,
    "de": flag_de,
    "it": flag_romance(IT_G),
    "es": flag_romance(ES_G),
    "pt": flag_romance(PT_G),
}


def main():
    only = None
    if "--lang" in sys.argv:
        only = sys.argv[sys.argv.index("--lang") + 1]
    dump_all = "--all" in sys.argv

    files = sorted(f for f in os.listdir(DATA)
                   if f.startswith("words") and f.endswith(".js"))
    if "--toefl" in sys.argv:
        files.append("toefl.js")

    all_words = defaultdict(set)   # lang -> set of normalized headwords
    entries = defaultdict(list)    # file -> entries
    for f in files:
        lang = lang_of(f)
        ents = parse_file(os.path.join(DATA, f))
        entries[f] = ents
        for e in ents:
            w = e["word"].lower().strip()
            w = re.sub(r"^(der|die|das|le|la|les|l'|il|lo|i|gli|el|los|las|o|a|os|as)\s*", "", w)
            all_words[lang].add(w)

    total = 0
    per_lang = defaultdict(list)
    for f in files:
        lang = lang_of(f)
        if only and lang != only:
            continue
        fl = FLAGGERS[lang]
        for e in entries[f]:
            bases = fl(e["word"], e.get("pos", ""), e.get("definition", ""))
            if not bases:
                continue
            hit = [b for b in bases if b in all_words[lang]]
            per_lang[lang].append({
                "file": f,
                "word": e["word"],
                "pos": e.get("pos", ""),
                "level": e.get("level", ""),
                "definition": e.get("definition", ""),
                "example": e.get("example", ""),
                "bases": bases,
                "base_exists": hit,
            })
            total += 1

    for lang in sorted(per_lang):
        rows = per_lang[lang]
        dup = [r for r in rows if r["base_exists"]]
        print(f"=== {lang.upper()}: {len(rows)} candidates "
              f"({len(dup)} whose base form already exists) ===")
        byfile = defaultdict(int)
        for r in rows:
            byfile[r["file"]] += 1
        for k in sorted(byfile):
            print(f"    {k:24s} {byfile[k]}")
        show = rows if dump_all else rows[:40]
        for r in show:
            mark = "DUP" if r["base_exists"] else "   "
            print(f"  {mark} {r['file']:22s} {r['word']:24s} [{r['pos']}] "
                  f"-> {r['bases']}")
        print()

    print(f"TOTAL candidates: {total}")

    if "--json" in sys.argv:
        out = os.path.join(ROOT, "scripts", "_gerund_candidates.json")
        with open(out, "w", encoding="utf-8") as fh:
            json.dump(per_lang, fh, ensure_ascii=False, indent=1)
        print("wrote", out)


if __name__ == "__main__":
    main()
