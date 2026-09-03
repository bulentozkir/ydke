#!/usr/bin/env python3
"""Upgrade "No dictionary definition available..." FALLBACK entries with real
content, in the German (*gode.js) and English (wordsb2/c1/c2.js) word lists.

Background: scripts/fix_placeholder_definitions.py replaced every auto-generated
"...from open-source list" placeholder with either a real dictionary definition
(+ Turkish translation) or, when no dictionary entry could be obtained, an
honest fallback: "No dictionary definition available for this word. - Bu kelime
için sözlük tanımı bulunamadı." Two categories of those fallbacks are actually
fixable and this script upgrades them:

1. ENGLISH (wordsb2/c1/c2.js): the earlier English fetch run was cut short by
   dictionaryapi.dev rate-limiting, so ~1,200 REAL words (actress, actuary,
   actualize, ...) never got fetched and fell back. Re-fetch those from
   dictionaryapi.dev (single-threaded ~1 req/s -- the API 429s hard above that)
   and translate to Turkish. Words that genuinely 404 (already cached empty)
   keep the honest fallback -- they are hyper-obscure non-words.

2. GERMAN (*gode.js): the English Wiktionary "de" REST section simply lacks
   thousands of real German words (Radspeiche, Pferderennbahn, resümieren, ...).
   Since there is no reliable free German-definition API (de.wiktionary REST
   returns 501), build a translation-based bilingual definition via Google
   Translate: de->en for the English gloss and de->tr for the Turkish gloss,
   giving "English. - Turkish." -- exactly the app's definition convention and
   far more useful than "no definition available". Example sentences (generic
   but grammatically valid "Ich lerne das Wort ...") are left unchanged.

Caches (reused + resumable): %TEMP%\\en_defs_cache.json,
%TEMP%\\synant_translate_cache.json (keyed "src|tgt|text").

Usage: python scripts/improve_fallback_definitions.py [de|en|both] [--limit N]
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

TEMP = os.environ.get("TEMP", ".")
EN_GLOSS_CACHE = os.path.join(TEMP, "en_defs_cache.json")
TRANSLATE_CACHE = os.path.join(TEMP, "synant_translate_cache.json")

DE_FILES = [
    r"c:\gitrepo\udsp\data\wordsa1gode.js",
    r"c:\gitrepo\udsp\data\wordsa2gode.js",
    r"c:\gitrepo\udsp\data\wordsb1gode.js",
    r"c:\gitrepo\udsp\data\wordsb2gode.js",
    r"c:\gitrepo\udsp\data\wordsc1gode.js",
    r"c:\gitrepo\udsp\data\wordsc2gode.js",
]
EN_FILES = [
    r"c:\gitrepo\udsp\data\wordsb2.js",
    r"c:\gitrepo\udsp\data\wordsc1.js",
    r"c:\gitrepo\udsp\data\wordsc2.js",
]

HEADERS = {
    "User-Agent": "TopWordsApp/1.0 (free educational Turkish vocabulary app; "
    "https://udsp.vercel.app)"
}
STYLE_RE = re.compile(r"<style\b[^>]*>.*?</style>", re.DOTALL | re.IGNORECASE)
TAG_RE = re.compile(r"<[^>]+>")
WS_RE = re.compile(r"[ \t]+")

WORD_RE = re.compile(r'word:\s*"((?:\\.|[^"])*)"')
DEF_RE = re.compile(r'definition:\s*"((?:\\.|[^"])*)"')
EX_RE = re.compile(r'example:\s*"((?:\\.|[^"])*)"')
FALLBACK_PREFIX = "No dictionary definition available"
DASHDASH_RE = re.compile(r"\s-\s")


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


EN_LIMITER = RateLimiter(1.0)  # ~1 req/s single-threaded (dictionaryapi.dev throttles above this)
TRANSLATE_LIMITER = RateLimiter(0.2)  # ~5 req/s aggregate (Google Translate free endpoint)


class NotFound(Exception):
    pass


class Transient(Exception):
    pass


def js_unescape(s):
    return s.replace('\\"', '"').replace("\\\\", "\\")


def js_escape(s):
    return s.replace("\\", "\\\\").replace('"', '\\"')


def strip_html(s):
    s = STYLE_RE.sub("", s)
    s = TAG_RE.sub("", s)
    s = s.replace("&nbsp;", " ")
    s = WS_RE.sub(" ", s)
    return s.strip()


def sanitize_piece(s):
    s = (s or "").strip()
    s = DASHDASH_RE.sub(", ", s)
    return s


def ensure_period(s):
    s = (s or "").strip()
    if s and s[-1] not in ".!?":
        s += "."
    return s


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


def fetch_json_retrying(url, limiter, attempts=4, base_delay=1.5):
    last_err = None
    for i in range(attempts):
        limiter.wait()
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=15) as resp:
                return json.loads(resp.read().decode("utf-8"))
        except urllib.error.HTTPError as e:
            if e.code == 404:
                raise NotFound(str(e))
            last_err = e
        except Exception as e:
            last_err = e
        time.sleep(base_delay * (2**i))
    raise Transient(str(last_err))


def fetch_en_definition(word):
    url = "https://api.dictionaryapi.dev/api/v2/entries/en/" + urllib.parse.quote(word)
    data = fetch_json_retrying(url, EN_LIMITER)
    definition = ""
    example = ""
    for entry in data:
        for meaning in entry.get("meanings", []):
            for d in meaning.get("definitions", []):
                if not definition and d.get("definition"):
                    definition = d["definition"].strip()
                if not example and d.get("example"):
                    example = d["example"].strip()
                if definition and example:
                    return {"definition": definition, "example": example}
    return {"definition": definition, "example": example}


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
        time.sleep(base_delay * (2**i))
    raise Transient(str(last_err))


def tr(cache, src, tgt, text):
    return cache.get(f"{src}|{tgt}|{text}", "")


def scan_fallbacks(path):
    """Return (text, entries) where entries is a list of dicts for EVERY
    entry, with def_match/ex_match spans and a `fallback` flag."""
    text = open(path, encoding="utf-8").read()
    words = list(WORD_RE.finditer(text))
    defs = list(DEF_RE.finditer(text))
    exs = list(EX_RE.finditer(text))
    if not (len(words) == len(defs) == len(exs)):
        raise RuntimeError(
            f"{path}: field count mismatch words={len(words)} defs={len(defs)} exs={len(exs)}"
        )
    entries = []
    for wm, dm, em in zip(words, defs, exs):
        d = js_unescape(dm.group(1))
        entries.append(
            {
                "word": js_unescape(wm.group(1)),
                "def_match": dm,
                "ex_match": em,
                "fallback": d.startswith(FALLBACK_PREFIX),
            }
        )
    return text, entries


def apply_replacements(text, entries, new_defs, new_exs):
    edits = []
    for i, e in enumerate(entries):
        if i in new_defs:
            m = e["def_match"]
            edits.append((m.start(1), m.end(1), js_escape(new_defs[i])))
        if i in new_exs:
            m = e["ex_match"]
            edits.append((m.start(1), m.end(1), js_escape(new_exs[i])))
    edits.sort(key=lambda x: x[0], reverse=True)
    for start, end, repl in edits:
        text = text[:start] + repl + text[end:]
    return text


# ------------------------------ ENGLISH ------------------------------

def process_english(limit=None):
    gloss_cache = load_json(EN_GLOSS_CACHE)
    translate_cache = load_json(TRANSLATE_CACHE)

    file_data = {}
    to_fetch = []  # words never fetched (worth trying)
    for path in EN_FILES:
        text, entries = scan_fallbacks(path)
        file_data[path] = (text, entries)
        for e in entries:
            if not e["fallback"]:
                continue
            w = e["word"]
            if w in gloss_cache and not (gloss_cache[w].get("definition") or ""):
                continue  # genuine 404 -- leave fallback
            if w not in gloss_cache:
                to_fetch.append(w)

    to_fetch = sorted(set(to_fetch))
    if limit is not None:
        to_fetch = to_fetch[:limit]
    print(f"[EN] fallback words worth fetching (never fetched): {len(to_fetch)}", flush=True)

    # Fetch single-threaded (dictionaryapi.dev throttles above ~1/s).
    done = fetched = notfound = transient = 0
    start = time.time()
    for w in to_fetch:
        try:
            gloss_cache[w] = fetch_en_definition(w)
            fetched += 1
        except NotFound:
            gloss_cache[w] = {"definition": "", "example": ""}
            notfound += 1
        except Transient:
            transient += 1
        done += 1
        if done % 25 == 0 or done == len(to_fetch):
            save_json(EN_GLOSS_CACHE, gloss_cache)
            elapsed = max(0.001, time.time() - start)
            rate = done / elapsed
            remain = (len(to_fetch) - done) / rate if rate else 0
            print(
                f"[EN fetch] {done}/{len(to_fetch)} ({rate:.2f}/s, ~{remain/60:.1f} min left, "
                f"fetched={fetched}, notfound={notfound}, retry_later={transient})",
                flush=True,
            )
    save_json(EN_GLOSS_CACHE, gloss_cache)

    # Translation jobs for every fallback word that now has a real def.
    jobs = []
    for path, (text, entries) in file_data.items():
        for e in entries:
            if not e["fallback"]:
                continue
            hit = gloss_cache.get(e["word"]) or {}
            gloss = sanitize_piece(hit.get("definition") or "")
            if gloss:
                jobs.append(("en", "tr", gloss))
                ex = sanitize_piece(hit.get("example") or "")
                if ex:
                    jobs.append(("en", "tr", ex))
    translate_all(jobs, translate_cache)

    total_def = total_ex = 0
    for path, (text, entries) in file_data.items():
        new_defs = {}
        new_exs = {}
        for i, e in enumerate(entries):
            if not e["fallback"]:
                continue
            hit = gloss_cache.get(e["word"]) or {}
            gloss = sanitize_piece(hit.get("definition") or "")
            if not gloss:
                continue  # still no def -> leave fallback
            tr_gloss = tr(translate_cache, "en", "tr", gloss)
            new_defs[i] = f"{ensure_period(gloss)} - {ensure_period(tr_gloss) or 'Çeviri bulunamadı.'}"
            total_def += 1
            ex = sanitize_piece(hit.get("example") or "")
            if ex:
                tr_ex = tr(translate_cache, "en", "tr", ex)
                if tr_ex:
                    new_exs[i] = f"{ensure_period(ex)} - {ensure_period(tr_ex)}"
                    total_ex += 1
        if new_defs or new_exs:
            updated = apply_replacements(text, entries, new_defs, new_exs)
            with open(path, "w", encoding="utf-8", newline="\n") as f:
                f.write(updated)
            print(f"[EN write] {os.path.basename(path)}: defs={len(new_defs)} examples={len(new_exs)}", flush=True)
    print(f"[EN] DONE upgraded_defs={total_def} upgraded_examples={total_ex} (remaining fallbacks are genuine 404 non-words)", flush=True)


# ------------------------------ GERMAN ------------------------------

def process_german(limit=None):
    translate_cache = load_json(TRANSLATE_CACHE)

    file_data = {}
    words = []
    for path in DE_FILES:
        text, entries = scan_fallbacks(path)
        file_data[path] = (text, entries)
        for e in entries:
            if e["fallback"]:
                words.append(e["word"])
    words = sorted(set(words))
    if limit is not None:
        # limit applies to number of entries fixed per file in apply phase;
        # here just cap the translation set for a smoke test
        words = words[:limit]
    print(f"[DE] fallback German words to translate: {len(words)}", flush=True)

    jobs = []
    for w in words:
        jobs.append(("de", "en", w))
        jobs.append(("de", "tr", w))
    translate_all(jobs, translate_cache)

    total_def = 0
    for path, (text, entries) in file_data.items():
        new_defs = {}
        for i, e in enumerate(entries):
            if not e["fallback"]:
                continue
            w = e["word"]
            en = sanitize_piece(tr(translate_cache, "de", "en", w))
            tk = sanitize_piece(tr(translate_cache, "de", "tr", w))
            if not en and not tk:
                continue  # translation failed -> leave fallback
            # Guard: if translation just echoes the word unchanged in both,
            # it's not useful -> leave fallback.
            if en.casefold() == w.casefold() and tk.casefold() == w.casefold():
                continue
            new_defs[i] = f"{ensure_period(en) or ensure_period(w)} - {ensure_period(tk) or 'çeviri yok.'}"
            total_def += 1
        if new_defs:
            updated = apply_replacements(text, entries, new_defs, {})
            with open(path, "w", encoding="utf-8", newline="\n") as f:
                f.write(updated)
            print(f"[DE write] {os.path.basename(path)}: defs={len(new_defs)}", flush=True)
    print(f"[DE] DONE upgraded_defs={total_def}", flush=True)


# ------------------------------ shared translate ------------------------------

def translate_all(pairs, cache):
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
    print(f"[translate] distinct strings to translate={len(todo)}", flush=True)
    if not todo:
        return
    done = skipped = 0
    start = time.time()
    with ThreadPoolExecutor(max_workers=5) as ex:
        futures = {ex.submit(translate_one, text, src, tgt): key for src, tgt, text, key in todo}
        for fut in as_completed(futures):
            key = futures[fut]
            try:
                cache[key] = fut.result()
            except Transient:
                skipped += 1
            done += 1
            if done % 200 == 0 or done == len(todo):
                save_json(TRANSLATE_CACHE, cache)
                elapsed = max(0.001, time.time() - start)
                rate = done / elapsed
                remain = (len(todo) - done) / rate if rate else 0
                print(
                    f"[translate] {done}/{len(todo)} ({rate:.1f}/s, ~{remain/60:.1f} min left, {skipped} skipped-for-retry)",
                    flush=True,
                )
    save_json(TRANSLATE_CACHE, cache)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("lang", nargs="?", default="both", choices=["de", "en", "both"])
    ap.add_argument("--limit", type=int, default=None)
    args = ap.parse_args()
    if args.lang in ("en", "both"):
        process_english(limit=args.limit)
    if args.lang in ("de", "both"):
        process_german(limit=args.limit)


if __name__ == "__main__":
    main()
