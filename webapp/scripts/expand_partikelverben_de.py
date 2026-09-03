#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""expand_partikelverben_de.py

Adds NEW German separable verbs (Partikelverben) to data/partikelverbde.js,
mined from hermitdave/FrequencyWords de_full.txt via the SAME morphological
candidate filter used in this project's original 383->2050 expansion
(documented in repo memory), extended further down the frequency list this
time, but with REAL Wiktionary definitions+examples (not the placeholder
"Ich lerne das Wort X" text the original pass used) -- reuses the same
fetch/translate infra as expand_words_de_cefr.py.

Morphological filter (best-effort, ~90%+ genuine, same acceptance bar as
the original pass):
  - starts with one of ~45 known separable-verb prefixes (longest-first)
  - ends in en/eln/ern (verb infinitive suffix)
  - remaining stem after the prefix is >=3 chars
  - rejects: past participles (remainder starts with "ge"), zu-infinitives
    (remainder starts with "zu"), noun-derived suffixes
    (-ungen/-ionen/-heiten/-keiten/-schaften/-nissen/-enden/-iker/-isten/
    -ismus/-chen/-lein), non-separable "-ieren" loan-verbs.

Usage: python scripts/expand_partikelverben_de.py [--dry-run] [--phase ...]
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

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")
TEMP = os.environ.get("TEMP", ".")

FREQ_SRC = os.path.join(TEMP, "de_full.txt")
DEF_CACHE_PATH = os.path.join(TEMP, "partikelverben_defs_cache.json")
TRANSLATE_CACHE_PATH = os.path.join(TEMP, "partikelverben_translate_cache.json")
PV_PATH = os.path.join(DATA, "partikelverbde.js")

HEADERS = {"User-Agent": "TopWordsApp/1.0 (educational; https://udsp.vercel.app)"}
WORD_RE = re.compile(r'word:\s*"((?:\\.|[^"])*)"')
DASHDASH_RE = re.compile(r"\s[-\u2013\u2014]\s")
CAP = 5000

PREFIXES = sorted([
    "ab", "an", "auf", "aus", "bei", "durch", "ein", "mit", "nach", "vor",
    "weg", "zu", "zurück", "zusammen", "los", "fest", "statt", "teil", "um",
    "entlang", "empor", "herein", "heraus", "hinein", "hinaus", "hinzu",
    "herum", "nieder", "weiter", "wieder", "voran", "voraus", "vorbei",
    "davon", "dabei", "daran", "darauf", "heran", "herab", "herbei", "hervor",
    "gegenüber", "auseinander", "entgegen", "überein",
], key=len, reverse=True)

NOUN_SUFFIXES = ("ungen", "ionen", "heiten", "keiten", "schaften", "nissen",
                 "enden", "iker", "isten", "ismus", "chen", "lein")
BLOCKLIST = {
    "zufrieden", "anderen", "beiden", "mitten", "nachrichten", "abendessen",
    "androiden", "antilopen", "abkürzungen", "einrichtungen", "abteilungen",
}
# Explicit/crude-content words -- checked separately from BLOCKLIST (which
# is for morphological false positives) since these are otherwise
# perfectly valid separable-verb-shaped candidates whose MEANING is the
# problem, found via a broad content scan of the fetched definitions
# (same QA pattern applied to the English/German-CEFR/French sibling
# scripts in this same session).
CONTENT_BLOCKLIST = {"versauen", "versaut", "ficken", "bumsen", "wichsen"}

# Shared stem-based content-safety filter (catches inflected/prefixed crude
# forms the exact CONTENT_BLOCKLIST above misses, e.g. "durchbumsen",
# "abkacken", "anscheißen"). Defensive import so a missing module never
# breaks a harvest run.
try:
    from content_safety import is_inappropriate as _cs_bad
except Exception:
    def _cs_bad(w, lang="en"):
        return False


def is_separable_candidate(word, existing_lower):
    # NOTE: use casefold(), not lower() -- casefold() correctly maps the
    # German sz-ligature (ß) to "ss" per Unicode rules, so a candidate
    # spelled with "ss" (a common alternate/Swiss-standard rendering in the
    # de_full.txt frequency corpus) is correctly recognized as the SAME
    # word as an existing sz-ligature-spelled entry -- plain .lower() does
    # NOT do this normalization and previously let ss-spelled duplicates of
    # already-existing sz-ligature words slip through (found via QA:
    # "abschließen" existing + "abschliessen" added as if new).
    wl = word.casefold()
    if wl in existing_lower:
        return None
    if wl in BLOCKLIST or wl in CONTENT_BLOCKLIST or _cs_bad(word, "de"):
        return None
    if wl.endswith(("ieren", "ierten", "iert")):
        return None
    if not wl.endswith(("en", "eln", "ern")):
        return None
    for suf in NOUN_SUFFIXES:
        if wl.endswith(suf):
            return None
    for prefix in PREFIXES:
        if wl.startswith(prefix):
            remainder = wl[len(prefix):]
            if len(remainder) < 3:
                continue  # too short a remainder to be plausible -- try a shorter prefix instead
            # The FIRST (longest) prefix that actually matches determines the
            # one true morphological parse -- if ITS remainder looks like a
            # past participle ("ge...") or zu-infinitive ("zu..."), the whole
            # word is an inflected form, full stop. Do NOT fall through to
            # try a shorter alternate prefix (e.g. mis-parsing
            # "zurückzukehren" as zu+"rückzukehren" once "zurück"+"zukehren"
            # is correctly recognized as a zu-infinitive) -- that produced
            # real false positives during QA.
            if remainder.startswith("ge") or remainder.startswith("zu"):
                return None
            return prefix
    return None


def load_json(p):
    return json.load(open(p, encoding="utf-8")) if os.path.exists(p) else {}


def save_json(p, d):
    tmp = p + ".tmp"
    json.dump(d, open(tmp, "w", encoding="utf-8"), ensure_ascii=False)
    os.replace(tmp, p)


def js_escape(s):
    return (s or "").replace("\\", "\\\\").replace('"', '\\"').replace("\n", " ").replace("\r", " ").strip()


def sanitize(s):
    return DASHDASH_RE.sub(", ", (s or "").strip())


def existing_words():
    used = set()
    for fn in ("partikelverbde.js", "wordsa1gode.js", "wordsa2gode.js", "wordsb1gode.js",
               "wordsb2gode.js", "wordsc1gode.js", "wordsc2gode.js", "wordsa11de.js",
               "wordsa12de.js", "wordsa21de.js", "wordsa22de.js", "wordsb11de.js",
               "wordsb12de.js", "synantde.js"):
        p = os.path.join(DATA, fn)
        if os.path.exists(p):
            used |= {w.casefold() for w in WORD_RE.findall(open(p, encoding="utf-8").read())}
    return used


def load_candidates():
    used = existing_words()
    print(f"[setup] {len(used)} existing German words excluded", flush=True)
    seen = set()
    cands = []
    with open(FREQ_SRC, encoding="utf-8") as f:
        for line in f:
            parts = line.rstrip("\n").split(" ")
            if len(parts) < 2:
                continue
            w = parts[0].strip()
            wl = w.casefold()
            if wl in seen:
                continue
            if is_separable_candidate(w, used):
                seen.add(wl)
                cands.append(wl)
    print(f"[candidates] {len(cands)} separable-verb-shaped candidates found", flush=True)
    return cands


# --------------------------------------------------------------- fetch
STYLE_RE = re.compile(r"<style\b[^>]*>.*?</style>", re.DOTALL | re.IGNORECASE)
TAG_RE = re.compile(r"<[^>]+>")
WS_RE = re.compile(r"[ \t]+")
LEADING_PAREN_RE = re.compile(r"^\([^)]{1,50}\)\s*")
TRAILING_BRACKET_RE = re.compile(r"\s*\[[^\]]{1,80}\]\s*$")
BAD_DEFINITION_RE = re.compile(
    r"(inflection of|form of|plural of|genitive of|dative of|accusative of|"
    r"nominative of|alternative (spelling|form) of|obsolete (spelling|form) of|"
    r"archaic (spelling|form) of|dated (spelling|form) of|misspelling of|"
    r"superseded (spelling|form) of|past participle of|present participle of|"
    r"-person (singular|plural)|strong verb|weak verb|romanization of|"
    r"\bimperative\b|\bpresent of\b|\bpast of\b|comparative of|superlative of|"
    r"\bdeclension\b|\bconjugation\b|singular of|plural imperative|"
    r"first[- ]person|second[- ]person|third[- ]person|indicative|subjunctive|"
    r"preterite)", re.IGNORECASE)
MAINTENANCE_NOTE_RE = re.compile(
    r"(quotations? indicative|citation needed|can we verify|please add|"
    r"rfquote|rfdef|is being sought)", re.IGNORECASE)
NAME_HINT_RE = re.compile(
    r"\b(surname|given name|forename|first name|place ?name|proper noun|"
    r"male given|female given|toponym)\b", re.IGNORECASE)
SENSITIVE_DEF_RE = re.compile(
    r"\b(sexually attracted to child|self-harm|self harm|suicid|genocide|"
    r"terroris|rape|raping|raped|molest|incest|bestiality|pedophil|"
    r"paedophil|masturbat|ejaculat|orgasm|fellatio|sodom|"
    r"offensive term|derogatory term|disparaging term|ethnic slur|racial slur|"
    r"\bslur\b|contemptuous term|insulting term|offensive name|racist term)\w*\b", re.IGNORECASE)


def strip_html(s):
    s = STYLE_RE.sub("", s)
    s = TAG_RE.sub("", s)
    s = s.replace("&nbsp;", " ")
    s = WS_RE.sub(" ", s)
    return s.strip()


class RateLimiter:
    def __init__(self, mi):
        self.mi = mi; self.lock = threading.Lock(); self.t = 0.0

    def wait(self):
        with self.lock:
            now = time.time(); s = max(now, self.t); self.t = s + self.mi
        d = s - now
        if d > 0:
            time.sleep(d)


DEF_LIMITER = RateLimiter(0.28)
TRANSLATE_LIMITER = RateLimiter(0.16)


class NotFound(Exception):
    pass


class Transient(Exception):
    pass


def clean_bundled_example(ex):
    ex = strip_html(ex or "").strip()
    if any(x in ex for x in ("--", "\u2014", "\u2013", " - ")):
        return ""
    if len(ex) < 6 or len(ex) > 160:
        return ""
    return ex


def fetch_de_definition(word):
    url = "https://en.wiktionary.org/api/rest_v1/page/definition/" + urllib.parse.quote(word)
    last_err = None
    for i in range(4):
        DEF_LIMITER.wait()
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=15) as resp:
                data = json.loads(resp.read().decode("utf-8"))
            break
        except urllib.error.HTTPError as e:
            if e.code == 404:
                raise NotFound(str(e))
            last_err = e
        except Exception as e:
            last_err = e
        time.sleep(0.8 * (2 ** i))
    else:
        raise Transient(str(last_err))

    de_entries = data.get("de")
    if not de_entries:
        raise NotFound("no de section")
    for entry in de_entries:
        raw_pos = (entry.get("partOfSpeech") or "").strip().lower()
        if raw_pos != "verb":
            continue
        for d in entry.get("definitions", []):
            definition = strip_html(d.get("definition", "")).split(" | ")[0].strip()
            definition = LEADING_PAREN_RE.sub("", definition).strip()
            definition = TRAILING_BRACKET_RE.sub("", definition).strip()
            if not definition or len(definition) < 8 or len(definition) > 160:
                continue
            if BAD_DEFINITION_RE.search(definition) or MAINTENANCE_NOTE_RE.search(definition):
                continue
            if NAME_HINT_RE.search(definition) or SENSITIVE_DEF_RE.search(definition):
                continue
            if re.search(r"(?<![A-Za-zÄÖÜäöüß])" + re.escape(word.lower()) + r"(?![A-Za-zÄÖÜäöüß])", definition.lower()):
                continue
            example = ""
            exs = d.get("examples") or []
            if exs:
                example = clean_bundled_example(exs[0])
            return {"definition": definition, "example": example}
    raise NotFound("no usable verb definition")


def fetch_batch(words, workers=5):
    cache = load_json(DEF_CACHE_PATH)
    todo = [w for w in words if w not in cache]
    if not todo:
        return cache
    done = 0
    start = time.time()
    with ThreadPoolExecutor(max_workers=workers) as ex:
        futs = {ex.submit(fetch_de_definition, w): w for w in todo}
        for fut in as_completed(futs):
            w = futs[fut]
            try:
                cache[w] = fut.result()
            except NotFound:
                cache[w] = None
            except Transient:
                pass
            done += 1
            if done % 300 == 0 or done == len(todo):
                save_json(DEF_CACHE_PATH, cache)
                el = max(0.001, time.time() - start)
                found = sum(1 for w2 in todo[:done] if cache.get(w2))
                print(f"[fetch] {done}/{len(todo)} ({done/el:.1f}/s, {found} found so far)", flush=True)
    save_json(DEF_CACHE_PATH, cache)
    return cache


def phase_fetch(batch_size=3000, max_rounds=100):
    candidates = load_candidates()
    cache = load_json(DEF_CACHE_PATH)
    pos = 0
    while pos < len(candidates) and candidates[pos] in cache:
        pos += 1
    print(f"[setup] first not-yet-cached at {pos}/{len(candidates)}", flush=True)

    for round_i in range(max_rounds):
        cache = load_json(DEF_CACHE_PATH)
        found = sum(1 for w in candidates[:pos] if cache.get(w))
        print(f"[round {round_i}] pos={pos} found={found}/{CAP}", flush=True)
        if found >= CAP or pos >= len(candidates):
            break
        batch = candidates[pos:pos + batch_size]
        if not batch:
            break
        fetch_batch(batch)
        pos += batch_size
    return candidates


def translate_one(text, src="de", tgt="tr", attempts=4, base_delay=0.6):
    q = urllib.parse.quote(text.replace("\n", " ").strip())
    url = f"https://translate.googleapis.com/translate_a/single?client=gtx&sl={src}&tl={tgt}&dt=t&q={q}"
    for i in range(attempts):
        TRANSLATE_LIMITER.wait()
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=20) as resp:
                data = json.loads(resp.read().decode("utf-8"))
            return "".join(seg[0] for seg in data[0])
        except Exception:
            time.sleep(base_delay * (2 ** i))
    return None


def build_chosen(candidates):
    cache = load_json(DEF_CACHE_PATH)
    chosen = []
    for w in candidates:
        if len(chosen) >= CAP:
            break
        rec = cache.get(w)
        if not rec:
            continue
        if w in CONTENT_BLOCKLIST or SENSITIVE_DEF_RE.search(rec.get("definition") or ""):
            # Defense-in-depth: same rationale as the sibling scripts --
            # catches a word/definition added to the blocklist AFTER it
            # was already fetched+cached by an earlier/in-progress run.
            continue
        chosen.append({"word": w, "definition": rec["definition"], "example": rec.get("example", "")})
    return chosen


def phase_translate():
    candidates = load_candidates()
    chosen = build_chosen(candidates)
    print(f"[translate] {len(chosen)} words chosen", flush=True)
    cache = load_json(TRANSLATE_CACHE_PATH)
    strings = []
    for e in chosen:
        strings.append(sanitize(e["definition"]))
        if e["example"]:
            strings.append(sanitize(e["example"]))
    uniq = sorted(set(strings))
    todo = [s for s in uniq if f"de|tr|{s}" not in cache]
    print(f"[translate] {len(todo)} to translate ({len(uniq)-len(todo)} cached)", flush=True)
    if todo:
        done = 0; start = time.time()
        with ThreadPoolExecutor(max_workers=6) as ex:
            futs = {ex.submit(translate_one, s): s for s in todo}
            for fut in as_completed(futs):
                s = futs[fut]
                r = fut.result()
                if r:
                    cache[f"de|tr|{s}"] = r
                done += 1
                if done % 200 == 0 or done == len(todo):
                    save_json(TRANSLATE_CACHE_PATH, cache)
                    el = max(0.001, time.time() - start)
                    print(f"[translate] {done}/{len(todo)} ({done/el:.1f}/s)", flush=True)
        save_json(TRANSLATE_CACHE_PATH, cache)


def tr(cache, s):
    return cache.get(f"de|tr|{s}", "")


NOTFOUND_EX_DE = "Kein Beispielsatz für dieses Wort verfügbar."
NOTFOUND_EX_TR = "Bu kelime için örnek cümle bulunamadı."


def append_entries(entries):
    text = open(PV_PATH, encoding="utf-8").read()
    idx = text.rstrip().rfind("];")
    lines = []
    for e in entries:
        lines.append("  {")
        lines.append('    word: "%s",' % js_escape(e["word"]))
        lines.append('    pos: "separable verb",')
        lines.append('    level: "PART",')
        lines.append('    category: "General",')
        lines.append('    definition: "%s",' % js_escape(e["definition"]))
        lines.append('    example: "%s",' % js_escape(e["example"]))
        lines.append("  },")
    new_text = text[:idx] + "\n".join(lines) + "\n" + text[idx:]
    with open(PV_PATH, "w", encoding="utf-8", newline="\n") as f:
        f.write(new_text)


def phase_apply():
    candidates = load_candidates()
    chosen = build_chosen(candidates)
    tcache = load_json(TRANSLATE_CACHE_PATH)
    entries = []
    for e in chosen:
        d2 = sanitize(e["definition"])
        td = tr(tcache, d2) or "Türkçe çeviri bulunamadı."
        if e["example"]:
            ex2 = sanitize(e["example"])
            te = tr(tcache, ex2)
            if not te:
                ex2, te = NOTFOUND_EX_DE, NOTFOUND_EX_TR
        else:
            ex2, te = NOTFOUND_EX_DE, NOTFOUND_EX_TR
        entries.append({
            "word": e["word"],
            "definition": f"{d2} - {td}",
            "example": f"{ex2} - {te}",
        })
    append_entries(entries)
    print(f"[done] appended {len(entries)} separable verbs", flush=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--phase", default="all", choices=["fetch", "translate", "apply", "all"])
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--batch-size", type=int, default=3000)
    ap.add_argument("--max-rounds", type=int, default=100)
    args = ap.parse_args()

    if args.dry_run:
        cands = load_candidates()
        print("first 20 candidates:", cands[:20])
        return

    if args.phase in ("fetch", "all"):
        phase_fetch(batch_size=args.batch_size, max_rounds=args.max_rounds)
    if args.phase == "fetch":
        return
    if args.phase in ("translate", "all"):
        phase_translate()
    if args.phase == "translate":
        return
    if args.phase in ("apply", "all"):
        phase_apply()


if __name__ == "__main__":
    main()
