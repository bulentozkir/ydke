#!/usr/bin/env python3
"""Fix placeholder / low-quality definitions (and, where the same API call
already provides one for free, examples) in the German (*gode.js) and
English (wordsb2.js/wordsc1.js/wordsc2.js) word-list files.

Two categories of broken `definition` fields are fixed:

1. TRUE PLACEHOLDER -- the exact auto-generated generic text left behind by
   scripts/expand_gode_wordlists.py / scripts/expand_english_wordlists.py:
     German:  "German (noun|verb|adjective) from open-source list. - ..."
     English: "English vocabulary word from open-source list. - ..."
   These need a FRESH dictionary lookup (Wiktionary "de"-section for German,
   dictionaryapi.dev for English) + a Turkish translation, for both the
   definition and (when the API provides one) the example.

2. PARTIAL (German *gode.js files only) -- an earlier, incomplete run of
   scripts/enrich_gode_definitions.py already replaced the definition with
   a bare English gloss but never added a Turkish half or the " - "
   separator (e.g. "climbing plant, climber, creeper, vine"). These only
   need a translation of the EXISTING text (no re-fetch) + reformatting
   into "Gloss. - Çeviri."; the example (already a safe, generic
   placeholder sentence) is left untouched.

Untouched: any entry whose definition already contains " - " and doesn't
match the placeholder pattern (i.e. every hand-authored / already-fixed
entry), and files with their OWN different, already-correct conventions
(phrasalverbsen.js, synanten.js, synantde.js) are never touched by this
script at all.

Output format (matches the established convention already used by every
correct entry in these files):
    definition = "<English gloss>. - <Turkish>."
    example    = "<German or English sentence>. - <Turkish>." (only
                 overwritten when a REAL example was fetched; otherwise the
                 existing example string -- generic or real -- is left as-is)

Caches (reused from earlier work in this project -- same word -> gloss
mapping is valid regardless of which file/project consumes it):
    %TEMP%\\de_defs_cache.json          (word -> {definition, example})
    %TEMP%\\en_defs_cache.json          (word -> {definition, example})
    %TEMP%\\synant_translate_cache.json (\"{src}|{tgt}|{text}\" -> translation)

Usage: python scripts/fix_placeholder_definitions.py [--limit N] [de|en|both]
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
DE_GLOSS_CACHE = os.path.join(TEMP, "de_defs_cache.json")
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
PLACEHOLDER_DE_RE = re.compile(r"^German (noun|verb|adjective) from open-source list\.")
PLACEHOLDER_EN_RE = re.compile(r"^English vocabulary word from open-source list\.")
DASHDASH_RE = re.compile(r"\s-\s")  # internal " - " that would be ambiguous once wrapped


class RateLimiter:
    """Thread-safe leaky-bucket limiter: caps the AGGREGATE request rate to
    a given API across all worker threads (same design proven in
    fetch_definitions.py / translate_and_build_synant.py)."""

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


EN_LIMITER = RateLimiter(1.5)  # ~0.67 req/s -- dictionaryapi.dev throttles hard well below 5/s
# and even below 2/s once triggered; confirmed via live diagnostics that a single-
# threaded ~1 req/s pace sustains zero HTTP 429s, so keep EN fetching single-threaded too.
DE_LIMITER = RateLimiter(0.3)  # ~3.3 req/s (en.wiktionary.org REST)
TRANSLATE_LIMITER = RateLimiter(0.15)  # ~6.6 req/s (Google Translate free endpoint)


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
    """Remove ambiguous internal ' - ' from a fetched/translated piece
    before it gets wrapped in our own "X. - Y." format (same rationale as
    translate_and_build_synant.py's sanitize_piece for ';')."""
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
                definition = strip_html(d["definition"]).split("\n")[0].strip()
            if not example:
                exs = d.get("examples") or []
                if exs:
                    example = strip_html(exs[0])
            if definition and example:
                return {"definition": definition, "example": example}
    if not definition:
        raise NotFound("no usable definition for " + word)
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


def scan_file(path):
    """Returns list of dicts: {word, def_match, ex_match, kind} where kind
    is one of "ok" (leave alone), "placeholder" (needs fresh fetch), or
    "partial" (German-only, translate existing gloss)."""
    text = open(path, encoding="utf-8").read()
    words = list(WORD_RE.finditer(text))
    defs = list(DEF_RE.finditer(text))
    exs = list(EX_RE.finditer(text))
    if not (len(words) == len(defs) == len(exs)):
        raise RuntimeError(
            f"{path}: field count mismatch words={len(words)} defs={len(defs)} exs={len(exs)}"
        )
    is_de = "gode" in os.path.basename(path)
    entries = []
    for wm, dm, em in zip(words, defs, exs):
        d = js_unescape(dm.group(1))
        kind = "ok"
        if PLACEHOLDER_DE_RE.match(d) or PLACEHOLDER_EN_RE.match(d):
            kind = "placeholder"
        elif is_de and " - " not in d:
            kind = "partial"
        entries.append(
            {
                "word": js_unescape(wm.group(1)),
                "def_match": dm,
                "ex_match": em,
                "kind": kind,
            }
        )
    return text, entries


def apply_replacements(text, entries, new_defs, new_exs):
    """new_defs/new_exs: {index: new_unescaped_string}. Applies all
    replacements by exact original match span, processing from the END of
    the file backward so earlier offsets stay valid after length changes."""
    edits = []  # (start, end, replacement_text_including_quotes)
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


def fetch_all(words, fetch_fn, cache, label):
    todo = [w for w in words if w not in cache]
    print(f"[{label}] unique words={len(words)} cached={len(words)-len(todo)} to_fetch={len(todo)}", flush=True)
    if not todo:
        return
    done = fetched = notfound = transient = 0
    start = time.time()
    with ThreadPoolExecutor(max_workers=1 if label == "en-fetch" else 6) as ex:
        futures = {ex.submit(fetch_fn, w): w for w in todo}
        for fut in as_completed(futures):
            w = futures[fut]
            try:
                cache[w] = fut.result()
                fetched += 1
            except NotFound:
                cache[w] = {"definition": "", "example": ""}
                notfound += 1
            except Transient:
                transient += 1
            done += 1
            if done % 50 == 0 or done == len(todo):
                save_json(DE_GLOSS_CACHE if label == "de-fetch" else EN_GLOSS_CACHE, cache)
                elapsed = max(0.001, time.time() - start)
                rate = done / elapsed
                remain = (len(todo) - done) / rate if rate else 0
                print(
                    f"[{label}] {done}/{len(todo)} ({rate:.1f}/s, ~{remain/60:.1f} min left, "
                    f"fetched={fetched}, notfound={notfound}, retry_later={transient})",
                    flush=True,
                )
    save_json(DE_GLOSS_CACHE if label == "de-fetch" else EN_GLOSS_CACHE, cache)


def translate_all(pairs, cache):
    todo = []
    seen = set()
    for src, tgt, text in pairs:
        key = f"{src}|{tgt}|{text}"
        if key in cache or key in seen or not text:
            continue
        seen.add(key)
        todo.append((src, tgt, text, key))
    print(f"[translate] distinct strings to translate={len(todo)}", flush=True)
    if not todo:
        return
    done = skipped = 0
    start = time.time()
    with ThreadPoolExecutor(max_workers=8) as ex:
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


def tr(cache, src, tgt, text):
    return cache.get(f"{src}|{tgt}|{text}", "")


NOTFOUND_DEF = (
    "No dictionary definition available for this word. - Bu kelime için sözlük tanımı bulunamadı."
)


def process_language(files, is_de, limit=None, skip_fetch=False):
    label_fetch = "de-fetch" if is_de else "en-fetch"
    gloss_cache_path = DE_GLOSS_CACHE if is_de else EN_GLOSS_CACHE
    gloss_cache = load_json(gloss_cache_path)
    translate_cache = load_json(TRANSLATE_CACHE)

    file_data = {}
    placeholder_words = []
    partial_count = 0
    for path in files:
        text, entries = scan_file(path)
        if limit is not None:
            # keep classification counts real, but only actually FIX the
            # first `limit` broken entries per file (smoke-test mode)
            fixed_so_far = 0
            for e in entries:
                if e["kind"] != "ok" and fixed_so_far >= limit:
                    e["kind"] = "ok"
                elif e["kind"] != "ok":
                    fixed_so_far += 1
        file_data[path] = (text, entries)
        for e in entries:
            if e["kind"] == "placeholder":
                placeholder_words.append(e["word"])
            elif e["kind"] == "partial":
                partial_count += 1

    placeholder_words = sorted(set(placeholder_words))
    print(f"[{'DE' if is_de else 'EN'}] placeholder unique words={len(placeholder_words)}, partial entries={partial_count}", flush=True)

    fetch_fn = fetch_de_definition if is_de else fetch_en_definition
    if skip_fetch:
        uncached = [w for w in placeholder_words if w not in gloss_cache]
        print(f"[{'DE' if is_de else 'EN'}] --skip-fetch: leaving {len(uncached)} uncached words as fallback", flush=True)
    else:
        fetch_all(placeholder_words, fetch_fn, gloss_cache, label_fetch)

    # Build translation job list.
    jobs = []
    for path, (text, entries) in file_data.items():
        for e in entries:
            if e["kind"] == "placeholder":
                hit = gloss_cache.get(e["word"]) or {}
                gloss = sanitize_piece(hit.get("definition") or "")
                if gloss:
                    jobs.append(("en", "tr", gloss))
                api_example = sanitize_piece(hit.get("example") or "")
                if api_example:
                    jobs.append(("de" if is_de else "en", "tr", api_example))
            elif e["kind"] == "partial":
                existing_def = js_unescape(e["def_match"].group(1))
                jobs.append(("en", "tr", sanitize_piece(existing_def)))

    translate_all(jobs, translate_cache)

    # Apply fixes per file.
    total_fixed_def = 0
    total_fixed_ex = 0
    total_notfound = 0
    for path, (text, entries) in file_data.items():
        new_defs = {}
        new_exs = {}
        for i, e in enumerate(entries):
            if e["kind"] == "placeholder":
                hit = gloss_cache.get(e["word"]) or {}
                gloss = sanitize_piece(hit.get("definition") or "")
                if gloss:
                    tr_gloss = tr(translate_cache, "en", "tr", gloss)
                    new_defs[i] = f"{ensure_period(gloss)} - {ensure_period(tr_gloss) or 'Çeviri bulunamadı.'}"
                else:
                    new_defs[i] = NOTFOUND_DEF
                    total_notfound += 1
                api_example = sanitize_piece(hit.get("example") or "")
                if api_example:
                    tr_ex = tr(translate_cache, "de" if is_de else "en", "tr", api_example)
                    if tr_ex:
                        new_exs[i] = f"{ensure_period(api_example)} - {ensure_period(tr_ex)}"
                        total_fixed_ex += 1
                total_fixed_def += 1
            elif e["kind"] == "partial":
                existing_def = sanitize_piece(js_unescape(e["def_match"].group(1)))
                tr_def = tr(translate_cache, "en", "tr", existing_def)
                new_defs[i] = f"{ensure_period(existing_def)} - {ensure_period(tr_def) or 'Çeviri bulunamadı.'}"
                total_fixed_def += 1

        if not new_defs and not new_exs:
            continue
        updated = apply_replacements(text, entries, new_defs, new_exs)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(updated)
        print(f"[write] {path}: fixed_defs={len(new_defs)} fixed_examples={len(new_exs)}", flush=True)

    print(
        f"[{'DE' if is_de else 'EN'}] DONE total_fixed_def={total_fixed_def} "
        f"total_examples_upgraded={total_fixed_ex} total_notfound_fallback={total_notfound}",
        flush=True,
    )


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("lang", nargs="?", default="both", choices=["de", "en", "both"])
    ap.add_argument("--limit", type=int, default=None, help="Only fix first N broken entries per file (smoke test)")
    ap.add_argument("--skip-fetch", action="store_true", help="Do not fetch missing definitions; apply cache-only, leaving uncached words as fallback")
    args = ap.parse_args()

    if args.lang in ("de", "both"):
        process_language(DE_FILES, is_de=True, limit=args.limit, skip_fetch=args.skip_fetch)
    if args.lang in ("en", "both"):
        process_language(EN_FILES, is_de=False, limit=args.limit, skip_fetch=args.skip_fetch)


if __name__ == "__main__":
    main()
