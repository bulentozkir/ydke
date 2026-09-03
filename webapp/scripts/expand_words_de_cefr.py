#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""expand_words_de_cefr.py

Adds up to 1000 NEW words to EACH of the 12 German CEFR files (telc-style
A1.1/A1.2/A2.1/A2.2/B1.1/B1.2 + self-authored "gode" A1-C2). This app
already has ~26,000+ existing German words across these 12 files plus
partikelverbde.js/synantde.js, so genuinely NEW candidates are the limiting
factor -- word quality and CEFR-appropriateness matter more than hitting an
exact count (see class docstring precedent in expand_words_en_cefr.py).

Pipeline (same proven infra as expand_french_wordlists.py, just for German):
  - Candidate words + difficulty ranking: hermitdave/FrequencyWords
    de_full.txt (frequency-ranked German word list).
  - Definitions + POS + example: en.wiktionary.org REST "page/definition"
    endpoint's "de" section (same endpoint used by fetch_definitions.py /
    enrich_gode_definitions.py elsewhere in this project) -- fetched ON
    DEMAND in frequency order (stop once each bucket's 1000-word quota is
    filled), since there's no pre-bundled bulk German dictionary dump
    available the way wordset-dictionary is for English.
  - Noun gender (der/die/das): best-effort suffix heuristic (same
    "~85-90% accuracy, hand-picked exceptions + documented trade-off"
    bar already established for the French le/la/l' gender guesser).
  - Turkish: Google Translate free endpoint, threaded + rate-limited.

Since Wiktionary fetch success is NOT guaranteed per candidate (many
frequency-list entries are inflected forms / not in Wiktionary / rejected
for quality), this fetches MORE candidates than the raw quota and keeps
going until each bucket is filled or the candidate stream is exhausted.

Buckets are filled in this fixed order (telc's more granular beginner
split first, then gode's own A1->C2 progression) so that later buckets
naturally get rarer/more-advanced words:
  A1.1, A1.2, A2.1, A2.2, B1.1, B1.2 (telc) then
  A1, A2, B1, B2, C1, C2 (gode)

Usage:
  python scripts/expand_words_de_cefr.py --phase fetch     (fetch+build entries, resumable)
  python scripts/expand_words_de_cefr.py --phase translate
  python scripts/expand_words_de_cefr.py --phase apply
  python scripts/expand_words_de_cefr.py --phase all
"""
import os
import re
import sys
import json
import time
import argparse
import threading
import urllib.request
import urllib.parse
import urllib.error
from concurrent.futures import ThreadPoolExecutor, as_completed

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")
TEMP = os.environ.get("TEMP", ".")

FREQ_SRC = os.path.join(TEMP, "de_full.txt")
FREQ_URL = "https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2018/de/de_full.txt"
DEF_CACHE_PATH = os.path.join(TEMP, "de_cefr_expand_defs_cache.json")
TRANSLATE_CACHE_PATH = os.path.join(TEMP, "de_cefr_expand_translate_cache.json")
ENTRIES_CACHE_PATH = os.path.join(TEMP, "de_cefr_expand_entries.json")

HEADERS = {"User-Agent": "TopWordsApp/1.0 (educational; https://udsp.vercel.app)"}

# (level_label, filename, varname_unused, target_total) -- target_total is
# the FINAL total word count this file should reach (pre-task baseline +
# up to 5000 new), NOT a flat "add N more" cap. Using an absolute target
# (recomputed against the file's LIVE current count every run, same
# pattern as expand_french_wordlists.py's TARGET_TOTAL/needed_new) makes
# this script safe to re-run across multiple incremental fetch->translate
# ->apply cycles -- a flat "add 5000" cap would otherwise re-add ANOTHER
# 5000 on top of an already-fulfilled bucket every time the plan is
# rebuilt, since a freshly-built plan's bucket list always starts at 0
# regardless of what was already appended to the file in a PRIOR apply.
# Baselines (word count before this whole 1000/5000/4000-word expansion
# effort): telc 575/531/476/562/579/415; gode 995/997/996/2984/2994/2985
# (gode B2's baseline is 2983 after 1 explicit-content removal earlier).
# Current TARGET_TOTAL values = actual live count as of 2026-07-14 + 4000
# (telc A1.1/A1.2 already grew substantially in the prior +5000 pass;
# the other 10 buckets are still near their original baseline).
TARGETS = [
    ("A1.1", "wordsa11de.js", "WORDS_DE_A11", 9575),
    ("A1.2", "wordsa12de.js", "WORDS_DE_A12", 9042),
    ("A2.1", "wordsa21de.js", "WORDS_DE_A21", 4476),
    ("A2.2", "wordsa22de.js", "WORDS_DE_A22", 4562),
    ("B1.1", "wordsb11de.js", "WORDS_DE_B11", 4579),
    ("B1.2", "wordsb12de.js", "WORDS_DE_B12", 4415),
    ("A1", "wordsa1gode.js", "WORDS_GODE_A1", 4993),
    ("A2", "wordsa2gode.js", "WORDS_GODE_A2", 4994),
    ("B1", "wordsb1gode.js", "WORDS_GODE_B1", 4986),
    ("B2", "wordsb2gode.js", "WORDS_GODE_B2", 6978),
    ("C1", "wordsc1gode.js", "WORDS_GODE_C1", 6993),
    ("C2", "wordsc2gode.js", "WORDS_GODE_C2", 6979),
]
ALL_DE_FILES = [fname for _, fname, _, _ in TARGETS] + ["partikelverbde.js", "synantde.js"]


def current_file_count(fname):
    p = os.path.join(DATA, fname)
    if not os.path.exists(p):
        return 0
    return len(WORD_RE.findall(open(p, encoding="utf-8").read()))



WORD_RE = re.compile(r'word:\s*"((?:\\.|[^"])*)"')
DASHDASH_RE = re.compile(r"\s[-\u2013\u2014]\s")
DE_WORD_RE = re.compile(r"^[a-zäöüß][a-zäöüß-]{1,25}$")

# Shared stem-based content-safety filter (catches inflected/prefixed crude
# forms the exact PROFANITY_DE list below misses). Defensive import so a
# missing module never breaks a harvest run.
try:
    from content_safety import is_inappropriate as _cs_bad
except Exception:
    def _cs_bad(w, lang="en"):
        return False

PROFANITY_DE = {
    "scheisse", "scheiße", "arsch", "arschloch", "fotze", "wichser", "hure",
    "nutte", "schwanz", "fick", "ficken", "gefickt", "verfickt", "muschi",
    "titten", "titte", "penis", "vagina", "sperma", "orgasmus", "pisse",
    "pissen", "scheiss", "kacke", "kacken", "hurensohn", "schlampe",
    "bumsen", "wixxer", "wichsen", "kanacke", "neger", "judensau", "selbstmord",
    "vergewaltig", "kinderschänder", "pädophil", "pedophil", "inzest",
    # Explicit anatomical/sex-act terms -- same QA-driven addition as the
    # English CEFR expansion script (a bare-word check like this catches
    # the HEADWORD itself, not just definition-text content).
    "klitoris", "dildo", "vibrator", "vorhaut", "beschneidung", "beschnitten",
    "vulva", "schamlippen", "fellatio", "cunnilingus", "sexspielzeug",
    "versaut",
    # Final headword-precision scan caught "Bastard" (dictionary sense used
    # was "person born to unmarried parents" -- a stigmatizing/archaic
    # social-status concept, and the word doubles as a common German
    # insult/curse) -- excluded for the same register reasons as English
    # "bastard".
    "bastard",
}
NAME_HINT_RE = re.compile(
    r"\b(surname|given name|forename|first name|place ?name|proper noun|"
    r"male given|female given|toponym)\b", re.IGNORECASE)
SENSITIVE_DEF_RE = re.compile(
    r"\b(sexually attracted to child|child sex|self-harm|self harm|"
    r"suicid|genocide|terroris|rape|raping|raped|molest|incest|bestiality|"
    r"pedophil|paedophil|masturbat|ejaculat|orgasm|fellatio|sodom|"
    r"offensive term|derogatory term|disparaging term|ethnic slur|racial slur|"
    r"\bslur\b|contemptuous term|insulting term|offensive name|racist term)\w*\b",
    re.IGNORECASE)
BAD_POS = {"", "letter", "symbol", "phrase", "particle"}
POS_MAP = {
    "noun": "noun", "verb": "verb", "adjective": "adjective", "adverb": "adverb",
    "pronoun": "pronoun", "preposition": "preposition", "conjunction": "conjunction",
    "interjection": "phrase", "numeral": "number", "determiner": "determiner",
}
ACCEPTED_POS = {"noun", "verb", "adjective", "adverb"}
# Broad catch-all for INFLECTED-FORM / grammatical-table definitions (not a
# real standalone meaning) -- German Wiktionary entries for common words are
# dominated by conjugated verb forms (imperatives, present-tense persons)
# and declined adjective/noun forms (case+number tables), which must be
# excluded just as aggressively as the more obvious "inflection of" wording.
BAD_DEFINITION_RE = re.compile(
    r"(inflection of|form of|plural of|genitive of|dative of|accusative of|"
    r"nominative of|alternative (spelling|form) of|obsolete (spelling|form) of|"
    r"archaic (spelling|form) of|dated (spelling|form) of|misspelling of|"
    r"superseded (spelling|form) of|past participle of|present participle of|"
    r"-person (singular|plural)|strong verb|weak verb|romanization of|"
    r"\bimperative\b|\bimperative of\b|\bpresent of\b|\bpast of\b|"
    r"comparative of|superlative of|diminutive of|\bdeclension\b|"
    r"\bconjugation\b|singular of|plural imperative|"
    r"(nominative|accusative|dative|genitive)[/ ](nominative|accusative|dative|genitive)|"
    r"(strong|weak|mixed)[/ ](strong|weak|mixed)?\s*(nominative|accusative|dative|genitive)|"
    r"masculine singular|feminine singular|neuter singular|"
    r"masculine plural|feminine plural|neuter plural|"
    r"first[- ]person|second[- ]person|third[- ]person|"
    r"indicative|subjunctive|preterite|umlauted|"
    r"comparative degree|superlative degree)",
    re.IGNORECASE)
TRAILING_BRACKET_RE = re.compile(r"\s*\[[^\]]{1,80}\]\s*$")
MAINTENANCE_NOTE_RE = re.compile(
    r"(quotations? indicative|citation needed|can we verify|please add|"
    r"rfquote|rfdef|is being sought)", re.IGNORECASE)
LEADING_PAREN_RE = re.compile(r"^\([^)]{1,50}\)\s*")

NOTFOUND_EX_DE = "Kein Beispielsatz für dieses Wort verfügbar."
NOTFOUND_EX_TR = "Bu kelime için örnek cümle bulunamadı."


def load_json(p):
    return json.load(open(p, encoding="utf-8")) if os.path.exists(p) else {}


def save_json(p, d):
    tmp = p + ".tmp"
    json.dump(d, open(tmp, "w", encoding="utf-8"), ensure_ascii=False)
    os.replace(tmp, p)


def js_escape(s):
    return (s or "").replace("\\", "\\\\").replace('"', '\\"').replace("\n", " ").replace("\r", " ").strip()


def sanitize(s):
    s = (s or "").strip()
    s = DASHDASH_RE.sub(", ", s)
    return s


def existing_words():
    used = set()
    for fn in ALL_DE_FILES:
        p = os.path.join(DATA, fn)
        if os.path.exists(p):
            for w in WORD_RE.findall(open(p, encoding="utf-8").read()):
                bare = re.sub(r"^(der|die|das)\s+", "", w, flags=re.IGNORECASE)
                used.add(bare.casefold())
    return used


def ensure_freq_list():
    if os.path.exists(FREQ_SRC):
        return
    print("[freq] downloading de_full.txt ...", flush=True)
    req = urllib.request.Request(FREQ_URL, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=120) as resp:
        data = resp.read()
    with open(FREQ_SRC, "wb") as f:
        f.write(data)


def load_candidates(used):
    ensure_freq_list()
    seen = set()
    cands = []
    with open(FREQ_SRC, encoding="utf-8") as f:
        for line in f:
            parts = line.rstrip("\n").split(" ")
            if len(parts) < 2:
                continue
            w = parts[0].strip()
            wl = w.casefold()
            if not DE_WORD_RE.match(w.lower()):
                continue
            if wl in seen or wl in used:
                continue
            if wl in PROFANITY_DE or _cs_bad(w, "de"):
                continue
            seen.add(wl)
            cands.append(w)
    print(f"[candidates] {len(cands)} frequency-ranked German candidates after basic filtering", flush=True)
    return cands


# --------------------------------------------------------------- gender
GENDER_EXC_F = {
    "frau", "mutter", "tochter", "schwester", "tante", "oma", "stadt", "hand",
    "nacht", "wand", "kunst", "macht", "kraft", "angst", "person", "million",
    "million", "art", "zeit", "welt", "arbeit", "wohnung", "person",
}
GENDER_EXC_M = {
    "name", "friede", "buchstabe", "gedanke", "glaube", "wille", "funke",
    "junge", "käse", "monat",
}
GENDER_EXC_N = {
    "mädchen", "auto", "wasser", "kind", "haus", "jahr", "land", "buch",
    "bild", "ende", "herz", "auge", "ohr", "bett", "brot",
}
FEM_SUFFIXES = ("ung", "heit", "keit", "schaft", "tion", "sion", "tät", "ie",
                "ei", "in", "enz", "anz", "ur", "ik")
MASC_SUFFIXES = ("er", "ler", "ner", "ismus", "ant", "ent", "ich", "ig",
                 "ling", "us", "or", "eur", "ar")
NEUT_SUFFIXES = ("chen", "lein", "ment", "tum", "um", "nis", "ma", "o",
                 "en", "il", "at")


def guess_gender(bare_word):
    w = bare_word.lower()
    if w in GENDER_EXC_F:
        return "die"
    if w in GENDER_EXC_M:
        return "der"
    if w in GENDER_EXC_N:
        return "das"
    for suf in NEUT_SUFFIXES:
        if w.endswith(suf) and suf in ("chen", "lein", "ment", "tum", "nis"):
            return "das"
    for suf in FEM_SUFFIXES:
        if w.endswith(suf):
            return "die"
    for suf in MASC_SUFFIXES:
        if w.endswith(suf):
            return "der"
    for suf in NEUT_SUFFIXES:
        if w.endswith(suf):
            return "das"
    return "der"  # most common default, same fallback-to-majority-gender precedent as French


def capitalize_de(w):
    return w[:1].upper() + w[1:] if w else w


# --------------------------------------------------------------- fetch
STYLE_RE = re.compile(r"<style\b[^>]*>.*?</style>", re.DOTALL | re.IGNORECASE)
TAG_RE = re.compile(r"<[^>]+>")
WS_RE = re.compile(r"[ \t]+")


def strip_html(s):
    s = STYLE_RE.sub("", s)
    s = TAG_RE.sub("", s)
    s = s.replace("&nbsp;", " ")
    s = WS_RE.sub(" ", s)
    return s.strip()


class RateLimiter:
    def __init__(self, mi):
        self.mi = mi; self.lock = threading.Lock(); self.t = 0.0

    def wait(self):
        with self.lock:
            now = time.time(); s = max(now, self.t); self.t = s + self.mi
        d = s - now
        if d > 0:
            time.sleep(d)


DEF_LIMITER = RateLimiter(0.28)  # ~3.5 req/s, same conservatism as expand_french_wordlists.py
TRANSLATE_LIMITER = RateLimiter(0.16)


class NotFound(Exception):
    pass


class Transient(Exception):
    pass


def clean_bundled_example(ex):
    ex = strip_html(ex or "").strip()
    if any(x in ex for x in ("--", "\u2014", "\u2013", " - ")):
        return ""
    if len(ex) < 6 or len(ex) > 160:
        return ""
    return ex


def fetch_de_definition(word):
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

    de_entries = data.get("de")
    if not de_entries:
        raise NotFound("no de section")
    for entry in de_entries:
        raw_pos = (entry.get("partOfSpeech") or "").strip().lower()
        if raw_pos == "proper noun":
            raise NotFound("proper noun")
        pos = POS_MAP.get(raw_pos, "")
        if pos not in ACCEPTED_POS:
            continue
        for d in entry.get("definitions", []):
            definition = strip_html(d.get("definition", "")).split(" | ")[0].strip()
            definition = LEADING_PAREN_RE.sub("", definition).strip()
            definition = TRAILING_BRACKET_RE.sub("", definition).strip()
            if not definition or len(definition) < 8 or len(definition) > 160:
                continue
            if BAD_DEFINITION_RE.search(definition) or MAINTENANCE_NOTE_RE.search(definition):
                continue
            if NAME_HINT_RE.search(definition) or SENSITIVE_DEF_RE.search(definition):
                continue
            if re.search(r"(?<![A-Za-zÄÖÜäöüß])" + re.escape(word.lower()) + r"(?![A-Za-zÄÖÜäöüß])", definition.lower()):
                continue  # circular
            example = ""
            exs = d.get("examples") or []
            if exs:
                example = clean_bundled_example(exs[0])
            return {"definition": definition, "pos": pos, "example": example}
    raise NotFound("no usable definition")


def fetch_batch(words, workers=5):
    cache = load_json(DEF_CACHE_PATH)
    todo = [w for w in words if w not in cache]
    if not todo:
        return cache
    done = 0
    start = time.time()
    with ThreadPoolExecutor(max_workers=workers) as ex:
        futs = {ex.submit(fetch_de_definition, w): w for w in todo}
        for fut in as_completed(futs):
            w = futs[fut]
            try:
                cache[w] = fut.result()
            except NotFound:
                cache[w] = None
            except Transient:
                pass
            done += 1
            if done % 300 == 0 or done == len(todo):
                save_json(DEF_CACHE_PATH, cache)
                el = max(0.001, time.time() - start)
                found = sum(1 for w2 in todo[:done] if cache.get(w2))
                print(f"[fetch] {done}/{len(todo)} ({done/el:.1f}/s, {found} found so far)", flush=True)
    save_json(DEF_CACHE_PATH, cache)
    return cache


def reclassify_cache():
    """Re-applies the CURRENT (possibly since-strengthened) definition
    quality checks to already-cached results, purely in-memory/no network
    -- nulls out previously-accepted entries that no longer pass. Use this
    after tightening BAD_DEFINITION_RE/etc. instead of re-fetching (which
    would be slow and wasteful given nothing about the raw Wiktionary
    content itself changed, only our own acceptance criteria)."""
    cache = load_json(DEF_CACHE_PATH)
    changed = 0
    for w, rec in list(cache.items()):
        if not rec:
            continue
        d = rec.get("definition") or ""
        bad = (
            len(d) < 8 or len(d) > 160
            or BAD_DEFINITION_RE.search(d)
            or MAINTENANCE_NOTE_RE.search(d)
            or NAME_HINT_RE.search(d)
            or SENSITIVE_DEF_RE.search(d)
            or w in PROFANITY_DE
            or re.search(r"(?<![A-Za-zÄÖÜäöüß])" + re.escape(w.lower()) + r"(?![A-Za-zÄÖÜäöüß])", d.lower())
        )
        if bad:
            cache[w] = None
            changed += 1
    save_json(DEF_CACHE_PATH, cache)
    print(f"[reclassify] {changed} previously-accepted entries re-rejected under current rules", flush=True)
    return cache


def build_entries(candidates, cache):
    """Walk candidates in order, filling each bucket up to its cap using
    only candidates with a usable cached (found) definition. STOPS (does
    not skip past) the first candidate that has never been attempted yet
    (not in cache at all) -- distinguishes "not yet fetched" from "fetched
    and rejected" (both look like a falsy cache.get() result otherwise),
    so the caller knows exactly where the next fetch batch should resume.
    `cap` is recomputed EVERY call from target_total minus the file's LIVE
    current word count -- so a bucket already fulfilled by a prior apply
    correctly gets cap=0 (skipped) instead of being re-filled from scratch."""
    plan = {level: [] for level, _, _, _ in TARGETS}
    ci = 0
    hit_unknown = False
    for level, fname, varname, target_total in TARGETS:
        cap = max(0, target_total - current_file_count(fname))
        if hit_unknown:
            break
        bucket = plan[level]
        while len(bucket) < cap and ci < len(candidates):
            w = candidates[ci]
            if w not in cache:
                hit_unknown = True
                break
            ci += 1
            rec = cache.get(w)
            if not rec:
                continue
            if w in PROFANITY_DE:
                # Defense-in-depth: catches a word added to PROFANITY_DE
                # AFTER this word was already fetched+cached by an
                # in-progress/earlier run (candidate GENERATION already
                # excludes PROFANITY_DE, but a long-running fetch job
                # started before a blocklist update wouldn't retroactively
                # apply it without this second check here).
                continue
            word_out = capitalize_de(w) if rec["pos"] == "noun" else w
            if rec["pos"] == "noun":
                word_out = guess_gender(w) + " " + word_out
            bucket.append({"word": word_out, "pos": rec["pos"], "definition": rec["definition"], "example": rec["example"]})
        if hit_unknown:
            break
    return plan, ci


def phase_fetch(batch_size=3000, max_rounds=200):
    used = existing_words()
    print(f"[setup] {len(used)} existing German words excluded", flush=True)
    candidates = load_candidates(used)
    cache = load_json(DEF_CACHE_PATH)

    for round_i in range(max_rounds):
        plan, consumed_upto = build_entries(candidates, cache)
        totals = {lvl: len(v) for lvl, v in plan.items()}
        remaining_need = sum(max(0, target_total - current_file_count(fname)) - len(plan[level])
                              for level, fname, _, target_total in TARGETS)
        print(f"[round {round_i}] totals={totals} consumed_upto={consumed_upto}/{len(candidates)} remaining_need={remaining_need}", flush=True)
        if remaining_need <= 0 or consumed_upto >= len(candidates):
            break
        next_batch = candidates[consumed_upto:consumed_upto + batch_size]
        if not next_batch:
            break
        fetch_batch(next_batch)
        cache = load_json(DEF_CACHE_PATH)

    plan, _ = build_entries(candidates, load_json(DEF_CACHE_PATH))
    save_json(ENTRIES_CACHE_PATH, plan)
    for level, fname, varname, target_total in TARGETS:
        cap = max(0, target_total - current_file_count(fname))
        print(f"[plan] {level} ({fname}): {len(plan[level])}/{cap}", flush=True)
    return plan


def translate_one(text, src, tgt="tr", attempts=4, base_delay=0.6):
    q = urllib.parse.quote(text.replace("\n", " ").strip())
    url = f"https://translate.googleapis.com/translate_a/single?client=gtx&sl={src}&tl={tgt}&dt=t&q={q}"
    for i in range(attempts):
        TRANSLATE_LIMITER.wait()
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=20) as resp:
                data = json.loads(resp.read().decode("utf-8"))
            return "".join(seg[0] for seg in data[0])
        except Exception:
            time.sleep(base_delay * (2 ** i))
    return None


def phase_translate():
    plan = load_json(ENTRIES_CACHE_PATH)
    cache = load_json(TRANSLATE_CACHE_PATH)
    strings = []
    for level, entries in plan.items():
        for e in entries:
            strings.append(("de", sanitize(e["definition"])))
            if e.get("example"):
                strings.append(("de", sanitize(e["example"])))
    uniq = sorted(set(strings))
    todo = [(src, s) for src, s in uniq if f"{src}|tr|{s}" not in cache]
    print(f"[translate] {len(todo)} to translate ({len(uniq)-len(todo)} cached of {len(uniq)} unique)", flush=True)
    if todo:
        done = 0; start = time.time()
        with ThreadPoolExecutor(max_workers=6) as ex:
            futs = {ex.submit(translate_one, s, src): (src, s) for src, s in todo}
            for fut in as_completed(futs):
                src, s = futs[fut]
                r = fut.result()
                if r:
                    cache[f"{src}|tr|{s}"] = r
                done += 1
                if done % 200 == 0 or done == len(todo):
                    save_json(TRANSLATE_CACHE_PATH, cache)
                    el = max(0.001, time.time() - start)
                    print(f"[translate] {done}/{len(todo)} ({done/el:.1f}/s)", flush=True)
        save_json(TRANSLATE_CACHE_PATH, cache)


def tr(cache, src, s):
    return cache.get(f"{src}|tr|{s}", "")


def append_entries(path, level, entries):
    text = open(path, encoding="utf-8").read()
    idx = text.rstrip().rfind("];")
    if idx < 0:
        raise RuntimeError("no '];' terminator in " + path)
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
    new_text = text[:idx] + "\n".join(lines) + "\n" + text[idx:]
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(new_text)


def phase_apply():
    plan = load_json(ENTRIES_CACHE_PATH)
    tcache = load_json(TRANSLATE_CACHE_PATH)
    total = 0
    for level, fname, varname, target_total in TARGETS:
        entries = []
        for e in plan[level]:
            d2 = sanitize(e["definition"])
            td = tr(tcache, "de", d2) or "Türkçe çeviri bulunamadı."
            if e.get("example"):
                ex2 = sanitize(e["example"])
                te = tr(tcache, "de", ex2) or NOTFOUND_EX_TR
                if not tr(tcache, "de", ex2):
                    ex2, te = NOTFOUND_EX_DE, NOTFOUND_EX_TR
            else:
                ex2, te = NOTFOUND_EX_DE, NOTFOUND_EX_TR
            entries.append({
                "word": e["word"], "pos": e["pos"],
                "definition": f"{d2} - {td}",
                "example": f"{ex2} - {te}",
            })
        path = os.path.join(DATA, fname)
        append_entries(path, level, entries)
        total += len(entries)
        print(f"[write] {fname}: appended {len(entries)} entries", flush=True)
    print(f"[done] total written={total}", flush=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--phase", default="all", choices=["fetch", "translate", "apply", "all", "reclassify"])
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--batch-size", type=int, default=3000)
    ap.add_argument("--max-rounds", type=int, default=200)
    args = ap.parse_args()

    if args.dry_run:
        used = existing_words()
        cands = load_candidates(used)
        print("first 20 candidates:", cands[:20])
        return

    if args.phase == "reclassify":
        reclassify_cache()
        return

    if args.phase in ("fetch", "all"):
        reclassify_cache()
        phase_fetch(batch_size=args.batch_size, max_rounds=args.max_rounds)
    if args.phase == "fetch":
        return
    if args.phase in ("translate", "all"):
        phase_translate()
    if args.phase == "translate":
        return
    if args.phase in ("apply", "all"):
        phase_apply()


if __name__ == "__main__":
    main()
