#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""synant_metadata.py

Shared helpers for assigning REAL CEFR levels + topical categories to the
data/synant*.js word-morph datasets (previously all entries had the
placeholder `level:"SYN"` and no `category` field at all).

Level assignment
-----------------
Every entry's word is ranked against this project's existing per-language
frequency source (same sources/URLs already used by expand_words_en_cefr.py
/ expand_words_de_cefr.py / expand_french_wordlists.py). Words are sorted
by frequency rank (most frequent first; words absent from the frequency
list are pushed to the very end, i.e. treated as the rarest/most advanced)
and split into 6 equal-size contiguous bands -> A1..C2. This is the same
"frequency rank -> CEFR band" philosophy already established elsewhere in
this project (see expand_words_en_cefr.py's TARGETS rank ceilings), just
applied as a one-time SPLIT of an already-fixed word set instead of a
"harvest new words below rank N" growth cap.

Category assignment
--------------------
Reuses scripts/classify_word_categories.py's `classify()` (the existing
27-domain keyword-matching taxonomy already used for every other data/*.js
file) against the ENGLISH form of each entry's native-language definition.
English and French synant definitions are already English text (the
French file's own header confirms `definition` is an English gloss, not a
native-French definition) so they're classified directly. German
definitions are native German -- machine-translated to English ONCE
(cached, reusing the same synant_translate_cache.json + Google Translate
free-endpoint pattern already established in translate_and_build_synant.py)
purely so the SAME classifier can be reused; the STORED German definition
text itself is never changed by this translation.
"""
import os
import re
import sys
import json
import time
import threading
import urllib.request
import urllib.parse
from concurrent.futures import ThreadPoolExecutor, as_completed

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from classify_word_categories import classify  # noqa: E402

TEMP = os.environ.get("TEMP", ".")
HEADERS = {"User-Agent": "TopWordsApp/1.0 (educational; https://udsp.vercel.app)"}

FREQ_SOURCES = {
    "en": (
        os.path.join(TEMP, "en_freq_50k.txt"),
        "https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2018/en/en_50k.txt",
    ),
    "de": (
        os.path.join(TEMP, "de_full.txt"),
        "https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2018/de/de_full.txt",
    ),
    "fr": (
        os.path.join(TEMP, "fr_full.txt"),
        "https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2018/fr/fr_full.txt",
    ),
}

CEFR_BANDS = ["A1", "A2", "B1", "B2", "C1", "C2"]
FR_ARTICLE_RE = re.compile(r"^(le|la|les|l['\u2019])\s*", re.IGNORECASE)


def ensure_freq_file(lang):
    path, url = FREQ_SOURCES[lang]
    if not os.path.exists(path):
        print("[freq] downloading %s ..." % os.path.basename(path), flush=True)
        req = urllib.request.Request(url, headers=HEADERS)
        with urllib.request.urlopen(req, timeout=120) as resp:
            data = resp.read()
        with open(path, "wb") as f:
            f.write(data)
    return path


def load_rank_map(lang):
    path = ensure_freq_file(lang)
    rank = {}
    with open(path, encoding="utf-8") as f:
        for i, line in enumerate(f):
            parts = line.split()
            if not parts:
                continue
            w = parts[0].strip().casefold()
            if w not in rank:
                rank[w] = i
    return rank


def lookup_key(word, lang):
    w = word.strip()
    if lang == "fr":
        w = FR_ARTICLE_RE.sub("", w)
    return w.casefold()


def assign_levels(words, lang):
    """words: list of raw word strings (order preserved, duplicates OK).
    Returns a list of the same length with an assigned CEFR level string
    for each word, via a frequency-rank sextile split across the WHOLE
    list (not a per-word absolute threshold) so the result is always a
    meaningful 6-way spread regardless of the language's frequency-list
    coverage."""
    rank_map = load_rank_map(lang)
    n = len(words)
    ranks = [rank_map.get(lookup_key(w, lang), float("inf")) for w in words]
    order = sorted(range(n), key=lambda i: (ranks[i], i))
    levels = [None] * n
    band_count = len(CEFR_BANDS)
    for pos, i in enumerate(order):
        band = min(pos * band_count // n, band_count - 1)
        levels[i] = CEFR_BANDS[band]
    return levels


TRANSLATE_CACHE_PATH = os.path.join(TEMP, "synant_translate_cache.json")


class RateLimiter:
    """Thread-safe leaky-bucket limiter -- same design already proven safe
    at ~6.6 req/s aggregate in translate_and_build_synant.py."""

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


_LIMITER = RateLimiter(0.15)


def _load_json(path):
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    return {}


def _save_json(path, data):
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False)
    os.replace(tmp, path)


def _translate_one(text, src, tgt, attempts=4, base_delay=0.6):
    q = urllib.parse.quote(text.replace("\n", " ").strip())
    url = (
        "https://translate.googleapis.com/translate_a/single"
        "?client=gtx&sl=%s&tl=%s&dt=t&q=%s" % (src, tgt, q)
    )
    for i in range(attempts):
        _LIMITER.wait()
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=20) as resp:
                data = json.loads(resp.read().decode("utf-8"))
            return "".join(seg[0] for seg in data[0])
        except Exception:
            time.sleep(base_delay * (2 ** i))
    return None


def translate_de_to_en(texts):
    """texts: iterable of German strings (dupes OK). Returns {text: english}
    for every distinct input, using + growing the shared synant translate
    cache (resumable, safe to re-run)."""
    cache = _load_json(TRANSLATE_CACHE_PATH)
    uniq = sorted(set(texts))
    todo = [(t, "de|en|" + t) for t in uniq if ("de|en|" + t) not in cache]
    if todo:
        print("[translate-for-category] %d distinct German definitions -> English" % len(todo), flush=True)
        done = 0
        with ThreadPoolExecutor(max_workers=6) as ex:
            futs = {ex.submit(_translate_one, t, "de", "en"): (t, key) for t, key in todo}
            for fut in as_completed(futs):
                t, key = futs[fut]
                try:
                    cache[key] = fut.result() or t
                except Exception:
                    cache[key] = t
                done += 1
                if done % 300 == 0 or done == len(todo):
                    _save_json(TRANSLATE_CACHE_PATH, cache)
                    print("[translate-for-category] %d/%d" % (done, len(todo)), flush=True)
        _save_json(TRANSLATE_CACHE_PATH, cache)
    return {t: cache.get("de|en|" + t, t) for t in uniq}


def assign_categories(native_definitions, lang):
    """native_definitions: list of native-language definition strings (the
    pre-';' native half only, NOT the Turkish half). Returns a list of
    category domain strings, same order/length."""
    if lang == "de":
        en_map = translate_de_to_en(native_definitions)
        english_texts = [en_map.get(d, d) for d in native_definitions]
    else:
        english_texts = native_definitions
    return [classify(t) for t in english_texts]
