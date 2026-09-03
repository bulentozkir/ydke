"""Plan + apply the form-of definition fix for data/synantde.js.

data/synantde.js is auto-generated (scripts/translate_and_build_synant.py)
and then post-processed (scripts/classify_word_categories.py adds `category`).
This script is a further POST-PROCESSING pass in that same chain: it replaces
Wiktionary 'form-of' stub definitions ("Partizip Perfekt von X",
"Gerundium von X", "Plural von X", "Flexion von X") with real curated
definitions, and removes/renames entries that are pure inflected forms.

Curated content lives in scripts/_synant_formof_content.py so this pass is
idempotent and can be re-run after any regeneration of synantde.js.

Usage:
    python scripts/fix_synant_formof.py --plan       # rebuild the plan JSON
    python scripts/fix_synant_formof.py --report     # human-readable summary
    python scripts/fix_synant_formof.py --worksheet  # authoring worksheet
    python scripts/fix_synant_formof.py --apply      # delete pure inflections
    python scripts/fix_synant_formof.py --content    # apply curated defs
"""
import argparse
import collections
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")
TARGET = os.path.join(DATA, "synantde.js")
PLAN = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_synant_plan.json")

FIELD_RE = re.compile(
    r'(?P<key>\bword|\blevel|\bcategory|\bdefinition|\bexample|'
    r'\bsynonyms|\bantonyms)\s*:\s*"(?P<val>(?:[^"\\]|\\.)*)"'
)
ENTRY_RE = re.compile(r"\{(?P<body>[^{}]*)\}", re.S)

KINDS = [
    ("gerundium", re.compile(r"^Gerundium von\b", re.I)),
    ("participle", re.compile(r"^(Partizip (Perfekt|Pr\u00e4sens)|Perfektes Partizip) von\b", re.I)),
    ("plural", re.compile(r"\bPlural von\b", re.I)),
    ("singular", re.compile(r"\bSingular von\b", re.I)),
    ("flexion", re.compile(r"^Flexion von\b", re.I)),
    ("komparativ", re.compile(r"\bKomparativ von\b", re.I)),
    ("superlativ", re.compile(r"\bSuperlativ von\b", re.I)),
    ("praeteritum", re.compile(r"\bPr\u00e4teritum von\b", re.I)),
]

# Definitions that merely LOOK like form-of stubs but are real definitions.
SKIP_WORDS = {"wobei"}
# The upstream Wiktionary glosses were machine-translated, which mangled a few
# lemma names ('plural of Brief' -> 'Plural von Kurze', 'plural of kind' ->
# 'Plural von Art'). Correct them by hand.
LEMMA_OVERRIDE = {
    "Briefe": "Brief",
    "Kinder": "Kind",
    "Güter": "Gut",
    "letzter": "letzt",
    "diverse": "divers",
    "alternative": "alternativ",
    "innere": "inner",
}

# Inflected-looking words that are established German headwords in their own
# right (pronouns, determiners, a verb mis-glossed as an adjective form) --
# keep the word, only replace the stub definition.
FORCE_REDEFINE = {"alle", "andere", "einige", "das", "diese", "schnellen"}
# Lexicalised German plurals that are established headwords in their own
# right -- keep the word, only replace the stub definition.
LEXICALISED_PLURALS = {
    "Daten", "Leute", "Gefolgsleute", "Eltern", "Ferien", "Kosten",
    "Finanzen", "Alpen", "Gebr\u00fcder", "Geschwister", "M\u00f6bel",
    "Lebensmittel", "Personalien", "Spesen", "Trikot",
}


def parse(path=TARGET):
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


def native_def(entry):
    return (entry.get("definition") or "").split(";")[0].strip()


def kind_of(text):
    for name, pat in KINDS:
        if pat.search(text):
            return name
    return None


LEMMA_RE = re.compile(r"\bvon\s+(?P<lemma>[^\s,:;.\u201e\u201c(]+)")


def lemma_of(text):
    m = LEMMA_RE.search(text)
    if not m:
        return ""
    return m.group("lemma").strip("\u201e\u201c\"'(),.:;")


def build_plan():
    text, entries = parse()
    by_exact = {e["word"] for e in entries}

    rows = []
    for e in entries:
        w = e["word"]
        if w in SKIP_WORDS:
            continue
        nd = native_def(e)
        kind = kind_of(nd)
        if not kind:
            continue
        lemma = LEMMA_OVERRIDE.get(w) or lemma_of(nd)

        if kind in ("participle", "gerundium"):
            # German participial adjectives and nominalised infinitives are
            # legitimate headwords -- keep the word, fix the definition.
            action, target = "redefine", w
        elif w in FORCE_REDEFINE or w in LEXICALISED_PLURALS:
            action, target = "redefine", w
        elif lemma and lemma in by_exact:
            # pure inflected form whose base word is already an entry.
            # NOTE: exact case, not casefold -- capitalisation is semantic in
            # German (das Gut != gut, das Alt != alt).
            action, target = "delete", lemma
        else:
            action, target = "rewrite", lemma or w
        rows.append(
            {
                "word": w,
                "kind": kind,
                "level": e.get("level", ""),
                "category": e.get("category", ""),
                "definition": e.get("definition", ""),
                "example": e.get("example", ""),
                "synonyms": e.get("synonyms", ""),
                "antonyms": e.get("antonyms", ""),
                "lemma": lemma,
                "action": action,
                "target": target,
                "start": e["_start"],
                "end": e["_end"],
            }
        )
    rows.sort(key=lambda r: r["start"])
    with open(PLAN, "w", encoding="utf-8") as fh:
        json.dump(rows, fh, ensure_ascii=False, indent=1)
    return rows


def load_plan():
    with open(PLAN, encoding="utf-8") as fh:
        return json.load(fh)


def cmd_report():
    rows = load_plan()
    c = collections.Counter((r["kind"], r["action"]) for r in rows)
    for (k, a), n in sorted(c.items()):
        print(f"  {k:12} {a:9} {n}")
    print("TOTAL", len(rows))
    print()
    for a in ("delete", "rewrite"):
        print(f"--- {a.upper()}")
        for r in rows:
            if r["action"] == a:
                print(f"  {r['word']:28} -> {r['target']:24} [{r['kind']}] {native_def(r)[:52]}")


def cmd_worksheet():
    rows = load_plan()
    out = []
    for r in rows:
        if r["action"] not in ("redefine", "rewrite"):
            continue
        d = r["definition"].split(";")
        out.append(
            f'{r["word"]}\t{r["target"]}\t{r["kind"][:4]}\t{r["lemma"]}'
            f'\t{d[0][:44]}\t{r["synonyms"][:64]}'
        )
    path = os.path.join(os.path.dirname(PLAN), "_synant_worksheet.txt")
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(out) + "\n")
    print(f"{len(out)} rows -> {path}")


def js_escape(s):
    return s.replace("\\", "\\\\").replace('"', '\\"')


def cmd_apply():
    """Remove the pure-inflection entries (action == delete)."""
    text, _ = parse()
    rows = [r for r in load_plan() if r["action"] == "delete"]
    for r in rows:
        seg = text[r["start"]: r["end"]]
        if f'word:"{r["word"]}"' not in seg:
            raise SystemExit(f"OFFSET MISMATCH for {r['word']!r}")
    removed = 0
    for r in sorted(rows, key=lambda x: -x["start"]):
        start, end = r["start"], r["end"]
        # entries are one-per-line: swallow the trailing comma + newline
        while end < len(text) and text[end] in ",":
            end += 1
        while end < len(text) and text[end] == "\n":
            end += 1
            break
        text = text[:start] + text[end:]
        removed += 1
    with open(TARGET, "w", encoding="utf-8", newline="") as fh:
        fh.write(text)
    print(f"deleted {removed} entries from synantde.js")


def cmd_content():
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from _synant_formof_content import CONTENT  # noqa: E402

    text, _ = parse()
    rows = [r for r in load_plan() if r["action"] in ("redefine", "rewrite")]
    missing = [r["word"] for r in rows if r["word"] not in CONTENT]
    if missing:
        raise SystemExit(f"MISSING CONTENT for {len(missing)}: {missing[:20]}")

    for r in rows:
        seg = text[r["start"]: r["end"]]
        if f'word:"{r["word"]}"' not in seg:
            raise SystemExit(f"OFFSET MISMATCH for {r['word']!r}")

    n_re = n_rw = 0
    for r in sorted(rows, key=lambda x: -x["start"]):
        new_word, def_native, def_tr, ex_native, ex_tr = CONTENT[r["word"]]
        new_word = new_word or r["word"]
        block = text[r["start"]: r["end"]]

        # Only fields that actually CHANGE are rewritten. Values read back out
        # of the file are still JS-escaped, so feeding them through js_escape()
        # again would double the backslashes (\" -> \\\").
        repl = {"definition": f"{def_native};{def_tr}"}
        if new_word != r["word"]:
            repl["word"] = new_word
        if ex_native:
            repl["example"] = f"{ex_native};{ex_tr}"
        seen = set()

        def sub(m):
            k = m.group("key")
            if k in repl and k not in seen:
                seen.add(k)
                return f'{k}:"{js_escape(repl[k])}"'
            return m.group(0)

        newblock = FIELD_RE.sub(sub, block)
        for k in repl:
            if k not in seen:
                raise SystemExit(f"FIELD {k} NOT FOUND in entry {r['word']!r}")
        text = text[:r["start"]] + newblock + text[r["end"]:]
        if new_word != r["word"]:
            n_rw += 1
        else:
            n_re += 1
    with open(TARGET, "w", encoding="utf-8", newline="") as fh:
        fh.write(text)
    print(f"synantde.js: {n_rw} rewritten, {n_re} redefined")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plan", action="store_true")
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--worksheet", action="store_true")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--content", action="store_true")
    a = ap.parse_args()
    if a.plan:
        rows = build_plan()
        print(f"plan: {len(rows)} rows -> {PLAN}")
    if a.report:
        cmd_report()
    if a.worksheet:
        cmd_worksheet()
    if a.apply:
        cmd_apply()
    if a.content:
        cmd_content()


if __name__ == "__main__":
    main()
