#!/usr/bin/env python3
"""Boosts antonym coverage in data/synantde.js (German) from a handful of
hand-curated pairs up to a much larger target, by leveraging the antonym
data ALREADY present in data/synanten.js (English) -- which the source
syn-ant.csv gave real antonyms for ~5,200 of its 5,886 entries -- rather
than sourcing a brand-new German antonym dictionary (none exists as a free
API; de.wiktionary.org's REST API doesn't expose relation data either, see
scripts/translate_and_build_synant.py's header for that finding).

Pipeline:
1. Parse data/synanten.js for every (english_word, [english_antonym, ...])
   pair where antonyms is non-empty (~5,200 pairs).
2. Parse data/synantde.js for the full word list + which entries ALREADY
   have a non-empty antonyms field (skip those -- never overwrite).
3. Translate the ~5,200 distinct English words to German (en->de) via the
   free Google Translate endpoint, per-item requests through a
   ThreadPoolExecutor sharing one RateLimiter (the only approach proven
   reliable at scale -- see translate_and_build_synant.py; batching via
   newline-joining does NOT work reliably for this endpoint).
4. Where a translated German word EXACTLY matches (case-insensitive) an
   existing synantde.js word that still lacks antonyms, translate that
   word's English antonym list to German too (en->de, same mechanism) and
   record it as a pending update.
5. Apply all pending updates to data/synantde.js: entries without an
   `antonyms` key get one INSERTED right after `synonyms:"..."` (matching
   the exact object-literal format already used everywhere else in the
   file); entries are matched/replaced by their unique `word` value via
   regex, so this is a surgical update -- not a full regenerate.

Reuses the RateLimiter/translate_one/load_json/save_json helpers from
translate_and_build_synant.py so results share the SAME resumable
%TEMP%\\synant_translate_cache.json cache (keyed by "{src}|{tgt}|{text}").

Usage: python scripts/expand_de_antonyms.py [--limit N]
"""
import os
import re
import sys
import json
from concurrent.futures import ThreadPoolExecutor, as_completed

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from translate_and_build_synant import (  # noqa: E402
    RateLimiter,
    Transient,
    TRANSLATE_LIMITER,
    TRANSLATE_WORKERS,
    translate_one,
    load_json,
    save_json,
    TRANSLATE_CACHE,
    js_escape,
)

EN_FILE = r"c:\gitrepo\udsp\data\synanten.js"
DE_FILE = r"c:\gitrepo\udsp\data\synantde.js"

ENTRY_RE = re.compile(
    r'\{word:"(?P<word>(?:\\.|[^"\\])*)",level:"(?P<level>[^"]*)",'
    r'definition:"(?P<definition>(?:\\.|[^"\\])*)",example:"(?P<example>(?:\\.|[^"\\])*)",'
    r'synonyms:"(?P<synonyms>(?:\\.|[^"\\])*)"'
    r'(?:,antonyms:"(?P<antonyms>(?:\\.|[^"\\])*)")?\},'
)


def unesc(s):
    return (s or "").replace('\\"', '"').replace("\\\\", "\\")


def parse_entries(path):
    text = open(path, encoding="utf-8").read()
    entries = []
    for m in ENTRY_RE.finditer(text):
        entries.append(
            {
                "word": unesc(m.group("word")),
                "synonyms": unesc(m.group("synonyms")),
                "antonyms": unesc(m.group("antonyms")) if m.group("antonyms") else "",
            }
        )
    return text, entries


def translate_many(pairs, cache):
    """pairs: list of (src, tgt, text). Fills cache in place. Returns
    number newly translated (vs already cached)."""
    todo = []
    seen = set()
    for src, tgt, text in pairs:
        key = f"{src}|{tgt}|{text}"
        if key in cache or key in seen or not text:
            continue
        seen.add(key)
        todo.append((src, tgt, text, key))
    total = len(todo)
    if total == 0:
        return 0
    print(f"[translate] {total} distinct strings to translate", flush=True)
    done = 0
    skipped = 0
    with ThreadPoolExecutor(max_workers=TRANSLATE_WORKERS) as ex:
        futures = {ex.submit(translate_one, text, src, tgt): key for src, tgt, text, key in todo}
        for fut in as_completed(futures):
            key = futures[fut]
            try:
                cache[key] = fut.result()
            except Transient:
                skipped += 1
            done += 1
            if done % 150 == 0 or done == total:
                save_json(TRANSLATE_CACHE, cache)
                print(f"[translate] {done}/{total} ({skipped} skipped-for-retry)", flush=True)
    save_json(TRANSLATE_CACHE, cache)
    return total


def tr(cache, src, tgt, text):
    return cache.get(f"{src}|{tgt}|{text}", "")


if __name__ == "__main__":
    limit = None
    for arg in sys.argv[1:]:
        if arg.startswith("--limit"):
            limit = int(arg.split("=")[1])

    TARGET_DE_ANTONYMS = 2000

    _, en_entries = parse_entries(EN_FILE)
    en_pairs = [
        (e["word"], [a.strip() for a in e["antonyms"].split(";") if a.strip()])
        for e in en_entries
        if e["antonyms"]
    ]
    if limit:
        en_pairs = en_pairs[:limit]
    print(f"EN words with antonyms available: {len(en_pairs)}")

    de_text, de_entries = parse_entries(DE_FILE)
    de_index = {}
    de_syn_index = {}  # synonym token (casefold) -> entry, for entries still lacking antonyms
    de_has_antonym = 0
    for e in de_entries:
        k = e["word"].casefold()
        de_index[k] = e
        if e["antonyms"]:
            de_has_antonym += 1
        else:
            for tok in e["synonyms"].split(";"):
                tok = tok.strip()
                if tok and tok.casefold() not in de_syn_index:
                    de_syn_index[tok.casefold()] = e
    print(f"DE total entries: {len(de_entries)}, currently with antonyms: {de_has_antonym}, synonym-token index size: {len(de_syn_index)}")

    cache = load_json(TRANSLATE_CACHE)

    # Step 1: translate EN words -> DE.
    word_jobs = [("en", "de", w) for w, _ in en_pairs]
    translate_many(word_jobs, cache)

    # Step 2: find matches against existing DE words lacking antonyms -- try
    # an exact headword match first, then fall back to a synonym-list-token
    # match (a DE entry whose *synonyms* field contains the translated word,
    # even if that entry's own headword doesn't match).
    matches = []  # (de_word_exact, [en_antonym, ...])
    seen_de = set()
    for en_word, en_ants in en_pairs:
        de_word_guess = tr(cache, "en", "de", en_word)
        if not de_word_guess:
            continue
        k = de_word_guess.strip().casefold()
        hit = de_index.get(k)
        if not hit or hit["antonyms"]:
            hit = de_syn_index.get(k)
        if not hit or hit["antonyms"] or hit["word"].casefold() in seen_de:
            continue
        seen_de.add(hit["word"].casefold())
        matches.append((hit["word"], en_ants))
        if de_has_antonym + len(matches) >= TARGET_DE_ANTONYMS:
            break
    print(f"Matched {len(matches)} existing DE words currently lacking antonyms (target extra: {TARGET_DE_ANTONYMS - de_has_antonym})")

    # Step 3: translate the antonym word-lists for matched entries only.
    ant_jobs = []
    for _, en_ants in matches:
        for a in en_ants[:4]:
            ant_jobs.append(("en", "de", a))
    translate_many(ant_jobs, cache)

    # Step 4: build final updates and apply via regex substitution.
    updates = {}
    for de_word, en_ants in matches:
        de_ants = []
        seen_ant = set()
        for a in en_ants[:4]:
            t = tr(cache, "en", "de", a)
            if not t:
                continue
            tk = t.strip().casefold()
            if tk in seen_ant or tk == de_word.casefold():
                continue
            seen_ant.add(tk)
            de_ants.append(t.strip())
        if de_ants:
            updates[de_word] = "; ".join(de_ants)
    print(f"Built {len(updates)} antonym updates with resolved German text")

    def repl(m):
        word = unesc(m.group("word"))
        if word in updates and not m.group("antonyms"):
            return (
                m.group(0)[:-2]
                + f',antonyms:"{js_escape(updates[word])}"'
                + "},"
            )
        return m.group(0)

    updated_text = ENTRY_RE.sub(repl, de_text)
    with open(DE_FILE, "w", encoding="utf-8", newline="\n") as f:
        f.write(updated_text)

    final_total = len(re.findall(r'antonyms:"[^"]', updated_text))
    print(f"DONE. DE entries with antonyms now: {final_total} (was {de_has_antonym})")
