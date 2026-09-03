#!/usr/bin/env python3
"""Generator for data/synanten.js and data/synantde.js.

Reads the two external source files (already downloaded to %TEMP%) and
mechanically transforms them into the Top Words word-record schema:
    { word, level, definition, example, synonyms, antonyms }

- English source: syn-ant.csv  -> word,syn1#syn2#...,ant1#ant2#...
- German source:  german.syn   -> "word syn1 syn2 syn3" per line (space-delimited)
  German antonyms are NOT in the source; a small curated dictionary of common
  antonym pairs is applied on top (best-effort, not exhaustive).

If %TEMP%/en_defs_cache.json and/or %TEMP%/de_defs_cache.json exist, their
definition/example values are merged by word into generated records. Missing
cache entries (or records with empty values) safely remain empty.
"""
import os
import re
import tempfile
import json

TEMP = tempfile.gettempdir()
EN_SRC = os.path.join(TEMP, "syn-ant.csv")
DE_SRC = os.path.join(TEMP, "german.syn")
EN_OUT = r"c:\gitrepo\udsp\data\synanten.js"
DE_OUT = r"c:\gitrepo\udsp\data\synantde.js"
EN_CACHE = os.path.join(TEMP, "en_defs_cache.json")
DE_CACHE = os.path.join(TEMP, "de_defs_cache.json")

JUNK_TOKEN_RE = re.compile(r"^\\[a-zA-Z#]?$")  # artifacts like \n \r \v \# \  seen in the EN source

# Reject non-lexical headwords that sometimes slip into the raw synonym
# corpus: chemical formulas (H2O, C17H21NO4), unit/slash abbreviations
# (km/h, GNU/Linux), unit symbols with a superscript 2/3 (cm³), and
# truncated decimal expansions ("3,14159..."). None of these are real
# dictionary words, so they must never become flashcards.
JUNK_HEADWORD_RE = re.compile(
    r"^[\d.,]+\.{3}$"
    r"|^[A-Za-z]{1,4}\d[A-Za-z\d]{0,8}$"
    r"|^[A-Za-z]+/[A-Za-z]+$"
    r"|^[a-z]{1,4}[\u00b2\u00b3]$"
)


def clean_tokens(raw_field, exclude_key=None):
    """Split a '#'-delimited field into a deduped, cleaned list of tokens."""
    out = []
    seen = set()
    for tok in raw_field.split("#"):
        t = tok.strip().strip(",").strip()
        if not t or JUNK_TOKEN_RE.match(t):
            continue
        key = t.lower()
        if key in seen or (exclude_key and key == exclude_key):
            continue
        seen.add(key)
        out.append(t)
    return out


def parse_english():
    with open(EN_SRC, encoding="utf-8") as f:
        lines = f.readlines()
    entries = {}
    order = []
    for raw in lines:
        line = raw.rstrip("\n\r")
        if not line.strip():
            continue
        parts = line.split(",", 2)
        word = parts[0].strip()
        if not word or JUNK_HEADWORD_RE.match(word):
            continue
        syn_field = parts[1] if len(parts) > 1 else ""
        ant_field = parts[2] if len(parts) > 2 else ""
        key = word.lower()
        if key not in entries:
            entries[key] = {"word": word, "syn_seen": set(), "synonyms": [], "ant_seen": set(), "antonyms": []}
            order.append(key)
        e = entries[key]
        for t in clean_tokens(syn_field, key):
            tl = t.lower()
            if tl not in e["syn_seen"]:
                e["syn_seen"].add(tl)
                e["synonyms"].append(t)
        for t in clean_tokens(ant_field, key):
            tl = t.lower()
            if tl not in e["ant_seen"]:
                e["ant_seen"].add(tl)
                e["antonyms"].append(t)
    result = []
    for key in order:
        e = entries[key]
        if not e["synonyms"] and not e["antonyms"]:
            continue
        result.append(
            {
                "word": e["word"],
                "level": "SYN",
                "definition": "",
                "example": "",
                "synonyms": "; ".join(e["synonyms"]),
                "antonyms": "; ".join(e["antonyms"]),
            }
        )
    return result


# Curated common German antonym pairs (best-effort, not exhaustive). Applied
# on top of the synonym-only source. Kept intentionally small/high-confidence
# rather than guessed; most words will simply have no antonym, same as most
# English words in real usage lack a clean single antonym.
ANTONYM_PAIRS_DE = [
    ("groß", "klein"), ("gut", "schlecht"), ("schön", "hässlich"), ("neu", "alt"),
    ("jung", "alt"), ("schnell", "langsam"), ("hoch", "niedrig"), ("breit", "schmal"),
    ("lang", "kurz"), ("dick", "dünn"), ("stark", "schwach"), ("hell", "dunkel"),
    ("warm", "kalt"), ("heiß", "kalt"), ("trocken", "nass"), ("voll", "leer"),
    ("offen", "geschlossen"), ("laut", "leise"), ("hart", "weich"), ("schwer", "leicht"),
    ("reich", "arm"), ("glücklich", "unglücklich"), ("froh", "traurig"), ("ruhig", "unruhig"),
    ("freundlich", "unfreundlich"), ("gesund", "krank"), ("sauber", "schmutzig"),
    ("billig", "teuer"), ("einfach", "schwierig"), ("leicht", "schwierig"),
    ("wichtig", "unwichtig"), ("richtig", "falsch"), ("wahr", "falsch"), ("echt", "falsch"),
    ("sicher", "gefährlich"), ("möglich", "unmöglich"), ("aktiv", "passiv"),
    ("positiv", "negativ"), ("männlich", "weiblich"), ("privat", "öffentlich"),
    ("national", "international"), ("innen", "außen"), ("oben", "unten"),
    ("vorne", "hinten"), ("links", "rechts"), ("Norden", "Süden"), ("Osten", "Westen"),
    ("Anfang", "Ende"), ("Krieg", "Frieden"), ("Liebe", "Hass"), ("Freund", "Feind"),
    ("Erfolg", "Misserfolg"), ("Gewinn", "Verlust"), ("Frage", "Antwort"),
    ("Tag", "Nacht"), ("Sommer", "Winter"), ("kaufen", "verkaufen"),
    ("öffnen", "schließen"), ("beginnen", "beenden"), ("kommen", "gehen"),
    ("geben", "nehmen"), ("lieben", "hassen"), ("gewinnen", "verlieren"),
    ("lachen", "weinen"), ("leben", "sterben"), ("helfen", "schaden"),
    ("erlauben", "verbieten"), ("akzeptieren", "ablehnen"), ("loben", "tadeln"),
    ("bauen", "zerstören"), ("sparen", "verschwenden"), ("steigen", "sinken"),
    ("zunehmen", "abnehmen"), ("vergrößern", "verkleinern"), ("erhöhen", "senken"),
    ("erinnern", "vergessen"), ("beginn", "ende"), ("anfangen", "aufhören"),
    ("schwierig", "einfach"), ("dünn", "dick"), ("arm", "reich"), ("krank", "gesund"),
    ("schmutzig", "sauber"), ("teuer", "billig"), ("falsch", "richtig"),
    ("gefährlich", "sicher"), ("unmöglich", "möglich"), ("passiv", "aktiv"),
    ("negativ", "positiv"), ("weiblich", "männlich"), ("öffentlich", "privat"),
    ("außen", "innen"), ("unten", "oben"), ("hinten", "vorne"), ("rechts", "links"),
    ("Süden", "Norden"), ("Westen", "Osten"), ("Ende", "Anfang"), ("Frieden", "Krieg"),
    ("Hass", "Liebe"), ("Feind", "Freund"), ("Misserfolg", "Erfolg"), ("Verlust", "Gewinn"),
    ("Antwort", "Frage"), ("Nacht", "Tag"), ("Winter", "Sommer"), ("verkaufen", "kaufen"),
    ("schließen", "öffnen"), ("beenden", "beginnen"), ("gehen", "kommen"),
    ("nehmen", "geben"), ("hassen", "lieben"), ("verlieren", "gewinnen"),
    ("weinen", "lachen"), ("sterben", "leben"), ("schaden", "helfen"),
    ("verbieten", "erlauben"), ("ablehnen", "akzeptieren"), ("tadeln", "loben"),
    ("zerstören", "bauen"), ("verschwenden", "sparen"), ("sinken", "steigen"),
    ("abnehmen", "zunehmen"), ("verkleinern", "vergrößern"), ("senken", "erhöhen"),
    ("vergessen", "erinnern"), ("aufhören", "anfangen"), ("leise", "laut"),
    ("weich", "hart"), ("leer", "voll"), ("geschlossen", "offen"), ("dunkel", "hell"),
    ("kalt", "warm"), ("nass", "trocken"), ("klein", "groß"), ("schlecht", "gut"),
    ("hässlich", "schön"), ("alt", "neu"), ("langsam", "schnell"), ("niedrig", "hoch"),
    ("schmal", "breit"), ("kurz", "lang"), ("schwach", "stark"), ("unwichtig", "wichtig"),
    ("unglücklich", "glücklich"), ("traurig", "froh"), ("unruhig", "ruhig"),
    ("unfreundlich", "freundlich"), ("international", "national"),
]
ANTONYM_DE = {}
for a, b in ANTONYM_PAIRS_DE:
    ANTONYM_DE.setdefault(a, b)
    ANTONYM_DE.setdefault(b, a)

META_RE = re.compile(r"^(Language|Charset|Thesaurus):")


def parse_german():
    with open(DE_SRC, encoding="utf-8") as f:
        lines = f.readlines()
    entries = {}
    order = []
    for raw in lines:
        line = raw.rstrip("\n\r")
        stripped = line.strip()
        if not stripped or stripped.startswith("#") or META_RE.match(stripped):
            continue
        tokens = stripped.split()
        if len(tokens) < 2:
            continue
        word = tokens[0]
        if JUNK_HEADWORD_RE.match(word):
            continue
        syns = tokens[1:]
        if word not in entries:
            entries[word] = {"word": word, "seen": set(), "synonyms": []}
            order.append(word)
        e = entries[word]
        for s in syns:
            if s != word and s not in e["seen"]:
                e["seen"].add(s)
                e["synonyms"].append(s)
    result = []
    for key in order:
        e = entries[key]
        if not e["synonyms"]:
            continue
        antonym = ANTONYM_DE.get(e["word"])
        result.append(
            {
                "word": e["word"],
                "level": "SYN",
                "definition": "",
                "example": "",
                "synonyms": "; ".join(e["synonyms"]),
                "antonyms": antonym or "",
            }
        )
    return result


def js_escape(s):
    return s.replace("\\", "\\\\").replace('"', '\\"')


def load_definition_cache(path, fold_keys=False):
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as f:
        raw = json.load(f)
    out = {}
    for k, v in raw.items():
        if not isinstance(v, dict):
            continue
        key = k.lower() if fold_keys else k
        out[key] = {
            "definition": (v.get("definition") or "").strip(),
            "example": (v.get("example") or "").strip(),
        }
    return out


def merge_definition_cache(entries, cache, fold_lookup=False):
    filled = 0
    for e in entries:
        key = e["word"].lower() if fold_lookup else e["word"]
        hit = cache.get(key)
        if not hit:
            continue
        if hit["definition"]:
            e["definition"] = hit["definition"]
        if hit["example"]:
            e["example"] = hit["example"]
        if e["definition"]:
            filled += 1
    return filled


def write_js(path, varname, entries, source_note):
    lines = []
    with_definition = sum(1 for e in entries if e.get("definition"))
    with_example = sum(1 for e in entries if e.get("example"))
    lines.append("// Auto-generated by scripts/build_synant.py — DO NOT hand-edit entries here.")
    lines.append("// Source: " + source_note)
    lines.append(
        "// Schema: { word, level, definition, example, synonyms, antonyms }."
    )
    lines.append(
        "// Fetched coverage at generation time: definition %d/%d, example %d/%d."
        % (with_definition, len(entries), with_example, len(entries))
    )
    lines.append("// synonyms/antonyms are semicolon-separated. Used ONLY by Word Morph (wordmorph.js), independent of WORD_SETS.")
    lines.append("window.%s = [" % varname)
    for e in entries:
        parts = [
            'word:"%s"' % js_escape(e["word"]),
            'level:"%s"' % e["level"],
            'definition:"%s"' % js_escape(e["definition"]),
            'example:"%s"' % js_escape(e["example"]),
        ]
        if e["synonyms"]:
            parts.append('synonyms:"%s"' % js_escape(e["synonyms"]))
        if e["antonyms"]:
            parts.append('antonyms:"%s"' % js_escape(e["antonyms"]))
        lines.append("{" + ",".join(parts) + "},")
    lines.append("];")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    en_cache = load_definition_cache(EN_CACHE, fold_keys=True)
    de_cache = load_definition_cache(DE_CACHE, fold_keys=False)

    en = parse_english()
    en_filled = merge_definition_cache(en, en_cache, fold_lookup=True)
    write_js(
        EN_OUT,
        "SYN_ANT_EN",
        en,
        "https://raw.githubusercontent.com/verachell/English-word-lists-synonyms-antonyms/main/syn-ant.csv",
    )
    print("English entries:", len(en))
    print("English entries with a definition:", en_filled)

    de = parse_german()
    de_filled = merge_definition_cache(de, de_cache, fold_lookup=False)
    write_js(
        DE_OUT,
        "SYN_ANT_DE",
        de,
        "https://raw.githubusercontent.com/PSeitz/germansynonyms/master/german.syn (antonyms hand-curated on top, best-effort)",
    )
    print("German entries:", len(de))
    print("German entries with a definition:", de_filled)
    with_ant = sum(1 for e in de if e["antonyms"])
    print("German entries with an antonym filled in:", with_ant)
