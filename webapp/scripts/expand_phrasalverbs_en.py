#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""expand_phrasalverbs_en.py

Adds NEW English phrasal verbs to data/phrasalverbsen.js, mined from the
wordset-dictionary (same source already cached for expand_words_en_cefr.py
-- %TEMP%\\wordset_dict.json): any two- or three-token entry shaped like
"<verb> <particle>[ <particle>]" (e.g. "wolf down", "walk out of") with a
real verb-sense definition. True idiomatic phrasal verbs are a closed,
limited set in English -- this app already has 2000+ curated ones, so
(consistent with this project's "up to 1000 if you can" quality-first
framing) this adds whatever genuinely-usable NEW candidates exist rather
than forcing the count to exactly 1000.

Examples sourced from Tatoeba (reusing the cache from
expand_words_en_cefr.py where possible); Turkish via Google Translate.

Usage: python scripts/expand_phrasalverbs_en.py [--dry-run] [--phase ...]
"""
import os
import re
import sys
import bz2
import json
import math
import time
import argparse
import threading
import urllib.request
import urllib.parse
from concurrent.futures import ThreadPoolExecutor, as_completed

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")
TEMP = os.environ.get("TEMP", ".")

WORDSET_CACHE = os.path.join(TEMP, "wordset_dict.json")
TATOEBA_BZ2 = os.path.join(TEMP, "eng_sentences.tsv.bz2")
WORD_MATCH_CACHE = os.path.join(TEMP, "phrasalverbs_word_matches.json")
TRANSLATE_CACHE = os.path.join(TEMP, "phrasalverbs_translate_cache.json")

HEADERS = {"User-Agent": "TopWordsApp/1.0 (educational; https://udsp.vercel.app)"}
PV_PATH = os.path.join(DATA, "phrasalverbsen.js")
WORD_RE = re.compile(r'word:\s*"((?:\\.|[^"])*)"')
TOKEN_RE = re.compile(r"[^\W\d_]+", re.UNICODE)

PARTICLES = {
    "up", "down", "in", "out", "on", "off", "away", "back", "over", "through",
    "along", "around", "about", "into", "onto", "forward", "together", "apart",
    "aside", "across", "ahead", "behind", "by", "forth", "round", "under",
    "with", "for", "to", "at", "from", "of", "upon",
}
BAD_SUBSTRINGS = ("arse", "ass ", "piss", "shit", "fuck", "cunt", "nigger", "faggot", "whore",
                   "jack off", "jerk off", "wank off", "beat off")
# Explicit-content phrases caught via QA (e.g. "jack off" = masturbate) --
# checking BAD_SUBSTRINGS against the CANDIDATE WORD ITSELF (not just its
# definition) matters here because a REMOVED entry is no longer "existing"
# and would otherwise be silently re-added as a "new candidate" on a later
# re-run (this happened once already -- "jack off" was removed for content
# in an earlier pass, then resurfaced and got re-added by a subsequent run
# before these substrings were added to this list).
# NOTE: "get off" deliberately NOT included -- its dominant/common sense is
# "to disembark" (a legitimate, everyday phrasal verb); blanket-blocking it
# would lose that for the sake of a secondary crude slang sense.
LEADING_PAREN_RE = re.compile(r"^\([^)]{1,50}\)\s*")


def js_escape(s):
    return (s or "").replace("\\", "\\\\").replace('"', '\\"').replace("\n", " ").replace("\r", " ").strip()


def load_json(p):
    return json.load(open(p, encoding="utf-8")) if os.path.exists(p) else {}


def save_json(p, d):
    tmp = p + ".tmp"
    json.dump(d, open(tmp, "w", encoding="utf-8"), ensure_ascii=False)
    os.replace(tmp, p)


def sanitize(s):
    return DASHDASH_RE.sub(", ", (s or "").strip())


DASHDASH_RE = re.compile(r"\s[-\u2013\u2014]\s")


def gather_candidates():
    data = json.load(open(WORDSET_CACHE, encoding="utf-8"))
    existing = {w.lower() for w in WORD_RE.findall(open(PV_PATH, encoding="utf-8").read())}
    cands = []
    for word, entry in data.items():
        wl = word.lower().strip()
        parts = wl.split()
        if len(parts) not in (2, 3):
            continue
        verb = parts[0]
        if not re.match(r"^[a-z]+$", verb):
            continue
        if len(parts) == 2 and parts[1] not in PARTICLES:
            continue
        if len(parts) == 3 and (parts[1] not in PARTICLES or parts[2] not in PARTICLES):
            continue
        if wl in existing:
            continue
        if any(b in wl for b in BAD_SUBSTRINGS):
            continue
        best = None
        for m in entry.get("meanings", []):
            pos = (m.get("speech_part") or "").strip().lower()
            if pos != "verb":
                continue
            d = LEADING_PAREN_RE.sub("", (m.get("def") or "").strip()).strip()
            if not d or len(d) < 8 or len(d) > 160:
                continue
            if re.search(r"(?<![a-z])" + re.escape(verb) + r"(?![a-z])", d.lower()):
                continue  # circular
            best = d
            break
        if best:
            cands.append({"word": wl, "pos": "phrasal verb", "definition": best})
    print(f"[candidates] {len(cands)} new phrasal-verb candidates found", flush=True)
    return cands


class RateLimiter:
    def __init__(self, mi):
        self.mi = mi; self.lock = threading.Lock(); self.t = 0.0

    def wait(self):
        with self.lock:
            now = time.time(); s = max(now, self.t); self.t = s + self.mi
        d = s - now
        if d > 0:
            time.sleep(d)


TL = RateLimiter(0.16)


def is_good_candidate(sentence):
    n = len(sentence)
    if n < 8 or n > 180:
        return False
    words = sentence.split()
    return 2 <= len(words) <= 25


def fuzzy_ok(token_lower, target_lower):
    if len(target_lower) < 5:
        return False
    common = 0
    for a, b in zip(token_lower, target_lower):
        if a == b:
            common += 1
        else:
            break
    needed = max(5, math.ceil(0.65 * len(target_lower)))
    return common >= needed and abs(len(token_lower) - len(target_lower)) <= 5


def load_tatoeba_sentences():
    sentences = []
    with bz2.open(TATOEBA_BZ2, "rt", encoding="utf-8", errors="replace") as f:
        for line in f:
            parts = line.rstrip("\n").split("\t")
            if len(parts) < 3:
                continue
            text = parts[2].strip()
            if text:
                sentences.append(text)
    print(f"[tatoeba] {len(sentences)} sentences loaded", flush=True)
    return sentences


def build_inverted_index(sentences, needed_tokens):
    """One single pass over the corpus, building word -> list-of-sentence-
    indices ONLY for tokens we actually care about (needed_tokens) -- avoids
    the O(phrases x sentences) blowup of re-scanning the whole corpus once
    per phrase."""
    index = {}
    for si, sent in enumerate(sentences):
        if not is_good_candidate(sent):
            continue
        for tok in TOKEN_RE.findall(sent):
            tl = tok.lower()
            if tl in needed_tokens:
                index.setdefault(tl, []).append(si)
    return index


def tokens_close_together(sentence, toks, max_span_slack=3):
    """Verifies the phrase's tokens don't just ALL happen to appear
    somewhere in the sentence (which can produce false matches like "add"
    + "to" both appearing in "Nothing TO ADD!" with no real "add to"
    relationship) -- requires the tokens to appear IN THE SAME ORDER as
    the phrase itself (verb before particle -- rejects "Nothing TO add"
    for "add to" and "want TO attend" for "attend to", where "to" is
    really just an unrelated infinitive marker placed BEFORE the verb)
    AND within a compact window (allows a little slack for separable
    placement, e.g. "look the word up" / "look it up"). Tries every
    occurrence of the FIRST token (not just its first/earliest one) so a
    verb appearing multiple times in one sentence still gets a fair shot
    at finding a correctly-ordered match. Returns the window span (smaller
    = tighter, more trustworthy) or None if no valid ordered match exists."""
    sent_tokens = [t.lower() for t in TOKEN_RE.findall(sentence)]
    first_tok = toks[0]
    first_positions = [i for i, t in enumerate(sent_tokens) if t == first_tok]
    if not first_positions:
        return None
    best_span = None
    for start in first_positions:
        positions = [start]
        ok = True
        search_from = start
        for t in toks[1:]:
            found_at = None
            for i in range(search_from + 1, len(sent_tokens)):
                if sent_tokens[i] == t:
                    found_at = i
                    break
            if found_at is None:
                ok = False
                break
            positions.append(found_at)
            search_from = found_at
        if not ok:
            continue
        span = positions[-1] - positions[0]
        if span > len(toks) - 1 + max_span_slack:
            continue
        if best_span is None or span < best_span:
            best_span = span
    return best_span


def match_all_phrases(phrases, sentences):
    """Returns {phrase: sentence_or_None}. Builds ONE inverted index over
    the whole corpus for every distinct token appearing in any target
    phrase (fast: intersect short posting lists instead of rescanning
    2M sentences per phrase), then VERIFIES each candidate sentence
    actually uses the tokens close together (not just present anywhere)
    before accepting it -- picks the tightest, then shortest, match."""
    needed_tokens = set()
    for p in phrases:
        needed_tokens.update(p.lower().split())
    print(f"[index] building inverted index for {len(needed_tokens)} distinct tokens...", flush=True)
    index = build_inverted_index(sentences, needed_tokens)
    result = {}
    for p in phrases:
        toks = p.lower().split()
        lists = [index.get(t, []) for t in toks]
        if any(not lst for lst in lists):
            result[p] = None
            continue
        lists.sort(key=len)
        candidate_ids = set(lists[0])
        for lst in lists[1:]:
            candidate_ids &= set(lst)
            if not candidate_ids:
                break
        if not candidate_ids:
            result[p] = None
            continue
        best = None
        best_key = None
        for i in candidate_ids:
            sent = sentences[i]
            span = tokens_close_together(sent, toks)
            if span is None:
                continue
            key = (span, len(sent))
            if best_key is None or key < best_key:
                best_key = key
                best = sent
        result[p] = best
    return result


def phase_index(cands):
    cache = load_json(WORD_MATCH_CACHE)
    todo = [c["word"] for c in cands if c["word"] not in cache]
    print(f"[index] {len(cands)} total, {len(todo)} need matching", flush=True)
    if todo:
        sentences = load_tatoeba_sentences()
        matches = match_all_phrases(todo, sentences)
        cache.update(matches)
        save_json(WORD_MATCH_CACHE, cache)
    hit = sum(1 for v in cache.values() if v)
    print(f"[index] total matched so far: {hit}/{len(cache)}", flush=True)
    return cache


def translate_one(text, src="en", tgt="tr", attempts=4, base_delay=0.6):
    q = urllib.parse.quote(text.replace("\n", " ").strip())
    url = f"https://translate.googleapis.com/translate_a/single?client=gtx&sl={src}&tl={tgt}&dt=t&q={q}"
    for i in range(attempts):
        TL.wait()
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=20) as resp:
                data = json.loads(resp.read().decode("utf-8"))
            return "".join(seg[0] for seg in data[0])
        except Exception:
            time.sleep(base_delay * (2 ** i))
    return None


def phase_translate(strings):
    cache = load_json(TRANSLATE_CACHE)
    uniq = sorted(set(strings))
    todo = [s for s in uniq if f"en|tr|{s}" not in cache]
    print(f"[translate] {len(todo)} to translate ({len(uniq)-len(todo)} cached)", flush=True)
    if todo:
        done = 0; start = time.time()
        with ThreadPoolExecutor(max_workers=6) as ex:
            futs = {ex.submit(translate_one, s): s for s in todo}
            for fut in as_completed(futs):
                s = futs[fut]
                r = fut.result()
                if r:
                    cache[f"en|tr|{s}"] = r
                done += 1
                if done % 200 == 0 or done == len(todo):
                    save_json(TRANSLATE_CACHE, cache)
                    el = max(0.001, time.time() - start)
                    print(f"[translate] {done}/{len(todo)} ({done/el:.1f}/s)", flush=True)
        save_json(TRANSLATE_CACHE, cache)


def tr(cache, s):
    return cache.get(f"en|tr|{s}", "")


NOTFOUND_EX = "No example sentence available for this word."
NOTFOUND_EX_TR = "Bu kelime için örnek cümle bulunamadı."


def append_entries(entries):
    text = open(PV_PATH, encoding="utf-8").read()
    idx = text.rstrip().rfind("];")
    lines = []
    for e in entries:
        lines.append("  {")
        lines.append('    word: "%s",' % js_escape(e["word"]))
        lines.append('    pos: "%s",' % js_escape(e["pos"]))
        lines.append('    level: "PV",')
        lines.append('    category: "General",')
        lines.append('    definition: "%s",' % js_escape(e["definition"]))
        lines.append('    example: "%s",' % js_escape(e["example"]))
        lines.append("  },")
    new_text = text[:idx] + "\n".join(lines) + "\n" + text[idx:]
    with open(PV_PATH, "w", encoding="utf-8", newline="\n") as f:
        f.write(new_text)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--phase", default="all", choices=["candidates", "index", "translate", "apply", "all"])
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    cands = gather_candidates()
    if args.dry_run:
        for c in cands[:15]:
            print(" ", c["word"], "->", sanitize(c["definition"])[:70])
        return

    match_cache = phase_index(cands) if args.phase in ("index", "all", "candidates") else load_json(WORD_MATCH_CACHE)
    if args.phase == "index":
        return

    def resolve_example(word):
        m = match_cache.get(word)
        if m:
            return sanitize(m), True
        return NOTFOUND_EX, False

    if args.phase in ("translate", "all"):
        strings = []
        for c in cands:
            strings.append(sanitize(c["definition"]))
            ex, needs_tr = resolve_example(c["word"])
            if needs_tr:
                strings.append(ex)
        phase_translate(strings)
    if args.phase == "translate":
        return

    if args.phase in ("apply", "all"):
        tcache = load_json(TRANSLATE_CACHE)
        entries = []
        for c in cands:
            d2 = sanitize(c["definition"])
            td = tr(tcache, d2) or "Türkçe çeviri bulunamadı."
            ex2, needs_tr = resolve_example(c["word"])
            if needs_tr:
                te = tr(tcache, ex2)
                if not te:
                    ex2, te = NOTFOUND_EX, NOTFOUND_EX_TR
            else:
                te = NOTFOUND_EX_TR
            entries.append({
                "word": c["word"], "pos": c["pos"],
                "definition": f"{d2} - {td}",
                "example": f"{ex2} - {te}",
            })
        append_entries(entries)
        print(f"[done] appended {len(entries)} phrasal verbs", flush=True)


if __name__ == "__main__":
    main()
