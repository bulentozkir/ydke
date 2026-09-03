#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""fix_missing_turkish.py

Adds missing Turkish translations found across data/*.js (reported by a
user: phrasal verbs like "add up to" showed a definition with no Turkish
half at all in the Quiz/Flashcard views). Root cause was two independent,
unrelated gaps:

1. data/phrasalverbsen.js -- EVERY `definition` field (3213/3213) was
   always English-only. The file's own header says it was extracted
   straight from a PDF word list; only `example` sentences ever got a
   Turkish translation pass, `definition` never did.
2. data/wordsXXgode.js (the 6 self-authored German CEFR files) -- a small
   subset of `example` fields (~375 total across all 6 files) are
   German-only, missing their Turkish half (most examples in these files
   ARE already correctly translated; this is a gap in a handful of
   entries only, not the whole file).

(data/synant*.js files are NOT affected/touched -- they intentionally use
a ";"-separated "<native>;<Turkish>" format instead of " - ", per their own
header comments, and already have Turkish content for the vast majority of
entries in that format.)

Both fixes append " - <Turkish translation>" to the existing field value,
matching this project's universal `"<text>. - <Türkçe>."` convention,
translating via the free Google Translate endpoint (same proven
rate-limited pattern as expand_french_wordlists.py /
translate_and_build_synant.py). Fully resumable via a %TEMP% cache.

Usage:
  python scripts/fix_missing_turkish.py --dry-run   (report counts only, no network/writes)
  python scripts/fix_missing_turkish.py             (full run)
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

TEMP = os.environ.get("TEMP", ".")
CACHE_PATH = os.path.join(TEMP, "missing_turkish_translate_cache.json")
DATA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data")

HEADERS = {
    "User-Agent": "TopWordsApp/1.0 (free educational Turkish vocabulary app; "
    "https://udsp.vercel.app)"
}


class RateLimiter:
    """Thread-safe leaky-bucket limiter -- same design used throughout this
    project's other translation scripts (proven safe at this rate)."""

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


LIMITER = RateLimiter(0.15)  # ~6.6 req/s aggregate, proven safe elsewhere in this project


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


def translate_one(text, src, tgt, attempts=4, base_delay=0.6):
    q = urllib.parse.quote(text.replace("\n", " ").strip())
    url = (
        "https://translate.googleapis.com/translate_a/single"
        f"?client=gtx&sl={src}&tl={tgt}&dt=t&q={q}"
    )
    last_err = None
    for i in range(attempts):
        LIMITER.wait()
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=20) as resp:
                data = json.loads(resp.read().decode("utf-8"))
            return "".join(seg[0] for seg in data[0])
        except Exception as e:
            last_err = e
        time.sleep(base_delay * (2 ** i))
    raise RuntimeError(str(last_err))


def js_escape(s):
    return s.replace("\\", "\\\\").replace('"', '\\"').replace("\n", " ").strip()


def field_value_re(field_name):
    return re.compile(field_name + r':\s*"((?:[^"\\]|\\.)*)"')


def collect_missing(path, field_name):
    with open(path, encoding="utf-8") as f:
        content = f.read()
    missing = []
    seen = set()
    for m in field_value_re(field_name).finditer(content):
        raw = m.group(1)
        if not raw.strip() or " - " in raw:
            continue
        if raw in seen:
            continue
        seen.add(raw)
        missing.append(raw)
    return content, missing


def translate_missing(missing, src_lang, cache, label):
    todo = [t for t in missing if f"{src_lang}|tr|{t}" not in cache]
    print(f"[{label}] {len(missing)} distinct strings missing Turkish, {len(todo)} not yet cached")
    if not todo:
        return
    done = 0
    start = time.time()
    with ThreadPoolExecutor(max_workers=6) as ex:
        futures = {ex.submit(translate_one, t, src_lang, "tr"): t for t in todo}
        for fut in as_completed(futures):
            t = futures[fut]
            key = f"{src_lang}|tr|{t}"
            try:
                cache[key] = fut.result()
            except Exception as e:
                print(f"  ! failed: {t!r}: {e}")
            done += 1
            if done % 100 == 0 or done == len(todo):
                save_json(CACHE_PATH, cache)
                elapsed = time.time() - start
                rate = done / elapsed if elapsed else 0
                remaining = (len(todo) - done) / rate if rate else 0
                print(f"  [{label}] {done}/{len(todo)} ({rate:.1f}/s, ~{remaining/60:.1f} min left)")
    save_json(CACHE_PATH, cache)


def apply_fix(path, field_name, src_lang, cache):
    content, missing = collect_missing(path, field_name)
    if not missing:
        return 0

    def repl(m):
        raw = m.group(1)
        if not raw.strip() or " - " in raw:
            return m.group(0)
        tr = cache.get(f"{src_lang}|tr|{raw}")
        if not tr:
            return m.group(0)  # translation failed/missing -- leave untouched, resumable later
        new_val = raw.rstrip() + " - " + tr.strip()
        return field_name + ': "' + js_escape(new_val) + '"'

    new_content, n = field_value_re(field_name).subn(repl, content)
    with open(path, "w", encoding="utf-8") as f:
        f.write(new_content)
    return n


def main():
    dry_run = "--dry-run" in sys.argv
    cache = load_json(CACHE_PATH)

    # 1. phrasalverbsen.js -- definition field, source language English.
    pv_path = os.path.join(DATA_DIR, "phrasalverbsen.js")
    _, pv_missing = collect_missing(pv_path, "definition")
    translate_missing(pv_missing, "en", cache, "phrasalverbsen.js/definition")

    # 2. wordsXXgode.js -- example field, source language German.
    gode_files = [
        "wordsa1gode.js",
        "wordsa2gode.js",
        "wordsb1gode.js",
        "wordsb2gode.js",
        "wordsc1gode.js",
        "wordsc2gode.js",
    ]
    gode_missing = {}
    for fname in gode_files:
        path = os.path.join(DATA_DIR, fname)
        _, missing = collect_missing(path, "example")
        gode_missing[fname] = missing
        translate_missing(missing, "de", cache, fname + "/example")

    if dry_run:
        print("[dry-run] stopping before writing any files")
        return

    n = apply_fix(pv_path, "definition", "en", cache)
    print(f"[write] {pv_path}: fixed {n} definitions")

    for fname in gode_files:
        path = os.path.join(DATA_DIR, fname)
        n = apply_fix(path, "example", "de", cache)
        print(f"[write] {path}: fixed {n} examples")


if __name__ == "__main__":
    main()
