#!/usr/bin/env python3
"""Fetches English-language definitions + example sentences for every word
in synanten.js (English) and synantde.js (German) from free public APIs,
with disk caching (resumable) and threaded concurrency.

Sources:
- English: Free Dictionary API (api.dictionaryapi.dev), itself sourced from
  Wiktionary under CC BY-SA 3.0.
- German: English Wiktionary's page/definition REST API, "de" section (also
  CC BY-SA) -- returns English-language glosses of German words plus short
  example phrases with translations. There is no equivalent free API that
  returns German words' definitions in TURKISH, so (same as the antonym
  best-effort precedent already used for these two files) definitions here
  are English-only, not the English/Turkish or German/Turkish bilingual
  format used in the hand-curated CEFR word*.js files.

Results are cached to %TEMP%\\en_defs_cache.json / de_defs_cache.json so the
script is safely resumable if interrupted -- re-running it only fetches
words not already in the cache. Use --limit N for a quick smoke test before
committing to a full run.
"""
import os
import re
import sys
import json
import time
import threading
import urllib.request
import urllib.parse
import urllib.error
from concurrent.futures import ThreadPoolExecutor, as_completed

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_synant import parse_english, parse_german  # reuse existing parsers

TEMP = os.environ.get("TEMP", ".")
EN_CACHE = os.path.join(TEMP, "en_defs_cache.json")
DE_CACHE = os.path.join(TEMP, "de_defs_cache.json")

HEADERS = {
    "User-Agent": "TopWordsApp/1.0 (free educational Turkish vocabulary app; "
    "https://udsp.vercel.app)"
}
STYLE_RE = re.compile(r"<style\b[^>]*>.*?</style>", re.DOTALL | re.IGNORECASE)
TAG_RE = re.compile(r"<[^>]+>")
WS_RE = re.compile(r"[ \t]+")
EMPTY = {"definition": "", "example": ""}


class RateLimiter:
    """Thread-safe leaky-bucket limiter: enforces a minimum spacing between
    request STARTS across all worker threads combined, so the aggregate
    request rate to a given API never exceeds min_interval no matter how
    many threads are running concurrently. A first full-scale run relied on
    worker count + retry/backoff alone; that still let bursts through and
    the server throttled us into a death spiral (rate crashed from ~1.6/s to
    ~0.2/s after ~500 requests, ETA ballooning to 7+ hours). Capping the
    aggregate send rate up front avoids ever triggering that throttle.
    """

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


# Empirically-safe aggregate rates (isolated single-word requests at 5/s
# succeeded reliably; kept DE a bit more conservative since it's a shared
# Wikimedia endpoint, not a small dedicated API).
EN_LIMITER = RateLimiter(0.2)  # ~5 req/s
DE_LIMITER = RateLimiter(0.3)  # ~3.3 req/s


def strip_html(s):
    s = STYLE_RE.sub("", s)
    s = TAG_RE.sub("", s)
    s = s.replace("&nbsp;", " ")
    s = WS_RE.sub(" ", s)
    return s.strip()


def fetch_json(url, timeout=10):
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8"))


class NotFound(Exception):
    """Raised for a definitive 404 (word genuinely not in the dictionary) --
    NOT retried, and cached as an empty result (a real, final answer)."""


class Transient(Exception):
    """Raised for a rate-limit/connection/server error -- retried with
    backoff, and if retries are exhausted the word is left OUT of the cache
    entirely so a future re-run of this script will retry it again."""


def fetch_json_retrying(url, limiter, attempts=5, base_delay=1.0):
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
            last_err = e  # 429 / 5xx -- transient, worth retrying
        except Exception as e:
            last_err = e  # timeout / connection reset / etc. -- transient
        time.sleep(base_delay * (2 ** i))
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


def fetch_de_definition(word):
    url = "https://en.wiktionary.org/api/rest_v1/page/definition/" + urllib.parse.quote(word)
    data = fetch_json_retrying(url, DE_LIMITER)
    de_entries = data.get("de")
    if not de_entries:
        raise NotFound("no German section for " + word)
    definition = ""
    example = ""
    for entry in de_entries:
        for d in entry.get("definitions", []):
            if not definition and d.get("definition"):
                # Keep only the first sense/line: nested multi-sense entries
                # otherwise get concatenated into one long, unwieldy string.
                definition = strip_html(d["definition"]).split("\n")[0].strip()
            if not example:
                exs = d.get("examples") or []
                if exs:
                    example = strip_html(exs[0])
            if definition and example:
                return {"definition": definition, "example": example}
    return {"definition": definition, "example": example}


def load_cache(path):
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    return {}


def save_cache(path, cache):
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(cache, f, ensure_ascii=False)
    os.replace(tmp, path)


def run(words, cache_path, fetch_fn, workers, label):
    cache = load_cache(cache_path)
    todo = [w for w in words if w not in cache]
    print("[%s] %d total, %d cached, %d to fetch" % (label, len(words), len(cache), len(todo)), flush=True)
    done = 0
    skipped = 0
    start = time.time()
    with ThreadPoolExecutor(max_workers=workers) as ex:
        futures = {ex.submit(fetch_fn, w): w for w in todo}
        for fut in as_completed(futures):
            w = futures[fut]
            try:
                cache[w] = fut.result()
            except NotFound:
                cache[w] = dict(EMPTY)  # definitive: genuinely not in the dictionary
            except Transient:
                skipped += 1  # left OUT of cache -- retried on the next run
            done += 1
            if done % 100 == 0 or done == len(todo):
                save_cache(cache_path, cache)
                elapsed = time.time() - start
                rate = done / elapsed if elapsed else 0
                remaining = (len(todo) - done) / rate if rate else 0
                print(
                    "[%s] %d/%d done (%.1f/s, ~%.1f min left, %d skipped-for-retry)"
                    % (label, done, len(todo), rate, remaining / 60, skipped),
                    flush=True,
                )
    save_cache(cache_path, cache)
    found = sum(1 for v in cache.values() if v.get("definition"))
    print(
        "[%s] COMPLETE: %d cached, %d with a definition, %d left for a re-run"
        % (label, len(cache), found, skipped),
        flush=True,
    )
    return cache


if __name__ == "__main__":
    which = "both"
    limit = None
    for arg in sys.argv[1:]:
        if arg in ("en", "de", "both"):
            which = arg
        elif arg.startswith("--limit"):
            limit = int(arg.split("=")[1])

    if which in ("en", "both"):
        en_words = [e["word"] for e in parse_english()]
        if limit:
            en_words = en_words[:limit]
        run(en_words, EN_CACHE, fetch_en_definition, workers=4, label="EN")

    if which in ("de", "both"):
        de_words = [e["word"] for e in parse_german()]
        if limit:
            de_words = de_words[:limit]
        run(de_words, DE_CACHE, fetch_de_definition, workers=3, label="DE")
