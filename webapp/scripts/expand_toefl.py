#!/usr/bin/env python3
r"""Expand data/toefl.js to 6000 entries with clean, advanced, reasonably
common English vocabulary sourced from the open wordset-dictionary
(accurate per-word def + example + speech_part), ranked by an English
frequency list so the added words are genuinely useful collegiate vocabulary
rather than obscure/technical curiosities. The Turkish half of each field is
produced with Google Translate (no dictionaryapi.dev dependency, so this does
not conflict with the fallback-fetch background job).

Pipeline: wordset cache (scripts/_wordset_analyze.py downloads it) ->
filter clean advanced words with a good simple example -> keep those in a
sensible frequency band, most-common-first -> translate def+example en->tr
-> write "English. - Turkish." entries into toefl.js.

Usage:
  python scripts/expand_toefl.py --dry-run    # preview, no write, no network
  python scripts/expand_toefl.py              # translate + write
"""
import os
import re
import json
import time
import argparse
import threading
import urllib.request
import urllib.parse
from concurrent.futures import ThreadPoolExecutor, as_completed

TEMP = os.environ.get("TEMP", ".")
WORDSET_CACHE = os.path.join(TEMP, "wordset_dict.json")
FREQ_CACHE = os.path.join(TEMP, "en_freq_50k.txt")
# Dedicated cache (NOT the shared synant_translate_cache.json) so this can run
# concurrently with the fallback-fix background job without clobbering its
# cache file via last-writer-wins atomic replaces.
TRANSLATE_CACHE = os.path.join(TEMP, "toefl_translate_cache.json")
FREQ_URL = "https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2018/en/en_50k.txt"

TOEFL_PATH = r"c:\gitrepo\udsp\data\toefl.js"
TARGET = 6000

HEADERS = {"User-Agent": "TopWordsApp/1.0 (educational; https://udsp.vercel.app)"}
WORD_RE = re.compile(r'word:\s*"((?:\\.|[^"])*)"')
EN_FILES = [
    "data/toefl.js", "data/wordsa1.js", "data/wordsa2.js", "data/wordsb1.js",
    "data/wordsb2.js", "data/wordsc1.js", "data/wordsc2.js",
    "data/phrasalverbsen.js", "data/synanten.js",
]
BAD_LABELS = {"technical", "scientific", "medical", "archaic", "slang", "informal",
              "vulgar", "offensive", "dialect", "ethnic slur", "obsolete", "nonstandard"}
GOOD_POS = {"noun", "verb", "adjective", "adverb"}
WORD_OK = re.compile(r"^[a-z][a-z]{4,13}$")
DASHDASH_RE = re.compile(r"\s[-\u2013\u2014]\s")
ATTRIB_RE = re.compile(r"\s*[-\u2013\u2014]\s*[A-Z][\w.']*(?:\s+[A-Z][\w.']*){0,3}\s*$")

# Frequency band: skip the most common ~3500 words (basic/function words &
# inflections like going/things/wanted) and anything rarer than 45000
# (obscure). The mid band is where genuine collegiate/TOEFL vocabulary lives.
MIN_RANK = 3500
MAX_RANK = 45000

# Hard content filter: wordset does not reliably label profanity/sexual/slur
# senses, so exclude these explicitly -- this is a vocabulary app for
# students. Substring check catches derived forms too.
PROFANITY = {
    "fuck", "fucking", "fucked", "fucker", "shit", "shitty", "bitch", "bastard",
    "asshole", "arsehole", "cunt", "dick", "cock", "pussy", "prick", "wanker",
    "whore", "slut", "damn", "bollocks", "bugger", "twat", "nigger", "nigga",
    "faggot", "fag", "spic", "chink", "kike", "wop", "coon", "dyke", "tranny",
    "retard", "retarded", "penis", "vagina", "anus", "testicle", "scrotum",
    "erection", "orgasm", "ejaculate", "ejaculation", "masturbate",
    "masturbation", "porn", "pornography", "pornographic", "boob", "boobs",
    "tits", "titty", "horny", "coitus", "fornicate", "fornication", "sodomy",
    "sodomize", "genitalia", "genital", "condom", "brothel", "prostitute",
    "prostitution", "rape", "rapist", "molest", "incest", "pedophile",
    "bestiality", "zoophilia", "zoophilism", "damned", "hell", "crap", "piss",
    "pissed", "turd", "screw", "screwed", "arse", "wank", "jizz", "cum",
    "nipple", "buttock", "buttocks", "orgy", "libido", "aphrodisiac",
}


# Shared stem-based content-safety filter (catches inflected/prefixed crude
# forms the exact-set PROFANITY list below misses). Defensive import so a
# missing module never breaks a harvest run.
try:
    from content_safety import is_inappropriate as _cs_bad
except Exception:
    def _cs_bad(w, lang="en"):
        return False


def is_clean_word(w):
    lw = w.casefold()
    if _cs_bad(w, "en"):
        return False
    if lw in PROFANITY:
        return False
    # catch obvious profane substrings (e.g. motherfucker, bullshit)
    for bad in ("fuck", "shit", "bitch", "cunt", "nigger", "faggot", "pussy",
                "asshole", "porn", "whore"):
        if bad in lw:
            return False
    return True


class RateLimiter:
    def __init__(self, mi):
        self.mi = mi; self.lock = threading.Lock(); self.t = 0.0
    def wait(self):
        with self.lock:
            now = time.time(); s = max(now, self.t); self.t = s + self.mi
        d = s - now
        if d > 0: time.sleep(d)


TL = RateLimiter(0.2)


def js_escape(s): return s.replace("\\", "\\\\").replace('"', '\\"')
def load_json(p): return json.load(open(p, encoding="utf-8")) if os.path.exists(p) else {}
def save_json(p, d):
    tmp = p + ".tmp"; json.dump(d, open(tmp, "w", encoding="utf-8"), ensure_ascii=False); os.replace(tmp, p)


def ensure_period(s):
    s = (s or "").strip()
    if s and s[-1] not in ".!?": s += "."
    return s


def sanitize(s):
    s = (s or "").strip()
    s = DASHDASH_RE.sub(", ", s)   # protect the app's " - " field separator
    return s


def words_in(paths):
    toefl, allen = set(), set()
    for p in paths:
        if not os.path.exists(p): continue
        s = set(w.casefold() for w in WORD_RE.findall(open(p, encoding="utf-8").read()))
        allen |= s
        if p.endswith("toefl.js"): toefl = s
    return toefl, allen


def load_freq():
    if not os.path.exists(FREQ_CACHE):
        req = urllib.request.Request(FREQ_URL, headers=HEADERS)
        with urllib.request.urlopen(req, timeout=60) as resp:
            open(FREQ_CACHE, "w", encoding="utf-8").write(resp.read().decode("utf-8", "replace"))
    rank = {}
    for i, line in enumerate(open(FREQ_CACHE, encoding="utf-8")):
        parts = line.split()
        if parts:
            w = parts[0].strip().casefold()
            if w not in rank:
                rank[w] = i
    return rank


def clean_example(ex):
    ex = (ex or "").strip()
    # Reject quotation / author-attribution artifacts and anything that would
    # collide with the app's " - " field separator. wordset sometimes stores
    # sourced quotations like "...heart- John le Carre" or em-dash quotes;
    # we have far more candidates than needed, so reject rather than repair.
    if any(x in ex for x in ("--", "\u2014", "\u2013", " - ")):
        return ""
    if re.search(r"[-]\s+[A-Z][a-z]+", ex):   # "word- Name" attribution
        return ""
    if len(ex) < 12 or len(ex) > 130:
        return ""
    if ex.count('"') % 2 != 0:
        return ""
    return ex


def pick_meaning(entry):
    best = None
    for m in entry.get("meanings", []):
        pos = (m.get("speech_part") or "").strip().lower()
        if pos not in GOOD_POS: continue
        if {l.get("name", "").lower() for l in (m.get("labels") or [])} & BAD_LABELS: continue
        d = (m.get("def") or "").strip()
        if not d or len(d) < 10 or len(d) > 160: continue
        ex = clean_example(m.get("example"))
        if best is None and ex:
            best = (d, ex, pos)
        if ex:
            return (d, ex, pos)
    return best


def download_wordset():
    """Download the open wordset-dictionary (26 letter files) and cache it,
    so this script is self-contained on a fresh checkout."""
    if os.path.exists(WORDSET_CACHE):
        return load_json(WORDSET_CACHE)
    base = "https://raw.githubusercontent.com/wordset/wordset-dictionary/master/data/"
    merged = {}
    for ch in "abcdefghijklmnopqrstuvwxyz":
        req = urllib.request.Request(base + ch + ".json", headers=HEADERS)
        with urllib.request.urlopen(req, timeout=60) as resp:
            merged.update(json.loads(resp.read().decode("utf-8")))
        print("downloaded wordset", ch, flush=True)
    save_json(WORDSET_CACHE, merged)
    return merged


def gather_candidates():
    data = download_wordset()
    toefl, allen = words_in(EN_FILES)
    freq = load_freq()
    cands = []
    for word, entry in data.items():
        if not WORD_OK.match(word): continue
        if not is_clean_word(word): continue
        if word.endswith(("ing", "ed")): continue   # skip inflected forms
        if word.casefold() in allen: continue  # fully new to the whole app
        if {l.get("name", "").lower() for l in (entry.get("labels") or [])} & BAD_LABELS: continue
        picked = pick_meaning(entry)
        if not picked: continue
        d, ex, pos = picked
        r = freq.get(word.casefold())
        # keep a sensible frequency band: skip too-basic (rank < MIN_RANK) and
        # too-rare (rank > MAX_RANK / not listed) -- collegiate vocabulary is
        # the mid-frequency band.
        if r is None or r < MIN_RANK or r > MAX_RANK:
            continue
        cands.append((r, word, pos, d, ex))
    cands.sort(key=lambda x: x[0])  # most common (within band) first
    return toefl, cands


def translate_all(strings):
    cache = load_json(TRANSLATE_CACHE)
    uniq = set(strings)
    todo = [s for s in uniq if f"en|tr|{s}" not in cache]
    print(f"[translate] to_translate={len(todo)} (cached={len(uniq)-len(todo)} of {len(uniq)} unique)", flush=True)
    if todo:
        done = 0; start = time.time()
        def work(s):
            q = urllib.parse.quote(s.replace("\n", " ").strip())
            url = f"https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=tr&dt=t&q={q}"
            for i in range(4):
                TL.wait()
                try:
                    req = urllib.request.Request(url, headers=HEADERS)
                    with urllib.request.urlopen(req, timeout=20) as resp:
                        data = json.loads(resp.read().decode("utf-8"))
                    return "".join(seg[0] for seg in data[0])
                except Exception:
                    time.sleep(0.6 * (2 ** i))
            return None
        with ThreadPoolExecutor(max_workers=5) as ex:
            futs = {ex.submit(work, s): s for s in todo}
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
    return cache


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    toefl_text = open(TOEFL_PATH, encoding="utf-8").read()
    have = len(WORD_RE.findall(toefl_text))
    need = max(0, TARGET - have)
    print(f"toefl current={have}, need={need}")

    toefl, cands = gather_candidates()
    print(f"clean freq-ranked candidates: {len(cands)}")
    chosen = cands[:need]
    if chosen:
        print(f"chosen={len(chosen)} (freq rank range {chosen[0][0]}..{chosen[-1][0]})")

    if args.dry_run:
        pos_counts = {}
        for r, w, pos, d, ex in chosen:
            pos_counts[pos] = pos_counts.get(pos, 0) + 1
        print("pos distribution:", pos_counts)
        print("\n=== 15 most-common chosen ===")
        for r, w, pos, d, ex in chosen[:15]:
            print(f"  [{r}] {w} [{pos}]: {sanitize(d)[:70]}")
            print(f"       ex: {sanitize(ex)[:80]}")
        print("\n=== 8 least-common chosen ===")
        for r, w, pos, d, ex in chosen[-8:]:
            print(f"  [{r}] {w} [{pos}]: {sanitize(d)[:70]}")
        return

    strings = []
    for r, w, pos, d, ex in chosen:
        strings.append(sanitize(d)); strings.append(sanitize(ex))
    tcache = translate_all(strings)

    def tr(s): return tcache.get(f"en|tr|{s}", "")
    blocks = []
    missing = 0
    for r, w, pos, d, ex in chosen:
        d2, ex2 = sanitize(d), sanitize(ex)
        td, te = tr(d2), tr(ex2)
        if not td or not te:
            missing += 1
            continue
        definition = f"{ensure_period(d2)} - {ensure_period(td)}"
        example = f"{ensure_period(ex2)} - {ensure_period(te)}"
        blocks.append(
            "  {\n"
            f'    word: "{js_escape(w)}",\n'
            f'    pos: "{pos}",\n'
            '    level: "TOEFL",\n'
            f'    definition: "{js_escape(definition)}",\n'
            f'    example: "{js_escape(example)}",\n'
            "  },\n"
        )
    print(f"built={len(blocks)} missing_translation={missing}")
    insert_at = toefl_text.rfind("];")
    updated = toefl_text[:insert_at] + "".join(blocks) + toefl_text[insert_at:]
    open(TOEFL_PATH, "w", encoding="utf-8", newline="\n").write(updated)
    print(f"WROTE -> new total {have + len(blocks)}")


if __name__ == "__main__":
    main()
