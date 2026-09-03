#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""expand_words_en_cefr.py

Adds up to 1000 NEW words to EACH of: wordsa1.js, wordsa2.js, wordsb1.js,
wordsb2.js, wordsc1.js, wordsc2.js, and toefl.js (English). This app already
has ~16,000+ unique English words across its existing files, so genuinely
NEW, real, well-defined candidates are the limiting factor -- not every
level will actually reach 1000 (see the supply-vs-target note below); this
script always favors word QUALITY and CEFR-level-appropriateness over
hitting an exact count.

Sources (both free, no API key, no per-request rate limit for the bulk
parts):
  - Definitions + POS: the open wordset-dictionary (26 letter JSON files,
    CC-licensed, same source already proven in expand_toefl.py) --
    cached to %TEMP%\\wordset_dict.json.
  - Difficulty ranking: hermitdave/FrequencyWords en_50k.txt -- cached to
    %TEMP%\\en_freq_50k.txt. Words NOT in the top-50k list are treated as
    "rank = infinity" (rarer than anything ranked) -- these are only ever
    assigned to C1/C2/TOEFL, never A1/A2/B1/B2, since CEFR's whole premise
    is common-to-rare progression and forcing a rare/unranked word into A1
    would violate the "quality for each level" requirement.
  - Example sentences: Tatoeba's free bulk English sentence export (CC BY
    2.0 FR, already cached to %TEMP%\\eng_sentences.tsv.bz2 from an earlier
    session) -- exact then fuzzy (inflection-tolerant) token match, reusing
    the exact algorithm from fix_placeholder_examples.py. Falls back to the
    wordset entry's own bundled example if Tatoeba has no hit and the
    bundled one is clean; falls back further to the established HONEST
    "No example sentence available..." text (never a fabricated sentence)
    if neither source has anything usable.
  - Turkish: Google Translate free endpoint, threaded + rate-limited (same
    proven pattern as every other bulk-translation script in this project).

Allocation algorithm (single pass over ALL candidates, sorted by frequency
rank ascending with unranked treated as +infinity):
  1. Exclude every word already used ANYWHERE in the app's English files
     (all 6 CEFR files + toefl.js + phrasalverbsen.js + synanten.js).
  2. Fill A1, then A2, then B1, then B2 -- each up to 1000 -- ONLY from
     ranked candidates (never dip into the unranked/rarer-than-50k tail).
     If the ranked pool runs out before a bucket reaches 1000, that bucket
     simply gets fewer -- an honest, expected outcome given this app's
     already-deep existing coverage of common words.
  3. Fill C1, then C2, then TOEFL -- each up to 1000 -- continuing from
     wherever the previous step left off, INCLUDING the unranked tail
     (C1/C2/TOEFL are exactly where rarer/more advanced vocabulary
     belongs).

Usage:
  python scripts/expand_words_en_cefr.py --dry-run     (no network writes, just shows the plan)
  python scripts/expand_words_en_cefr.py --phase index  (Tatoeba matching only)
  python scripts/expand_words_en_cefr.py --phase translate
  python scripts/expand_words_en_cefr.py --phase apply  (writes the files)
  python scripts/expand_words_en_cefr.py --phase all
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
FREQ_CACHE = os.path.join(TEMP, "en_freq_50k.txt")
FREQ_URL = "https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2018/en/en_50k.txt"
TATOEBA_BZ2 = os.path.join(TEMP, "eng_sentences.tsv.bz2")
TATOEBA_URL = "https://downloads.tatoeba.org/exports/per_language/eng/eng_sentences.tsv.bz2"
WORD_MATCH_CACHE = os.path.join(TEMP, "example_fix_word_matches.json")  # shared/reused cache
TRANSLATE_CACHE = os.path.join(TEMP, "en_cefr_expand_translate_cache.json")
PLAN_CACHE = os.path.join(TEMP, "en_cefr_expand_plan.json")

HEADERS = {"User-Agent": "TopWordsApp/1.0 (educational; https://udsp.vercel.app)"}

TARGETS = [  # (level, filename, varname, cap, max_rank -- None = no ceiling)
    ("A1", "wordsa1.js", "WORDS_A1", 4000, 9000),
    ("A2", "wordsa2.js", "WORDS_A2", 4000, 18000),
    ("B1", "wordsb1.js", "WORDS_B1", 4000, 28000),
    ("B2", "wordsb2.js", "WORDS_B2", 4000, 45000),
    ("C1", "wordsc1.js", "WORDS_C1", 4000, None),
    ("C2", "wordsc2.js", "WORDS_C2", 4000, None),
    ("TOEFL", "toefl.js", "TOEFL", 4000, None),
]
RANKED_ONLY_LEVELS = {"A1", "A2", "B1", "B2"}

ALL_EN_FILES = ["toefl.js", "wordsa1.js", "wordsa2.js", "wordsb1.js", "wordsb2.js",
                "wordsc1.js", "wordsc2.js", "phrasalverbsen.js", "synanten.js"]

WORD_RE = re.compile(r'word:\s*"((?:\\.|[^"])*)"')
TOKEN_RE = re.compile(r"[^\W\d_]+", re.UNICODE)
WORD_OK = re.compile(r"^[a-z][a-z]{4,13}$")
DASHDASH_RE = re.compile(r"\s[-\u2013\u2014]\s")
BAD_LABELS = {"technical", "scientific", "medical", "archaic", "slang", "informal",
              "vulgar", "offensive", "dialect", "ethnic slur", "obsolete", "nonstandard"}
GOOD_POS = {"noun", "verb", "adjective", "adverb"}
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
    "prostitution", "rape", "rapist", "molest", "molestation", "incest",
    "pedophile", "pedophilia", "paedophile", "paedophilia", "pedo", "paedo",
    "bestiality", "zoophilia", "zoophilism", "damned", "hell", "crap", "piss",
    "pissed", "turd", "screw", "screwed", "arse", "wank", "jizz", "cum",
    "nipple", "buttock", "buttocks", "orgy", "libido", "aphrodisiac",
    "suicide", "rapes", "raping", "raped", "seppuku",
    # Explicit anatomical/sex-act terms -- caught via QA (e.g. "fellatio",
    # "dildo", "clitoris" slipped past the definition-CONTENT-only checks
    # below since the HEADWORD ITSELF wasn't checked against these, only a
    # narrower substring list) -- language-learning vocabulary has no need
    # for these regardless of how "clinically" a dictionary defines them.
    "fellatio", "fellate", "fellation", "cunnilingus", "dildo", "vibrator", "clitoris",
    "clitoral", "foreskin", "circumcision", "circumcised", "labia", "vulva",
    "smegma", "buttplug", "strapon", "blowjob", "handjob", "ballsack",
    "dominatrix", "gigolo", "voyeurism", "exhibitionist", "swinger",
    "stripper", "striptease", "callgirl", "hooker", "bondage",
    "sadomasochism", "sadomasochist", "fetishist", "kinky", "threesome",
    "orgasmic", "erotica", "erotic", "titillating", "concubine", "harlot",
    "nudie", "peepshow", "peep-show", "smut", "lewd", "raunchy",
    # Further QA pass across the FULL candidate pool (broader keyword net:
    # sexual/erotic/genital/prostitut/etc appearing anywhere in the
    # definition) surfaced these -- real explicit-content/crude-slang words
    # or senses, kept separate from the many FALSE positives that same
    # broad scan also produced (e.g. "asexual" reproduction in botany/
    # biology -- "gemma", "medusa", "yeast" -- or "bondage" meaning
    # captivity -- "liberator" -- which are legitimate, unrelated
    # vocabulary and were deliberately NOT added here).
    "madam", "randy", "wench", "foreplay", "sadistic", "hustler", "vaginal",
    "pecker", "cuckold", "pubes", "molester", "succubus", "floozy",
    "nymphomaniac", "nympho", "lecherous", "lecher", "hotness", "eroticism",
    "sadism", "bordello", "jockstrap", "hymen", "aflame",
    "homosexual", "homosexuality", "heterosexual", "bisexual", "transvestite",
    "bottomless", "topless",
    # Final headword-precision scan (checks the CANDIDATE WORD directly, not
    # just definition text) caught these -- "midget" (increasingly viewed
    # as an offensive/dated term for people with dwarfism), "gigolo"/
    # "nympho"/"bitch"(-as-noun-insult)/"pussy"(-as-vulgar-slang) (crude/
    # derogatory slang), "rape"/"raping"/"raped"/"rapist" (too heavy a
    # topic for a general vocabulary app despite being real dictionary
    # words), "glans" (near-exclusively used in an explicit anatomical
    # context despite a "clinical" gloss), "jerk off"/"jerking off" (crude
    # masturbation slang, same family as "jack off").
    "midget", "gigolo", "nympho", "jerk off", "jerking off", "glans",
    # Found via the +4000 pass's bad-examples/headword scan -- all near-
    # exclusively anatomical/sexual-activity terms with no other common
    # usage, same rationale as "glans"/"foreskin"/"circumcision" above.
    "outercourse", "penile", "prepuce", "introitus",
}
NAME_RE = re.compile(
    r"\b(given name|surname|forename|first name|male given|female given|"
    r"name of a|name for a (male|female|boy|girl)|placename|place ?name|"
    r"proper noun|used as a name)\b", re.IGNORECASE)
# Sensitive-topic content filter applied to DEFINITION TEXT (not just the
# headword) -- catches e.g. "paedophile"/"child" combinations, self-harm,
# and other subject matter unsuitable for a general-audience vocabulary app
# even when the headword itself looks innocuous.
SENSITIVE_DEF_RE = re.compile(
    r"\b(sexually attracted to child|child sex|self-harm|self harm|"
    r"suicid|genocide|terroris|rape|raping|raped|molest|incest|bestiality|"
    r"pedophil|paedophil|masturbat|ejaculat|orgasm|fellatio|sodom|"
    r"offensive term|derogatory term|disparaging term|ethnic slur|racial slur|"
    r"\bslur\b|contemptuous term|insulting term|offensive name|racist term)\w*\b",
    re.IGNORECASE)
# Linguistic-jargon / usage-note fragments from WordNet-style glosses that a
# language learner wouldn't understand and that don't read as standalone
# definitions -- reject rather than try to repair.
JARGON_DEF_RE = re.compile(
    r"\(with `|\(postpositive\)|\(prenominal\)|\(usually followed by|"
    r"\(often followed by|\(a\) \(|used with a negative|nonstandard usage|"
    r"\bq\.v\.\b", re.IGNORECASE)
LEADING_PAREN_RE = re.compile(r"^\([^)]{1,50}\)\s*")
NOTFOUND_EX = "No example sentence available for this word."
NOTFOUND_EX_TR = "Bu kelime için örnek cümle bulunamadı."


def js_escape(s):
    return (s or "").replace("\\", "\\\\").replace('"', '\\"').replace("\n", " ").replace("\r", " ").strip()


def load_json(p):
    return json.load(open(p, encoding="utf-8")) if os.path.exists(p) else {}


def save_json(p, d):
    tmp = p + ".tmp"
    json.dump(d, open(tmp, "w", encoding="utf-8"), ensure_ascii=False)
    os.replace(tmp, p)


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
    for bad in ("fuck", "shit", "bitch", "cunt", "nigger", "faggot", "pussy", "asshole",
                "porn", "whore", "pedo", "paedo", "rape", "suicid"):
        if bad in lw:
            return False
    return True


def clean_definition(word, d):
    """Strips a leading usage-note parenthetical, then rejects definitions
    that are jargon fragments, sensitive-topic content, or circular (echo
    the headword itself instead of actually defining it)."""
    d = LEADING_PAREN_RE.sub("", d).strip()
    if not d or len(d) < 15 or len(d) > 160:
        return ""
    if JARGON_DEF_RE.search(d):
        return ""
    if SENSITIVE_DEF_RE.search(d):
        return ""
    if re.search(r"(?<![A-Za-z])" + re.escape(word.lower()) + r"(?![A-Za-z])", d.lower()):
        return ""  # circular/echo definition
    return d


def looks_like_name(entry):
    for m in entry.get("meanings", []):
        if NAME_RE.search(m.get("def") or ""):
            return True
    return False


def sanitize(s):
    s = (s or "").strip()
    s = DASHDASH_RE.sub(", ", s)
    return s


def clean_bundled_example(ex):
    ex = (ex or "").strip()
    if any(x in ex for x in ("--", "\u2014", "\u2013", " - ")):
        return ""
    if re.search(r"[-]\s+[A-Z][a-z]+", ex):
        return ""
    if len(ex) < 12 or len(ex) > 130:
        return ""
    if ex.count('"') % 2 != 0:
        return ""
    return ex


def download_wordset():
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


def existing_words():
    used = set()
    for fn in ALL_EN_FILES:
        p = os.path.join(DATA, fn)
        if os.path.exists(p):
            used |= {w.casefold() for w in WORD_RE.findall(open(p, encoding="utf-8").read())}
    return used


def gather_candidates():
    data = download_wordset()
    freq = load_freq()
    used = existing_words()
    ranked, unranked = [], []
    for word, entry in data.items():
        if not WORD_OK.match(word):
            continue
        if word.casefold() in used:
            continue
        if not is_clean_word(word):
            continue
        if word.endswith(("ing", "ed")):
            continue
        if {l.get("name", "").lower() for l in (entry.get("labels") or [])} & BAD_LABELS:
            continue
        if looks_like_name(entry):
            continue
        best = None
        for m in entry.get("meanings", []):
            pos = (m.get("speech_part") or "").strip().lower()
            if pos not in GOOD_POS:
                continue
            if {l.get("name", "").lower() for l in (m.get("labels") or [])} & BAD_LABELS:
                continue
            d = clean_definition(word, (m.get("def") or "").strip())
            if not d:
                continue
            ex = clean_bundled_example(m.get("example"))
            best = (d, pos, ex)
            if ex:
                break  # prefer a meaning that also has a clean bundled example
        if not best:
            continue
        d, pos, bundled_ex = best
        r = freq.get(word.casefold())
        rec = {"word": word, "pos": pos, "definition": d, "bundled_example": bundled_ex, "rank": r}
        if r is None:
            unranked.append(rec)
        else:
            ranked.append(rec)
    ranked.sort(key=lambda x: x["rank"])
    return ranked, unranked


def build_plan():
    ranked, unranked = gather_candidates()
    print(f"[plan] ranked candidates: {len(ranked)}, unranked candidates: {len(unranked)}", flush=True)
    ri, ui = 0, 0
    plan = {}
    for level, fname, varname, cap, max_rank in TARGETS:
        chosen = []
        # ranked-first, but NEVER past this level's rank ceiling -- filling
        # a bucket to its numeric cap by reaching arbitrarily far into a
        # much-higher (rarer) rank than is level-appropriate would violate
        # "quality for each level" (caught via QA: words like "wraith"/
        # "reaper" at rank ~12500 were initially forced into A1 just to hit
        # 1000). A shortfall below `cap` here is the correct, honest outcome
        # when the level-appropriate supply runs out, not a bug to "fix" by
        # loosening the ceiling.
        while len(chosen) < cap and ri < len(ranked) and (max_rank is None or ranked[ri]["rank"] <= max_rank):
            chosen.append(ranked[ri]); ri += 1
        if level not in RANKED_ONLY_LEVELS:
            while len(chosen) < cap and ui < len(unranked):
                chosen.append(unranked[ui]); ui += 1
        plan[level] = chosen
        rng = f"rank {chosen[0]['rank']}..{chosen[-1]['rank']}" if chosen else "n/a"
        print(f"[plan] {level} ({fname}): {len(chosen)}/{cap} words chosen ({rng}, ceiling={max_rank})", flush=True)
    print(f"[plan] leftover ranked unused: {len(ranked) - ri}, leftover unranked unused: {len(unranked) - ui}", flush=True)
    return plan


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


def is_good_tatoeba_candidate(sentence):
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


def ensure_tatoeba():
    if os.path.exists(TATOEBA_BZ2) and os.path.getsize(TATOEBA_BZ2) > 1_000_000:
        return
    print("[tatoeba] downloading eng_sentences.tsv.bz2 ...", flush=True)
    req = urllib.request.Request(TATOEBA_URL, headers=HEADERS)
    tmp = TATOEBA_BZ2 + ".tmp"
    with urllib.request.urlopen(req, timeout=120) as resp, open(tmp, "wb") as f:
        while True:
            chunk = resp.read(1024 * 256)
            if not chunk:
                break
            f.write(chunk)
    os.replace(tmp, TATOEBA_BZ2)


def load_tatoeba_sentences():
    ensure_tatoeba()
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


def match_words_to_sentences(words, sentences):
    target_lower = {w.lower(): w for w in words}
    best = {}
    for sent in sentences:
        if not is_good_tatoeba_candidate(sent):
            continue
        for tok in TOKEN_RE.findall(sent):
            tl = tok.lower()
            if tl in target_lower and tl not in best:
                best[tl] = (len(sent), sent)
    exact_found = set(best.keys())
    leftover = [w for w in words if w.lower() not in exact_found]
    if leftover:
        buckets = {}
        for w in leftover:
            wl = w.lower()
            if len(wl) >= 5:
                buckets.setdefault(wl[:5], []).append(wl)
        fuzzy_best = {}
        if buckets:
            for sent in sentences:
                if not is_good_tatoeba_candidate(sent):
                    continue
                for tok in TOKEN_RE.findall(sent):
                    tl = tok.lower()
                    if len(tl) < 5:
                        continue
                    cands = buckets.get(tl[:5])
                    if not cands:
                        continue
                    for wl in cands:
                        if wl in fuzzy_best:
                            continue
                        if fuzzy_ok(tl, wl):
                            fuzzy_best[wl] = (len(sent), sent)
        for w in leftover:
            hit = fuzzy_best.get(w.lower())
            if hit:
                best[w.lower()] = hit
    return {w: (best[w.lower()][1] if w.lower() in best else None) for w in words}


def phase_index_examples(plan):
    cache = load_json(WORD_MATCH_CACHE)
    all_words = sorted({rec["word"] for level_list in plan.values() for rec in level_list})
    todo = [w for w in all_words if w not in cache]
    print(f"[index] {len(all_words)} total words needed, {len(todo)} need Tatoeba matching (cached={len(all_words)-len(todo)})", flush=True)
    if todo:
        sentences = load_tatoeba_sentences()
        matches = match_words_to_sentences(todo, sentences)
        del sentences
        hit = sum(1 for v in matches.values() if v)
        print(f"[index] matched {hit}/{len(todo)} to a real Tatoeba sentence", flush=True)
        cache.update(matches)
        save_json(WORD_MATCH_CACHE, cache)
    return cache


def translate_one(text, src="en", tgt="tr", attempts=4, base_delay=0.6):
    q = urllib.parse.quote(text.replace("\n", " ").strip())
    url = f"https://translate.googleapis.com/translate_a/single?client=gtx&sl={src}&tl={tgt}&dt=t&q={q}"
    last_err = None
    for i in range(attempts):
        TL.wait()
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=20) as resp:
                data = json.loads(resp.read().decode("utf-8"))
            return "".join(seg[0] for seg in data[0])
        except Exception as e:
            last_err = e
        time.sleep(base_delay * (2 ** i))
    return None


def phase_translate(strings):
    cache = load_json(TRANSLATE_CACHE)
    uniq = sorted(set(strings))
    todo = [s for s in uniq if f"en|tr|{s}" not in cache]
    print(f"[translate] {len(todo)} to translate ({len(uniq)-len(todo)} cached of {len(uniq)} unique)", flush=True)
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
    return cache


def tr(cache, s):
    return cache.get(f"en|tr|{s}", "")


def append_entries(path, level, entries):
    text = open(path, encoding="utf-8").read()
    idx = text.rstrip().rfind("];")
    if idx < 0:
        raise RuntimeError("no '];' terminator in " + path)
    lines = []
    for e in entries:
        lines.append("  {")
        lines.append('    word: "%s",' % js_escape(e["word"]))
        lines.append('    pos: "%s",' % js_escape(e["pos"]))
        lines.append('    level: "%s",' % level)
        lines.append('    category: "General",')
        lines.append('    definition: "%s",' % js_escape(e["definition"]))
        lines.append('    example: "%s",' % js_escape(e["example"]))
        lines.append("  },")
    new_text = text[:idx] + "\n".join(lines) + "\n" + text[idx:]
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(new_text)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--phase", default="all", choices=["plan", "index", "translate", "apply", "all"])
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    if args.dry_run:
        plan = build_plan()
        for level, _, _, _, _ in TARGETS:
            chosen = plan[level]
            print(f"\n=== {level}: sample of 10 ===")
            for rec in chosen[:10]:
                print(f"  [{rec['rank']}] {rec['word']} [{rec['pos']}]: {sanitize(rec['definition'])[:70]}")
        return

    if args.phase in ("plan", "index", "translate", "apply", "all"):
        if os.path.exists(PLAN_CACHE) and args.phase != "plan":
            raw = load_json(PLAN_CACHE)
            plan = raw
        else:
            plan = build_plan()
            save_json(PLAN_CACHE, plan)
        if args.phase == "plan":
            return

    if args.phase in ("index", "all"):
        phase_index_examples(plan)
    if args.phase == "index":
        return

    match_cache = load_json(WORD_MATCH_CACHE)

    def resolve_example(rec):
        w = rec["word"]
        tatoeba = match_cache.get(w)
        if tatoeba:
            return sanitize(tatoeba), True
        if rec.get("bundled_example"):
            return sanitize(rec["bundled_example"]), True
        return NOTFOUND_EX, False

    if args.phase in ("translate", "all"):
        strings = []
        for level, _, _, _, _ in TARGETS:
            for rec in plan[level]:
                strings.append(sanitize(rec["definition"]))
                ex, needs_tr = resolve_example(rec)
                if needs_tr:
                    strings.append(ex)
        phase_translate(strings)
    if args.phase == "translate":
        return

    if args.phase in ("apply", "all"):
        tcache = load_json(TRANSLATE_CACHE)
        total_written = 0
        total_fallback_def = 0
        total_fallback_ex = 0
        for level, fname, varname, cap, max_rank in TARGETS:
            chosen = plan[level]
            entries = []
            for rec in chosen:
                d2 = sanitize(rec["definition"])
                ex2, needs_tr = resolve_example(rec)
                td = tr(tcache, d2)
                if not td:
                    total_fallback_def += 1
                    td = "Türkçe çeviri bulunamadı."
                if needs_tr:
                    te = tr(tcache, ex2)
                    if not te:
                        total_fallback_ex += 1
                        ex2, te = NOTFOUND_EX, NOTFOUND_EX_TR
                else:
                    te = NOTFOUND_EX_TR
                entries.append({
                    "word": rec["word"],
                    "pos": rec["pos"],
                    "definition": f"{d2} - {td}",
                    "example": f"{ex2} - {te}",
                })
            path = os.path.join(DATA, fname)
            append_entries(path, level, entries)
            total_written += len(entries)
            print(f"[write] {fname}: appended {len(entries)} entries", flush=True)
        print(f"[done] total written={total_written}, fallback_def={total_fallback_def}, fallback_ex={total_fallback_ex}", flush=True)


if __name__ == "__main__":
    main()
