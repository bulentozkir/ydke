#!/usr/bin/env python3
"""Replace generic auto-added entries in GODE files with dictionary data.

Target files:
- data/wordsa1gode.js
- data/wordsa2gode.js
- data/wordsb1gode.js
- data/wordsb2gode.js
- data/wordsc1gode.js
- data/wordsc2gode.js

Data source:
- https://en.wiktionary.org/api/rest_v1/page/definition/{word}

Behavior:
- Keeps `word`, `pos`, `level` unchanged.
- Replaces only generic placeholder `definition` / `example` fields.
- Uses cache in %TEMP% to avoid re-fetching on reruns.
"""

import json
import os
import re
import time
import threading
import urllib.error
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed

FILES = [
    r"c:\gitrepo\udsp\data\wordsa1gode.js",
    r"c:\gitrepo\udsp\data\wordsa2gode.js",
    r"c:\gitrepo\udsp\data\wordsb1gode.js",
    r"c:\gitrepo\udsp\data\wordsb2gode.js",
    r"c:\gitrepo\udsp\data\wordsc1gode.js",
    r"c:\gitrepo\udsp\data\wordsc2gode.js",
]

TEMP = os.environ.get("TEMP", ".")
CACHE_PATH = os.path.join(TEMP, "gode_de_defs_cache.json")
PREV_DE_CACHE = os.path.join(TEMP, "de_defs_cache.json")

HEADERS = {
    "User-Agent": "TopWordsApp/1.0 (educational vocabulary app; https://udsp.vercel.app)"
}

OBJECT_RE = re.compile(
    r"\{\s*\n"
    r"\s*word:\s*\"(?P<word>(?:\\.|[^\"])*)\",\s*\n"
    r"\s*pos:\s*\"(?P<pos>(?:\\.|[^\"])*)\",\s*\n"
    r"\s*level:\s*\"(?P<level>(?:\\.|[^\"])*)\",\s*\n"
    r"\s*definition:\s*\"(?P<definition>(?:\\.|[^\"])*)\",\s*\n"
    r"\s*example:\s*\"(?P<example>(?:\\.|[^\"])*)\",\s*\n"
    r"\s*\},",
    re.MULTILINE,
)
GENERIC_RE = re.compile(r"^German (noun|verb|adjective) from open-source list\. -", re.IGNORECASE)
STYLE_RE = re.compile(r"<style\b[^>]*>.*?</style>", re.DOTALL | re.IGNORECASE)
TAG_RE = re.compile(r"<[^>]+>")
WS_RE = re.compile(r"[ \t]+")


class NotFound(Exception):
    pass


class Transient(Exception):
    pass


class RateLimiter:
    def __init__(self, min_interval):
        self.min_interval = min_interval
        self.lock = threading.Lock()
        self.next_time = 0.0

    def wait(self):
        with self.lock:
            now = time.time()
            start = max(now, self.next_time)
            self.next_time = start + self.min_interval
        delay = start - now
        if delay > 0:
            time.sleep(delay)


LIMITER = RateLimiter(0.3)  # ~3.3 req/s aggregate


def js_unescape(s: str) -> str:
    return s.replace('\\"', '"').replace("\\\\", "\\")


def js_escape(s: str) -> str:
    return s.replace("\\", "\\\\").replace('"', '\\"')


def strip_html(s: str) -> str:
    s = STYLE_RE.sub("", s)
    s = TAG_RE.sub("", s)
    s = s.replace("&nbsp;", " ")
    s = WS_RE.sub(" ", s)
    return s.strip()


def load_cache(path: str) -> dict:
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
        return {
            k: {
                "definition": (v.get("definition") or "").strip(),
                "example": (v.get("example") or "").strip(),
            }
            for k, v in data.items()
            if isinstance(v, dict)
        }
    return {}


def save_cache(path: str, cache: dict) -> None:
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(cache, f, ensure_ascii=False)
    os.replace(tmp, path)


def seed_cache(cache: dict) -> dict:
    if not os.path.exists(PREV_DE_CACHE):
        return cache
    prev = load_cache(PREV_DE_CACHE)
    for k, v in prev.items():
        if k not in cache:
            cache[k] = v
    return cache


def fetch_de(word: str, attempts: int = 3) -> dict:
    url = "https://en.wiktionary.org/api/rest_v1/page/definition/" + urllib.parse.quote(word)
    last_err = None
    for i in range(attempts):
        LIMITER.wait()
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=8) as resp:
                data = json.loads(resp.read().decode("utf-8"))
            break
        except urllib.error.HTTPError as e:
            if e.code == 404:
                raise NotFound()
            last_err = e
        except Exception as e:
            last_err = e
        time.sleep(0.8 * (2**i))
    else:
        raise Transient(str(last_err))

    de_entries = data.get("de")
    if not de_entries:
        raise NotFound()

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
    return {"definition": definition, "example": example}


def collect_targets() -> tuple[dict, list[str], int]:
    file_text = {}
    words = []
    total_generic = 0
    seen = set()
    for path in FILES:
        text = open(path, encoding="utf-8").read()
        file_text[path] = text
        for m in OBJECT_RE.finditer(text):
            d = js_unescape(m.group("definition"))
            if not GENERIC_RE.match(d):
                continue
            total_generic += 1
            w = js_unescape(m.group("word"))
            if w not in seen:
                seen.add(w)
                words.append(w)
    return file_text, words, total_generic


def fetch_missing(cache: dict, words: list[str]) -> tuple[int, int, int]:
    todo = [w for w in words if w not in cache]
    print(f"[fetch] unique target words={len(words)}, cached={len(words)-len(todo)}, to_fetch={len(todo)}", flush=True)
    if not todo:
        return 0, 0, 0

    fetched = 0
    notfound = 0
    transient = 0
    done = 0
    start = time.time()

    with ThreadPoolExecutor(max_workers=3) as ex:
        futures = {ex.submit(fetch_de, w): w for w in todo}
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
            if done % 20 == 0 or done == len(todo):
                save_cache(CACHE_PATH, cache)
                elapsed = max(0.001, time.time() - start)
                rate = done / elapsed
                remain = (len(todo) - done) / rate
                print(
                    f"[fetch] {done}/{len(todo)} done ({rate:.1f}/s, ~{remain/60:.1f} min left, fetched={fetched}, notfound={notfound}, retry_later={transient})",
                    flush=True,
                )
    save_cache(CACHE_PATH, cache)
    return fetched, notfound, transient


def patch_files(file_text: dict, cache: dict) -> tuple[int, int]:
    replaced = 0
    with_real_example = 0

    for path, original in file_text.items():
        def repl(m: re.Match) -> str:
            nonlocal replaced, with_real_example
            word = js_unescape(m.group("word"))
            definition = js_unescape(m.group("definition"))
            example = js_unescape(m.group("example"))

            if not GENERIC_RE.match(definition):
                return m.group(0)

            hit = cache.get(word) or {}
            new_def = (hit.get("definition") or "").strip()
            new_ex = (hit.get("example") or "").strip()
            if not new_def:
                return m.group(0)

            replaced += 1
            if new_ex:
                with_real_example += 1

            final_ex = new_ex if new_ex else example
            return (
                "{\n"
                f'    word: "{js_escape(word)}",\n'
                f'    pos: "{m.group("pos")}",\n'
                f'    level: "{m.group("level")}",\n'
                f'    definition: "{js_escape(new_def)}",\n'
                f'    example: "{js_escape(final_ex)}",\n'
                "  },"
            )

        updated = OBJECT_RE.sub(repl, original)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(updated)

    return replaced, with_real_example


def main() -> None:
    cache = load_cache(CACHE_PATH)
    cache = seed_cache(cache)
    save_cache(CACHE_PATH, cache)

    file_text, words, total_generic = collect_targets()
    print(f"[scan] generic entries in files={total_generic}; unique generic words={len(words)}", flush=True)

    fetched, notfound, transient = fetch_missing(cache, words)
    replaced, with_real_example = patch_files(file_text, cache)

    print("[done] fetched_new=", fetched, "notfound=", notfound, "retry_later=", transient, flush=True)
    print("[done] replaced_generic_definitions=", replaced, "with_real_dictionary_example=", with_real_example, flush=True)


if __name__ == "__main__":
    main()
