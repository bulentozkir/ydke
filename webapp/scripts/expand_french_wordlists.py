#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""expand_french_wordlists.py

Expands data/wordsXXfr.js (A1, A2, B1, B2, C1, C2) with auto-harvested
French vocabulary APPENDED after the existing 80 hand-authored entries per
level, aiming for these total per-level counts (explicit user request):
    A1: 1000   A2: 1000   B1: 2000   B2: 2000   C1: 2000   C2: 3000

Pipeline (mirrors this project's proven German/TOEFL expansion scripts --
see scripts/fetch_definitions.py, scripts/expand_toefl.py):
1. Candidate words ranked by frequency, from the open hermitdave/
   FrequencyWords fr_full.txt (already used for German in this project --
   plain statistical frequency data, not a creative/copyrighted work).
   Download first (PowerShell): Invoke-WebRequest -Uri
   https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2018/fr/fr_full.txt
   -OutFile $env:TEMP\\fr_full.txt
2. Basic pre-filter (lowercase French letters + internal hyphen/apostrophe
   only, length 2-25, not already used in the hand-authored files, not in
   the French profanity blocklist).
3. Fetch an English gloss + French example sentence for each surviving
   candidate from en.wiktionary.org's REST "page/definition" endpoint's
   "fr" section (same proven source/endpoint already used for German
   definitions in this project). Reject: no "fr" section, empty
   definition, or tagged as a Proper noun.
4. Walk the frequency-ranked, successfully-defined words in order, filling
   each CEFR level's quota in turn (A1 first, then A2, ... then C2) --
   frequency-as-difficulty-proxy, same trade-off already accepted for this
   project's TOEFL word selection.
5. For nouns, apply a French grammatical-gender SUFFIX HEURISTIC (common,
   well-documented patterns + a small hardcoded exception list, best-effort
   ~85-90% accuracy, NOT a lookup against any licensed gender-tagged
   lexicon) to prepend le/la/l', matching this project's "best-effort"
   acceptance bar for other bulk morphological heuristics (e.g. the
   Partikelverben prefix filter in the German expansion).
6. Translate the English definition and French example to Turkish via
   Google Translate's free endpoint (identical rate-limited pattern to
   translate_and_build_synant.py / expand_toefl.py). Words with no real
   Wiktionary example get the SAME placeholder-example convention already
   used elsewhere in this project ("J'apprends le mot «X»." -- excluded
   from question-building games via shared.js's isPlaceholderExample(),
   which was extended to recognize this French pattern too).
7. Append the assembled entries (matching the exact data/wordsXXfr.js
   schema) to each file, after the existing 80 hand-authored entries.

Caches every fetch/translation to %TEMP% so this is fully resumable --
re-running only fetches/translates what's still missing.
Usage:
  python expand_french_wordlists.py --dry-run   (tiny smoke test, ~20/level)
  python expand_french_wordlists.py             (full run)
  python expand_french_wordlists.py --fetch-only   (stop after fetching defs, no translate/write)
  python expand_french_wordlists.py --skip-fetch   (use only what's already cached)
"""
import os
import re
import sys
import json
import time
import threading
import unicodedata
import urllib.request
import urllib.parse
import urllib.error
from concurrent.futures import ThreadPoolExecutor, as_completed

TEMP = os.environ.get("TEMP", ".")
FREQ_SRC = os.path.join(TEMP, "fr_full.txt")
DEF_CACHE_PATH = os.path.join(TEMP, "fr_defs_cache.json")
TRANSLATE_CACHE_PATH = os.path.join(TEMP, "fr_translate_cache.json")

DATA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")
LEVEL_FILES = {
    "A1": ("wordsa1fr.js", "WORDS_FR_A1"),
    "A2": ("wordsa2fr.js", "WORDS_FR_A2"),
    "B1": ("wordsb1fr.js", "WORDS_FR_B1"),
    "B2": ("wordsb2fr.js", "WORDS_FR_B2"),
    "C1": ("wordsc1fr.js", "WORDS_FR_C1"),
    "C2": ("wordsc2fr.js", "WORDS_FR_C2"),
}
TARGET_TOTAL = {"A1": 7992, "A2": 5995, "B1": 6987, "B2": 6991, "C1": 6989, "C2": 7986}

HEADERS = {
    "User-Agent": "TopWordsApp/1.0 (free educational Turkish vocabulary app; "
    "https://udsp.vercel.app)"
}

# ---------------------------------------------------------------- filtering
FR_LETTERS = "a-zàâäéèêëïîôöùûüÿçœæ"
WORD_RE = re.compile(r"^[" + FR_LETTERS + r"]+([-'’][" + FR_LETTERS + r"]+)*$")

PROFANITY = {
    "merde", "putain", "connard", "connasse", "salope", "encule", "enculer",
    "enculé", "encul\u00e9e", "batard", "b\u00e2tard", "pute", "salaud",
    "couillon", "bite", "couille", "couilles", "cul", "chier", "chiotte",
    "chiottes", "foutre", "niquer", "nique", "pd", "pédé", "pede", "negre",
    "n\u00e8gre", "bougnoule", "youpin", "pucelle", "baiser", "baise",
    "branler", "branlette", "salopard", "fdp", "ntm", "batarde", "conne",
    "con", "connards", "connasses", "chieur", "chieuse", "emmerder",
    "emmerdant", "emmerde", "foutu", "foutue",
    # Explicit anatomical/sex-act terms (French headwords) -- same QA-
    # driven addition as the English/German CEFR expansion scripts (a
    # bare-word check catches the HEADWORD itself, since the definition-
    # content check alone -- SENSITIVE_DEF_RE below -- won't catch a
    # word whose OWN gloss is short/generic, e.g. "vaginal. - vajinal").
    "vaginal", "vaginale", "vaginaux", "vaginales", "clitoris", "godemichet",
    "gode", "fellation", "cunnilingus", "masturbation", "masturber",
    "ejaculation", "éjaculation", "orgasme", "prépuce", "circoncision",
    "vulve", "prostituée", "prostitution", "bordel", "strip-tease",
    "strip-teaseuse", "travesti", "voyeurisme", "exhibitionniste",
    # Second, broader QA pass (scanned the raw definitions cache for
    # sexual/genital/prostitut/etc keyword hits) surfaced these crude-slang
    # / sex-work / explicit terms and wrongly-picked-sense words (e.g.
    # "parties" picked the euphemistic "genitals" sense instead of its
    # much more common "games/matches/parts" sense, "foutoir"/"soft" got
    # slang senses instead of "mess"/"soft (texture)") -- excluding the
    # whole word since this pipeline has no way to pick a different sense.
    "vit", "parties", "penis", "pénis", "porno", "pornographie",
    "pornographique", "prostituée", "prostitué", "prostituer", "courtisane",
    "pétasse", "déconner", "vagin", "homo", "gay", "lesbienne", "homosexuel",
    "homosexualité", "hétéro", "hétérosexuel", "bisexuel", "zizi", "pine",
    "zob", "puceau", "foutoir", "mouille", "mogo", "inceste", "fellation",
    "nichon", "soft", "lebron", "lubrique", "érotisme", "érotique",
    "fétichisme", "fétiche", "fétichiste", "vibromasseur", "coït",
    "foufoune", "cougar", "régulière", "coquine",
    # Third QA pass (found via _diag_bad_examples.py against WRITTEN
    # files, not just the cache) -- "shit" (English loanword slang for
    # hashish/cannabis -- drug slang, not just profanity), "pornographe"
    # (pornographer), "molester" (means "to maul/rough up" in actual
    # French, a false-friend of the English word but too easily misread
    # as the unrelated, much more serious English sense -- excluded for
    # learner clarity/comfort despite being a legitimate French verb).
    "shit", "pornographe", "molester",
    # Fourth QA pass (Phase D, +4000 re-expansion) -- found via
    # _diag_headword_scan.py against the freshly-harvested A1 batch:
    # "fouteur"/"fucker" (crude slang for "one who fucks"), "hentai"
    # (pornographic anime/manga genre), "gigolo"/"nympho"/"bitch"/"pussy"
    # (explicit/crude English loanwords that slipped in as French
    # candidates via the frequency list).
    "fouteur", "fucker", "hentai", "gigolo", "nympho", "bitch", "pussy",
    "porn",
}


# Shared stem-based content-safety filter (catches inflected/prefixed crude
# forms the exact-set PROFANITY list above misses). Defensive import so a
# missing module never breaks a harvest run.
try:
    from content_safety import is_inappropriate as _cs_bad
except Exception:
    def _cs_bad(w, lang="en"):
        return False


def is_clean_candidate(word, exclude_set):
    if not WORD_RE.match(word):
        return False
    if len(word) < 3 or len(word) > 25:
        return False
    if word in PROFANITY:
        return False
    if _cs_bad(word, "fr"):
        return False
    norm = strip_article(word).lower()
    if norm in exclude_set:
        return False
    return True


# Open-class content words only (noun/verb/adjective/adverb) -- closed-class
# grammar words (conjunctions, prepositions, determiners, pronouns) are a
# small fixed set better taught as grammar, not expanded as "vocabulary";
# excluding them also sidesteps most of the lowest-value/most homograph-prone
# harvested entries ("de", "du", "ou", "si", "ma"...).
ACCEPTED_POS = {"noun", "verb", "adjective", "adverb"}

# Wiktionary cross-reference / maintenance-note patterns that are NOT usable
# standalone definitions -- reject and treat the candidate as not found.
# Uses search() (not match()) since these can appear after a "first/third-"
# style combined-person prefix rather than at the very start of the string.
BAD_DEFINITION_RE = re.compile(
    r"(inflection of|form of|plural of|feminine of|feminine singular of|"
    r"feminine plural of|masculine plural of|alternative (spelling|form) of|"
    r"obsolete (spelling|form) of|archaic (spelling|form) of|"
    r"dated (spelling|form) of|misspelling of|superseded (spelling|form) of|"
    r"past participle of|present participle of|"
    r"-person (singular|plural)|present indicative|present subjunctive|"
    r"imperative of|imperfect of|preterite of|future of|conditional of)",
    re.IGNORECASE,
)
MAINTENANCE_NOTE_RE = re.compile(
    r"(quotations? indicative|citation needed|can we verify|please add|"
    r"rfquote|rfdef|is being sought)",
    re.IGNORECASE,
)
# Content-based sensitive-topic/slur filter -- applied on top of the
# structural BAD_DEFINITION_RE/MAINTENANCE_NOTE_RE checks above (found via
# QA on the sibling English-expansion script: a single ethnic slur entry
# slipped past label-based filtering alone since its OWN definition text
# said so explicitly -- "offensive term for ...").
SENSITIVE_DEF_RE = re.compile(
    r"\b(sexually attracted to child|self-harm|self harm|suicid|genocide|"
    r"terroris|rape|raping|raped|molest|incest|bestiality|pedophil|"
    r"paedophil|masturbat|ejaculat|orgasm|fellatio|sodom|"
    r"offensive term|derogatory term|disparaging term|ethnic slur|racial slur|"
    r"\bslur\b|contemptuous term|insulting term|offensive name|racist term)\w*\b",
    re.IGNORECASE,
)


ARTICLE_RE = re.compile(r"^(le|la|les)\s+", re.IGNORECASE)
ELISION_RE = re.compile(r"^l['\u2019]", re.IGNORECASE)


def strip_article(word):
    w = ARTICLE_RE.sub("", word)
    w = ELISION_RE.sub("", w)
    return w


def load_existing_words():
    """Parses word: "..." out of each existing hand-authored wordsXXfr.js
    file so the harvested list never duplicates an already-curated entry."""
    exclude = set()
    for level, (fname, _) in LEVEL_FILES.items():
        path = os.path.join(DATA_DIR, fname)
        if not os.path.exists(path):
            continue
        with open(path, encoding="utf-8") as f:
            content = f.read()
        for m in re.finditer(r'word:\s*"((?:[^"\\]|\\.)*)"', content):
            w = m.group(1)
            exclude.add(strip_article(w).lower())
    return exclude


# --------------------------------------------------------- frequency source
FREQ_URL = "https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2018/fr/fr_full.txt"


def ensure_freq_list():
    """Auto re-download if missing -- e.g. if %TEMP% got cleared between
    sessions (observed to happen to this exact file more than once; the
    plain-text frequency dump is cheap/fast to rebuild, unlike the fetch
    caches, so just fetch it again rather than requiring a manual step)."""
    if os.path.exists(FREQ_SRC):
        return
    print("[freq] %s missing, re-downloading..." % FREQ_SRC, flush=True)
    req = urllib.request.Request(FREQ_URL, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=120) as resp:
        data = resp.read()
    with open(FREQ_SRC, "wb") as f:
        f.write(data)


def load_frequency_words():
    ensure_freq_list()
    words = []
    with open(FREQ_SRC, encoding="utf-8") as f:
        for line in f:
            parts = line.rstrip("\n").split(" ")
            if len(parts) != 2:
                continue
            w = parts[0].strip().lower()
            words.append(w)
    return words


# ----------------------------------------------------------- rate limiting
class RateLimiter:
    def __init__(self, min_interval):
        self.min_interval = min_interval
        self.lock = threading.Lock()
        self.next_time = 0.0

    def wait(self):
        with self.lock:
            now = time.time()
            start_at = max(now, self.next_time)
            self.next_time = start_at + self.min_interval
        delay = start_at - now
        if delay > 0:
            time.sleep(delay)


DEF_LIMITER = RateLimiter(0.28)  # ~3.5 req/s aggregate, same conservatism as the German fetch
TRANSLATE_LIMITER = RateLimiter(0.15)  # ~6.6 req/s aggregate, proven safe elsewhere in this project


class NotFound(Exception):
    pass


class Transient(Exception):
    pass


STYLE_RE = re.compile(r"<style\b[^>]*>.*?</style>", re.DOTALL | re.IGNORECASE)
TAG_RE = re.compile(r"<[^>]+>")
WS_RE = re.compile(r"[ \t]+")


def strip_html(s):
    s = STYLE_RE.sub("", s)
    s = TAG_RE.sub("", s)
    s = s.replace("&nbsp;", " ")
    s = WS_RE.sub(" ", s)
    return s.strip()


POS_MAP = {
    "noun": "noun",
    "verb": "verb",
    "adjective": "adjective",
    "adverb": "adverb",
    "pronoun": "pronoun",
    "preposition": "preposition",
    "conjunction": "conjunction",
    "interjection": "phrase",
    "numeral": "number",
    "determiner": "determiner",
}


def fetch_fr_definition(word):
    url = "https://en.wiktionary.org/api/rest_v1/page/definition/" + urllib.parse.quote(word)
    last_err = None
    for i in range(4):
        DEF_LIMITER.wait()
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=15) as resp:
                data = json.loads(resp.read().decode("utf-8"))
            break
        except urllib.error.HTTPError as e:
            if e.code == 404:
                raise NotFound(str(e))
            last_err = e
        except Exception as e:
            last_err = e
        time.sleep(0.8 * (2 ** i))
    else:
        raise Transient(str(last_err))

    fr_entries = data.get("fr")
    if not fr_entries:
        raise NotFound("no French section for " + word)
    for entry in fr_entries:
        raw_pos = (entry.get("partOfSpeech") or "").strip().lower()
        if raw_pos == "proper noun":
            raise NotFound("proper noun: " + word)
        pos = POS_MAP.get(raw_pos, "")
        if pos not in ACCEPTED_POS:
            continue
        for d in entry.get("definitions", []):
            definition = strip_html(d.get("definition", "")).split(" | ")[0].strip()
            # Strip a leading "(...)" register/topic tag like "(dated)" if the
            # rest of the definition is still meaningful on its own.
            definition = re.sub(r"^\([^)]{1,40}\)\s*", "", definition).strip()
            if not definition:
                continue
            if BAD_DEFINITION_RE.search(definition) or MAINTENANCE_NOTE_RE.search(definition):
                continue
            if SENSITIVE_DEF_RE.search(definition):
                continue
            example = ""
            exs = d.get("examples") or []
            if exs:
                example = strip_html(exs[0])
                if MAINTENANCE_NOTE_RE.search(example):
                    example = ""
            return {"definition": definition, "pos": pos, "example": example}
    raise NotFound("no usable definition: " + word)


def load_json(path):
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    return {}


def save_json(path, data):
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False)
    os.replace(tmp, path)


def fetch_all_definitions(candidates, workers=4):
    cache = load_json(DEF_CACHE_PATH)
    todo = [w for w in candidates if w not in cache]
    print("[defs] %d candidates, %d cached, %d to fetch" % (len(candidates), len(candidates) - len(todo), len(todo)), flush=True)
    done = 0
    start = time.time()
    with ThreadPoolExecutor(max_workers=workers) as ex:
        futures = {ex.submit(fetch_fr_definition, w): w for w in todo}
        for fut in as_completed(futures):
            w = futures[fut]
            try:
                cache[w] = fut.result()
            except NotFound:
                cache[w] = None  # definitive: no usable French entry
            except Transient:
                pass  # left OUT of cache -- retried on a future run
            done += 1
            if done % 200 == 0 or done == len(todo):
                save_json(DEF_CACHE_PATH, cache)
                elapsed = time.time() - start
                rate = done / elapsed if elapsed else 0
                remaining = (len(todo) - done) / rate if rate else 0
                found = sum(1 for w2 in todo[:done] if cache.get(w2))
                print(
                    "[defs] %d/%d done (%.1f/s, ~%.1f min left, %d found so far)"
                    % (done, len(todo), rate, remaining / 60, found),
                    flush=True,
                )
    save_json(DEF_CACHE_PATH, cache)
    return cache


# ------------------------------------------------------- gender heuristic
GENDER_EXCEPTIONS_F = {
    "image", "page", "cage", "plage", "nage", "rage", "cible", "grange",
    "orange", "éponge", "eponge", "vidange", "horloge", "vengeance",
    "main", "fin", "faim", "dent", "jument", "forêt", "foret", "mer",
    "peau", "eau", "tribu", "vertu", "dartre", "hart",
}
GENDER_EXCEPTIONS_M = {
    "lycée", "lycee", "musée", "musee", "silence", "squelette", "incendie",
    "parapluie", "planisphère", "planisphere", "capitaine", "modèle",
    "modele", "problème", "probleme", "système", "systeme", "programme",
    "légume", "legume", "cadre", "reste", "monde", "verbe", "adverbe",
    "genre", "groupe", "espace", "vase", "guide",
}
FEM_SUFFIXES = (
    "tion", "sion", "xion", "té", "tié", "ette", "elle", "esse", "ude",
    "ure", "ance", "ence", "ade", "euse", "rice", "aison", "ison", "aille",
    "ouille", "ienne", "onne", "ise", "aine", "eresse",
)
MASC_SUFFIXES = (
    "age", "ment", "isme", "oir", "in", "ing", "al", "ou", "acle", "ège",
    "ege", "ème", "eme", "ome", "eau", "et", "ier", "on", "at", "aire",
    "phone", "scope", "logue", "gramme",
)


def guess_gender(bare_word):
    w = bare_word.lower()
    if w in GENDER_EXCEPTIONS_F:
        return "f"
    if w in GENDER_EXCEPTIONS_M:
        return "m"
    for suf in FEM_SUFFIXES:
        if w.endswith(suf):
            return "f"
    for suf in MASC_SUFFIXES:
        if w.endswith(suf):
            return "m"
    return "m"  # French has more masculine nouns overall; safest default


VOWEL_START_RE = re.compile(r"^[aeiouhàâäéèêëïîôöùûü]", re.IGNORECASE)


def add_article(bare_word, pos):
    if pos != "noun":
        return bare_word
    if VOWEL_START_RE.match(bare_word):
        return "l'" + bare_word
    gender = guess_gender(bare_word)
    return ("la " if gender == "f" else "le ") + bare_word


# --------------------------------------------------------------- translate
def translate_one(text, src, tgt, attempts=4, base_delay=0.6):
    q = urllib.parse.quote(text.replace("\n", " ").strip())
    url = (
        "https://translate.googleapis.com/translate_a/single"
        f"?client=gtx&sl={src}&tl={tgt}&dt=t&q={q}"
    )
    last_err = None
    for i in range(attempts):
        TRANSLATE_LIMITER.wait()
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=20) as resp:
                data = json.loads(resp.read().decode("utf-8"))
            return "".join(seg[0] for seg in data[0])
        except Exception as e:
            last_err = e
        time.sleep(base_delay * (2 ** i))
    raise Transient(str(last_err))


def translate_many(pairs, cache):
    todo = []
    seen = set()
    for src, tgt, text in pairs:
        if not text:
            continue
        key = f"{src}|{tgt}|{text}"
        if key in cache or key in seen:
            continue
        seen.add(key)
        todo.append((src, tgt, text, key))
    total = len(todo)
    print(f"[translate] {total} distinct strings to translate ({len(cache)} already cached)", flush=True)
    if total == 0:
        return
    done = 0
    start = time.time()
    with ThreadPoolExecutor(max_workers=6) as ex:
        futures = {ex.submit(translate_one, text, src, tgt): key for src, tgt, text, key in todo}
        for fut in as_completed(futures):
            key = futures[fut]
            try:
                cache[key] = fut.result()
            except Transient:
                pass
            done += 1
            if done % 300 == 0 or done == total:
                save_json(TRANSLATE_CACHE_PATH, cache)
                elapsed = time.time() - start
                rate = done / elapsed if elapsed else 0
                remaining = (total - done) / rate if rate else 0
                print(f"[translate] {done}/{total} ({rate:.1f}/s, ~{remaining/60:.1f} min left)", flush=True)
    save_json(TRANSLATE_CACHE_PATH, cache)


def tr(cache, src, tgt, text):
    return cache.get(f"{src}|{tgt}|{text}", "")


# ------------------------------------------------------------------ output
def js_escape(s):
    return (
        s.replace("\\", "\\\\")
        .replace('"', '\\"')
        .replace("\n", " ")
        .replace("\r", " ")
        .strip()
    )


def append_entries_to_file(level, entries):
    fname, varname = LEVEL_FILES[level]
    path = os.path.join(DATA_DIR, fname)
    with open(path, encoding="utf-8") as f:
        content = f.read()
    marker = "];"
    idx = content.rstrip().rfind(marker)
    if idx == -1:
        raise RuntimeError("could not find closing '];' in " + fname)
    head = content[:idx]
    tail = content[idx:]
    lines = []
    for e in entries:
        lines.append("  {")
        lines.append('    word: "%s",' % js_escape(e["word"]))
        lines.append('    pos: "%s",' % js_escape(e["pos"]))
        lines.append('    level: "%s",' % level)
        lines.append('    category: "General",')
        lines.append('    definition: "%s",' % js_escape(e["definition"]))
        lines.append('    example: "%s",' % js_escape(e["example"]))
        lines.append("  },")
    new_content = head + "\n".join(lines) + "\n" + tail
    with open(path, "w", encoding="utf-8") as f:
        f.write(new_content)
    print("[write] appended %d entries to %s (var %s)" % (len(entries), fname, varname), flush=True)


PLACEHOLDER_EXAMPLE_TEMPLATE = 'J\u2019apprends le mot \u00ab {word} \u00bb.'
PLACEHOLDER_TR_TEMPLATE = '"{word}" kelimesini \u00f6\u011freniyorum.'


def _int_arg(name, default):
    if name in sys.argv:
        i = sys.argv.index(name)
        if i + 1 < len(sys.argv):
            return int(sys.argv[i + 1])
    return default


def main():
    dry_run = "--dry-run" in sys.argv
    fetch_only = "--fetch-only" in sys.argv
    skip_fetch = "--skip-fetch" in sys.argv
    max_rounds = _int_arg("--max-rounds", 200)
    batch_size_override = _int_arg("--batch-size", 0)

    exclude = load_existing_words()
    print("[setup] %d existing words excluded from harvesting" % len(exclude), flush=True)

    freq_words = load_frequency_words()
    print("[setup] %d frequency-ranked candidate words loaded" % len(freq_words), flush=True)

    seen_norm = set()
    candidates = []
    for w in freq_words:
        if w in seen_norm:
            continue
        if not is_clean_candidate(w, exclude):
            continue
        seen_norm.add(w)
        candidates.append(w)
        if dry_run and len(candidates) >= 400:
            break

    print("[setup] %d candidates survive pre-filtering" % len(candidates), flush=True)

    targets = dict(TARGET_TOTAL)

    current_counts = {}
    for lvl, (fname, _) in LEVEL_FILES.items():
        path = os.path.join(DATA_DIR, fname)
        with open(path, encoding="utf-8") as f:
            current_counts[lvl] = len(re.findall(r'word:\s*"', f.read()))
    print("[setup] current word counts per level: %s" % current_counts, flush=True)

    needed_new = {lvl: max(0, targets[lvl] - current_counts[lvl]) for lvl in targets}
    if dry_run:
        needed_new = {k: 20 for k in targets}
    total_needed = sum(needed_new.values())
    print("[setup] new words needed per level: %s (total %d)" % (needed_new, total_needed), flush=True)

    level_order = ["A1", "A2", "B1", "B2", "C1", "C2"]

    if not skip_fetch:
        # Iteratively fetch in batches STARTING FROM the first not-yet-cached
        # candidate (not a single big headroom slice from position 0) --
        # a prior session may have already exhaustively explored (and mostly
        # rejected) a long prefix of the frequency-ranked candidate list, so
        # blindly re-slicing from the start can land entirely inside
        # already-fully-cached (and already-consumed-or-rejected) territory,
        # fetching nothing new at all. Keep advancing in batches until every
        # level's quota is filled or the candidate list is exhausted.
        def_cache_probe = load_json(DEF_CACHE_PATH)
        first_uncached = 0
        while first_uncached < len(candidates) and candidates[first_uncached] in def_cache_probe:
            first_uncached += 1
        print("[setup] first not-yet-cached candidate at position %d/%d" % (first_uncached, len(candidates)), flush=True)

        batch_size = batch_size_override or max(3000, total_needed * 2)
        pos = first_uncached
        for round_i in range(max_rounds):
            def_cache_probe = load_json(DEF_CACHE_PATH)
            level_idx_probe = 0
            filled = {lvl: 0 for lvl in level_order}
            for w in candidates[:pos] if pos else []:
                while level_idx_probe < len(level_order) and filled[level_order[level_idx_probe]] >= needed_new[level_order[level_idx_probe]]:
                    level_idx_probe += 1
                if level_idx_probe >= len(level_order):
                    break
                if def_cache_probe.get(w):
                    filled[level_order[level_idx_probe]] += 1
            remaining = sum(max(0, needed_new[lvl] - filled[lvl]) for lvl in level_order)
            print("[round %d] pos=%d filled=%s remaining=%d" % (round_i, pos, filled, remaining), flush=True)
            if remaining <= 0 or pos >= len(candidates):
                break
            batch = candidates[pos: pos + batch_size]
            if not batch:
                break
            fetch_all_definitions(batch, workers=4)
            pos += batch_size

    def_cache = load_json(DEF_CACHE_PATH)

    if fetch_only:
        found = sum(1 for v in def_cache.values() if v)
        print("[fetch-only] stopping after fetch phase: %d defined so far" % found, flush=True)
        return

    # Walk candidates in rank order, filling each level's quota in turn.
    level_idx = 0
    buckets = {lvl: [] for lvl in level_order}
    for w in candidates:
        while level_idx < len(level_order) and len(buckets[level_order[level_idx]]) >= needed_new[level_order[level_idx]]:
            level_idx += 1
        if level_idx >= len(level_order):
            break
        info = def_cache.get(w)
        if not info:
            continue
        if w in PROFANITY or SENSITIVE_DEF_RE.search(info.get("definition") or ""):
            # Defense-in-depth: catches a word/definition added to the
            # blocklist AFTER it was already fetched+cached by an earlier
            # run (candidate generation already excludes these, but a
            # long-running fetch job started before a blocklist update
            # wouldn't retroactively apply it without this second check).
            continue
        buckets[level_order[level_idx]].append((w, info))

    for lvl in level_order:
        print("[bucket] %s: %d/%d filled" % (lvl, len(buckets[lvl]), needed_new[lvl]), flush=True)

    # Translate definitions + examples (with placeholder fallback for words
    # lacking a real Wiktionary example) to Turkish.
    translate_cache = load_json(TRANSLATE_CACHE_PATH)
    pairs = []
    for lvl in level_order:
        for w, info in buckets[lvl]:
            pairs.append(("en", "tr", info["definition"]))
            if info["example"]:
                pairs.append(("fr", "tr", info["example"]))
    translate_many(pairs, translate_cache)
    translate_cache = load_json(TRANSLATE_CACHE_PATH)

    for lvl in level_order:
        entries = []
        for w, info in buckets[lvl]:
            word_out = add_article(w, info["pos"])
            def_tr = tr(translate_cache, "en", "tr", info["definition"])
            definition_out = "%s. - %s" % (info["definition"].rstrip("."), def_tr) if def_tr else info["definition"]
            if info["example"]:
                ex_tr = tr(translate_cache, "fr", "tr", info["example"])
                example_out = "%s - %s" % (info["example"], ex_tr) if ex_tr else info["example"]
            else:
                example_out = "%s - %s" % (
                    PLACEHOLDER_EXAMPLE_TEMPLATE.format(word=w),
                    PLACEHOLDER_TR_TEMPLATE.format(word=w),
                )
            entries.append(
                {
                    "word": word_out,
                    "pos": info["pos"],
                    "definition": definition_out,
                    "example": example_out,
                }
            )
        if entries:
            if dry_run:
                print("[dry-run] %s would get %d entries, e.g.:" % (lvl, len(entries)), flush=True)
                for e in entries[:3]:
                    print("   ", json.dumps(e, ensure_ascii=False), flush=True)
            else:
                append_entries_to_file(lvl, entries)

    print("[done] dry_run=%s" % dry_run, flush=True)


if __name__ == "__main__":
    main()
