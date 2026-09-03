#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Shared, resumable machine-translation helper for the content fix passes.

Uses the keyless Google Translate endpoint already relied on elsewhere in this
project (scripts/improve_fallback_definitions.py, translate_and_build_synant.py)
with the same leaky-bucket rate limiter.

The cache lives in scripts/.cache/ rather than %TEMP% -- %TEMP% caches do NOT
survive between sessions, and these passes are long enough that losing the
cache means re-paying the whole rate-limited cost.  It is written
incrementally (every SAVE_EVERY new results and on exit) so killing the job
never loses more than a few seconds of work.
"""
import io
import json
import os
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CACHE_DIR = os.path.join(ROOT, "scripts", ".cache")
HEADERS = {"User-Agent": "Mozilla/5.0 (compatible; udsp-content-fix/1.0)"}
SAVE_EVERY = 200


class RateLimiter(object):
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


LIMITER = RateLimiter(0.16)          # ~6 req/s aggregate, proven safe here


class Transient(Exception):
    pass


class Cache(object):
    def __init__(self, name):
        if not os.path.isdir(CACHE_DIR):
            os.makedirs(CACHE_DIR)
        self.path = os.path.join(CACHE_DIR, name)
        self.lock = threading.Lock()
        self.data = {}
        self._since_save = 0
        if os.path.exists(self.path):
            try:
                with io.open(self.path, encoding="utf-8") as f:
                    self.data = json.load(f)
            except ValueError:
                self.data = {}

    def get(self, key):
        return self.data.get(key)

    def put(self, key, value):
        with self.lock:
            self.data[key] = value
            self._since_save += 1
            due = self._since_save >= SAVE_EVERY
            if due:
                self._since_save = 0
        if due:
            self.save()

    def save(self):
        with self.lock:
            tmp = self.path + ".tmp"
            with io.open(tmp, "w", encoding="utf-8") as f:
                f.write(json.dumps(self.data, ensure_ascii=False))
            os.replace(tmp, self.path)

    def __len__(self):
        return len(self.data)


def translate_one(text, src, tgt, attempts=4, base_delay=0.6):
    q = urllib.parse.quote((text or "").replace("\n", " ").strip())
    url = ("https://translate.googleapis.com/translate_a/single"
           "?client=gtx&sl=%s&tl=%s&dt=t&q=%s" % (src, tgt, q))
    last = None
    for i in range(attempts):
        LIMITER.wait()
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=20) as resp:
                data = json.loads(resp.read().decode("utf-8"))
            return "".join(seg[0] for seg in data[0] if seg and seg[0])
        except Exception as exc:               # noqa: BLE001 - retried below
            last = exc
        time.sleep(base_delay * (2 ** i))
    raise Transient(str(last))


def translate(cache, text, src, tgt):
    """Cached single translation.  Returns "" on persistent failure."""
    text = (text or "").strip()
    if not text:
        return ""
    key = "%s|%s|%s" % (src, tgt, text)
    hit = cache.get(key)
    if hit is not None:
        return hit
    try:
        out = translate_one(text, src, tgt)
    except Transient:
        return ""
    cache.put(key, out)
    return out


def translate_many(cache, items, src, tgt, workers=6, progress=None):
    """items: iterable of text.  Returns {text: translation}.  Threaded."""
    import concurrent.futures

    todo = []
    out = {}
    for t in items:
        t = (t or "").strip()
        if not t or t in out:
            continue
        hit = cache.get("%s|%s|%s" % (src, tgt, t))
        if hit is not None:
            out[t] = hit
        else:
            todo.append(t)
    if not todo:
        return out
    done = [0]
    lock = threading.Lock()

    def work(t):
        try:
            r = translate_one(t, src, tgt)
        except Transient:
            r = ""
        else:
            cache.put("%s|%s|%s" % (src, tgt, t), r)
        with lock:
            done[0] += 1
            if progress and done[0] % 100 == 0:
                sys.stdout.write("\r    %s->%s %d/%d" % (src, tgt, done[0], len(todo)))
                sys.stdout.flush()
        return t, r

    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as ex:
        for t, r in ex.map(work, todo):
            out[t] = r
    cache.save()
    if progress:
        sys.stdout.write("\r    %s->%s %d/%d done\n" % (src, tgt, len(todo), len(todo)))
    return out


def ensure_period(s):
    s = (s or "").strip()
    if s and s[-1] not in ".!?…":
        s += "."
    return s


def titlecase_first(s):
    s = (s or "").strip()
    return s[:1].upper() + s[1:] if s else s
