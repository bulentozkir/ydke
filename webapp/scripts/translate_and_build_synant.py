#!/usr/bin/env python3
"""Builds bilingual (native-language ; Turkish) definition/example fields for
data/synanten.js (English) and data/synantde.js (German), then regenerates
both files.

Pipeline
--------
1. Reuses word/level/synonyms/antonyms from build_synant.py's
   parse_english()/parse_german().
2. Reuses the gloss/example caches already built by scripts/fetch_definitions.py
   (%TEMP%/en_defs_cache.json -- English definitions of English words from
   the Free Dictionary API; %TEMP%/de_defs_cache.json -- English-language
   glosses + German example sentences for German words, from English
   Wiktionary's "de" section). That script can keep running in the
   background while this one runs; this script tolerates a partial cache via
   a synonym-based fallback (see build_definition_pieces()/build_example_pieces()).
3. Translates the pieces that are not already in the target language via the
   free Google Translate web endpoint (translate.googleapis.com/translate_a/
   single). NOTE: newline-batching many strings into one request was tried
   first but is NOT reliable for this content -- Google's `dt=t` endpoint
   sentence-segments the WHOLE joined text linguistically (splits on
   semicolons/clause boundaries within one definition, or merges adjacent
   short phrase-only lines that lack terminal punctuation), so the number of
   segments returned frequently does not match the number of input lines --
   confirmed empirically (e.g. 25 joined definitions came back as 36, 41, or
   even just 1 segment). Per-item requests are the only reliable approach;
   throughput comes from a thread pool + a shared rate limiter instead.
   Results are cached by (src_lang, tgt_lang, text) to
   %TEMP%/synant_translate_cache.json so the script is fully resumable and
   never re-translates an identical string.

Output schema per entry (unchanged keys, new content):
    definition = "<word-language definition>;<Turkish>"
    example    = "<word-language example>;<Turkish>"

Usage: python scripts/translate_and_build_synant.py [--limit N] [en|de|both]
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
from build_synant import parse_english, parse_german, js_escape  # noqa: E402

TEMP = os.environ.get("TEMP", ".")
EN_GLOSS_CACHE = os.path.join(TEMP, "en_defs_cache.json")
DE_GLOSS_CACHE = os.path.join(TEMP, "de_defs_cache.json")
TRANSLATE_CACHE = os.path.join(TEMP, "synant_translate_cache.json")

EN_OUT = r"c:\gitrepo\udsp\data\synanten.js"
DE_OUT = r"c:\gitrepo\udsp\data\synantde.js"

HEADERS = {
    "User-Agent": "TopWordsApp/1.0 (free educational Turkish vocabulary app; "
    "https://udsp.vercel.app)"
}
TRANSLATE_WORKERS = 6


class RateLimiter:
    """Thread-safe leaky-bucket limiter (same design as fetch_definitions.py's
    -- caps the AGGREGATE request rate up front rather than reacting after a
    throttle hits)."""

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


TRANSLATE_LIMITER = RateLimiter(0.15)  # ~6.6 req/s aggregate cap across all workers


class Transient(Exception):
    pass



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
    """pairs: list of (src, tgt, text). Fills `cache[f'{src}|{tgt}|{text}']`
    for every distinct pair via a thread pool (rate-limited in aggregate).
    Resumable: skips anything already cached."""
    todo = []
    seen = set()
    for src, tgt, text in pairs:
        key = f"{src}|{tgt}|{text}"
        if key in cache or key in seen:
            continue
        seen.add(key)
        todo.append((src, tgt, text, key))

    total_todo = len(todo)
    if total_todo == 0:
        print("[translate] nothing to do, all cached", flush=True)
        return
    print(f"[translate] {total_todo} distinct strings to translate", flush=True)

    done = 0
    skipped = 0
    start = time.time()
    with ThreadPoolExecutor(max_workers=TRANSLATE_WORKERS) as ex:
        futures = {ex.submit(translate_one, text, src, tgt): key for src, tgt, text, key in todo}
        for fut in as_completed(futures):
            key = futures[fut]
            try:
                cache[key] = fut.result()
            except Transient:
                skipped += 1  # left OUT of cache -- retried on a future run
            done += 1
            if done % 150 == 0 or done == total_todo:
                save_json(TRANSLATE_CACHE, cache)
                elapsed = time.time() - start
                rate = done / elapsed if elapsed else 0
                remaining = (total_todo - done) / rate if rate else 0
                print(
                    f"[translate] {done}/{total_todo} ({rate:.1f}/s, ~{remaining/60:.1f} min left, {skipped} skipped-for-retry)",
                    flush=True,
                )
    save_json(TRANSLATE_CACHE, cache)
    print(f"[translate] COMPLETE: {done}/{total_todo}, {skipped} skipped-for-retry", flush=True)


def tr(cache, src, tgt, text):
    return cache.get(f"{src}|{tgt}|{text}", "")




SYN_SPLIT_RE = re.compile(r";\s*")


def first_syns(synonyms_field, n=2):
    return [s for s in SYN_SPLIT_RE.split(synonyms_field or "") if s][:n]


def build_english(entries, gloss_cache):
    """Returns list of jobs (src,tgt,text) needed + entries annotated with
    the raw pieces (native text already correct, needs only an en->tr pass)."""
    jobs = []
    for e in entries:
        hit = gloss_cache.get(e["word"], {}) if isinstance(gloss_cache.get(e["word"]), dict) else {}
        gloss = (hit.get("definition") or "").strip()
        if not gloss:
            syns = first_syns(e["synonyms"])
            gloss = ("Related to: " + ", ".join(syns) + ".") if syns else (e["word"] + ".")
        api_example = (hit.get("example") or "").strip()
        example = api_example or f'"{e["word"]}" is a useful word to know.'
        e["_def_native"] = gloss
        e["_ex_native"] = example
        jobs.append(("en", "tr", gloss))
        jobs.append(("en", "tr", example))
    return jobs


def build_german(entries, gloss_cache):
    jobs = []
    for e in entries:
        hit = gloss_cache.get(e["word"], {}) if isinstance(gloss_cache.get(e["word"]), dict) else {}
        eng_gloss = (hit.get("definition") or "").strip()
        api_example = (hit.get("example") or "").strip()

        if eng_gloss:
            e["_def_needs_de_translation"] = eng_gloss  # translate en->de
            jobs.append(("en", "de", eng_gloss))
            jobs.append(("en", "tr", eng_gloss))
        else:
            syns = first_syns(e["synonyms"])
            native = ("Ähnlich wie: " + ", ".join(syns) + ".") if syns else (e["word"] + ".")
            e["_def_native"] = native
            jobs.append(("de", "tr", native))

        if api_example:
            e["_ex_native"] = api_example
            jobs.append(("de", "tr", api_example))
        else:
            native_ex = f'Ich lerne das Wort "{e["word"]}".'
            e["_ex_native"] = native_ex
            jobs.append(("de", "tr", native_ex))
    return jobs


def sanitize_piece(text):
    """The stored format is "<native>;<Turkish>" with a SINGLE semicolon as
    the boundary (per the user's exact requested format). Many source
    definitions are old-dictionary-style and use ";" internally to separate
    multiple senses/near-synonyms (e.g. "to disown; to disinherit."), which
    would otherwise collide with that boundary and make client-side
    splitting ambiguous/wrong. Replace internal semicolons with commas so
    there is always EXACTLY ONE semicolon in the final string."""
    return re.sub(r"\s*;\s*", ", ", text.strip()).strip()


def finalize_english(entries, cache):
    for e in entries:
        d_native = sanitize_piece(e["_def_native"])
        x_native = sanitize_piece(e["_ex_native"])
        d_tr = sanitize_piece(tr(cache, "en", "tr", e["_def_native"]) or d_native)
        x_tr = sanitize_piece(tr(cache, "en", "tr", e["_ex_native"]) or x_native)
        e["definition"] = f"{d_native};{d_tr}"
        e["example"] = f"{x_native};{x_tr}"


def finalize_german(entries, cache):
    for e in entries:
        if "_def_needs_de_translation" in e:
            eng_gloss = e["_def_needs_de_translation"]
            d_native = sanitize_piece(tr(cache, "en", "de", eng_gloss) or eng_gloss)
            d_tr = sanitize_piece(tr(cache, "en", "tr", eng_gloss) or eng_gloss)
        else:
            d_native = sanitize_piece(e["_def_native"])
            d_tr = sanitize_piece(tr(cache, "de", "tr", e["_def_native"]) or d_native)
        x_native = sanitize_piece(e["_ex_native"])
        x_tr = sanitize_piece(tr(cache, "de", "tr", e["_ex_native"]) or x_native)
        e["definition"] = f"{d_native};{d_tr}"
        e["example"] = f"{x_native};{x_tr}"


def write_js(path, varname, entries, source_note):
    lines = []
    lines.append("// Auto-generated by scripts/translate_and_build_synant.py — DO NOT hand-edit entries here.")
    lines.append("// Source: " + source_note)
    lines.append(
        "// Schema: { word, level, definition, example, synonyms, antonyms }. "
        'definition="<native definition>;<Turkish>", example="<native example>;<Turkish>".'
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
    which = "both"
    limit = None
    for arg in sys.argv[1:]:
        if arg in ("en", "de", "both"):
            which = arg
        elif arg.startswith("--limit"):
            limit = int(arg.split("=")[1])

    translate_cache = load_json(TRANSLATE_CACHE)

    if which in ("en", "both"):
        en_entries = parse_english()
        if limit:
            en_entries = en_entries[:limit]
        en_gloss_cache = load_json(EN_GLOSS_CACHE)
        jobs = build_english(en_entries, en_gloss_cache)
        translate_many(jobs, translate_cache)
        finalize_english(en_entries, translate_cache)
        write_js(
            EN_OUT,
            "SYN_ANT_EN",
            en_entries,
            "https://raw.githubusercontent.com/verachell/English-word-lists-synonyms-antonyms/main/syn-ant.csv"
            " (definitions: Free Dictionary API / dictionaryapi.dev; Turkish: Google Translate)",
        )
        withdef = sum(1 for e in en_entries if e["definition"])
        print(f"[EN] wrote {len(en_entries)} entries, {withdef} with definition, to {EN_OUT}")

    if which in ("de", "both"):
        de_entries = parse_german()
        if limit:
            de_entries = de_entries[:limit]
        de_gloss_cache = load_json(DE_GLOSS_CACHE)
        jobs = build_german(de_entries, de_gloss_cache)
        translate_many(jobs, translate_cache)
        finalize_german(de_entries, translate_cache)
        write_js(
            DE_OUT,
            "SYN_ANT_DE",
            de_entries,
            "https://raw.githubusercontent.com/PSeitz/germansynonyms/master/german.syn"
            " (antonyms hand-curated; definitions: English Wiktionary 'de' section machine-translated"
            " to German/Turkish via Google Translate; examples: Wiktionary German usage sentences"
            " machine-translated to Turkish)",
        )
        withdef = sum(1 for e in de_entries if e["definition"])
        print(f"[DE] wrote {len(de_entries)} entries, {withdef} with definition, to {DE_OUT}")
