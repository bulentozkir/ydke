#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Source real example sentences for every entry that lacks a usable one.

Covers three findings at once, because all three end in the same state -- the
entry has no sentence that actually demonstrates its headword:

  #1  20,240 entries whose example is the honest "No example sentence
      available" fallback (Tatoeba, the earlier source, has no coverage for
      C1/C2 and TOEFL vocabulary -- en C1 34%, en C2 32%, TOEFL 23%).
  #5   2,423 entries whose example is a real sentence that does not contain
      the headword in any inflected form ("l'été" illustrated with "ça a
      été", the participle of "être").
  #6     404 entries sharing one recycled sentence across up to 10 unrelated
      headwords -- only the word the sentence genuinely contains keeps it.

Source: the English Wiktionary REST definition API, whose per-sense
`examples` are exactly the usage citations that Wiktionary editors attached
to that sense.  This is the same API this project already uses for German
definitions (scripts/fetch_definitions.py), and unlike Tatoeba its coverage
is best precisely where Tatoeba's is worst: rare, literary and technical
vocabulary.  Sentences are then machine-translated to Turkish, matching the
"<native> - <Turkish>" convention of the word files.

A sentence is only accepted if it actually contains the headword (same
inflection-tolerant check the app uses) and is a plausible sentence rather
than a bare gloss.  Words with no usable citation keep the honest fallback --
this pass never fabricates a sentence.

Usage:
    python scripts/fix_examples.py --plan
    python scripts/fix_examples.py --fetch [--limit N]
    python scripts/fix_examples.py --apply
"""
import argparse
import collections
import html
import json
import os
import re
import sys
import threading
import urllib.error
import urllib.parse
import urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _udsp_data as U
import _udsp_quality as Q
import _udsp_translate as T
import _wiktionary as W

WIKI_LIMITER = T.RateLimiter(0.1)          # 10 req/s, well inside Wikimedia's guidance
W.LIMITER = WIKI_LIMITER                   # one shared budget across both wikis
HEADERS = {"User-Agent": "udsp-vocab-content/1.0 (offline study app; contact via repo)"}

TAG_RE = re.compile(r"<[^>]+>")
WS_RE = re.compile(r"\s+")
# Wiktionary section codes for the languages this corpus covers.
WIKI_LANG = {"en": "en", "de": "de", "fr": "fr", "es": "es", "it": "it", "pt": "pt"}

FALLBACK = {
    "en": "No example sentence available for this word.",
    "de": "Kein Beispielsatz für dieses Wort verfügbar.",
    "fr": "Aucune phrase d'exemple disponible pour ce mot.",
    "es": "No hay frase de ejemplo disponible para esta palabra.",
    "it": "Nessuna frase di esempio disponibile per questa parola.",
    "pt": "Nenhuma frase de exemplo disponível para esta palavra.",
}
FALLBACK_TR = "Bu kelime için örnek cümle bulunamadı."


def strip_html(s):
    s = TAG_RE.sub("", s or "")
    s = html.unescape(s)
    return WS_RE.sub(" ", s).strip()


SECTION = {"es": "Spanish", "it": "Italian", "pt": "Portuguese"}


def examples_from(wikitext, site, lang):
    if site == "de":
        return W.german_examples(wikitext)
    if site == "fr":
        return W.french_examples(wikitext)
    # es/it/pt have no dedicated fetcher here, but the English Wiktionary
    # carries {{ux|es|...}} usage lines inside their own language sections
    return W.english_examples(wikitext, SECTION.get(lang, "English"))


LEAD_JUNK_RE = re.compile(r"^[\s\u2026.\u201e\u201c\u201d\u00ab\u00bb\"'\u2014\u2013\-\u2022*:;,]+")
TRAIL_JUNK_RE = re.compile(r"[\s\u201e\u201c\u201d\u00ab\u00bb\"\u2014\u2013]+$")


def tidy(s):
    """Drop the quotation decoration Wiktionary wraps citations in."""
    s = WS_RE.sub(" ", (s or "").strip())
    s = LEAD_JUNK_RE.sub("", s)
    s = TRAIL_JUNK_RE.sub("", s)
    return s.strip()


# "race;And trees" / "friend?Vincent" -- wikitext joined without a space; the
# two words read as one.
MANGLED_RE = re.compile(r"[;,?!][^\W\d_]")
CLOSERS = "\"'\u00bb\u201d\u2019)]\u203a"


def looks_like_sentence(s):
    """Reject anything that is not a complete, self-contained sentence.

    Wiktionary files two kinds of non-sentence under its example markup:
    bare collocations ("andare a casa", "to scratch out a meagre living")
    and excerpts sliced out of the middle of a quotation ("the courtiers
    are often furious and ... criminative against the judges").  Both read
    as broken text on a flashcard and are unusable for Dictation or
    Sentence Scramble, which need a whole sentence.
    """
    if len(Q.TOKEN_RE.findall(s)) < 3:
        return False
    if MANGLED_RE.search(s):
        return False
    first = next((c for c in s if c.isalpha()), "")
    if not first.isupper():
        return False
    return s.rstrip(CLOSERS)[-1:] in ".!?"


def usable(word, sentence, lang):
    """Accept only a real sentence that demonstrably uses the headword."""
    if not sentence:
        return False
    s = tidy(sentence)
    if len(s) < 12 or len(s) > 220:
        return False
    if not looks_like_sentence(s):
        return False
    # Wiktionary citations often start with a bare year or a bibliographic
    # reference -- those are quotations, not usage examples.
    if re.match(r"^\s*(?:\d{4}|c\.\s*\d{4}|\[)", s):
        return False
    if "[\u2026]" in s or "[...]" in s:      # elided quotation, reads as a fragment
        return False
    return Q.example_contains_word(word, s, lang)


def pick(candidates):
    """Best sentence for a learner: short, self-contained, fully punctuated.

    Wiktionary lists hand-written usage examples before literary quotations,
    so earlier candidates get a bonus -- but a tidy modern sentence further
    down still beats a rambling 200-character citation at the top.

    Re-applies the sentence-shape test so citations cached by an earlier,
    looser run are held to the same standard.
    """
    candidates = [s for s in (candidates or []) if looks_like_sentence(s)]

    def score(item):
        i, s = item
        v = -i * 0.5
        n = len(s)
        if 30 <= n <= 140:
            v += 3
        elif n <= 180:
            v += 1
        if s[-1:] in ".!?":
            v += 2
        if s[:1].isupper():
            v += 1
        if ";" in s or ":" in s:
            v -= 1
        return v

    return max(enumerate(candidates), key=score)[1] if candidates else None


# --- planning -----------------------------------------------------------
def build_plan():
    """-> {file: {word: reason}} recomputed live; never cached."""
    need = collections.defaultdict(dict)
    for name in U.word_files():
        _, entries = U.load(name)
        lang = U.lang_of(name)
        by_example = collections.defaultdict(list)
        for e in entries:
            w = U.unescape(e["word"])
            ex = e.native("example")
            if not ex or Q.is_placeholder_example(ex):
                need[name][w] = "missing"
                continue
            by_example[Q.fold(ex)].append((w, e))
        for _key, group in by_example.items():
            if len(group) == 1:
                w, e = group[0]
                if not Q.example_contains_word(w, e.native("example"), lang):
                    need[name][w] = "noword"
                continue
            # recycled: the sentence stays with the word it genuinely contains
            owners = [(w, e) for w, e in group
                      if Q.example_contains_word(w, e.native("example"), lang)]
            keep = owners[0][0] if owners else None
            for w, _e in group:
                if w != keep:
                    need[name][w] = "recycled"
    return need


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plan", action="store_true")
    ap.add_argument("--fetch", action="store_true")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--limit", type=int, default=0)
    args = ap.parse_args()

    need = build_plan()
    total = sum(len(v) for v in need.values())
    by_reason = collections.Counter(r for v in need.values() for r in v.values())
    if args.plan or not (args.fetch or args.apply):
        print("entries needing a new example: %d" % total)
        for r, c in by_reason.most_common():
            print("   %6d  %s" % (c, r))
        print("")
        for name in sorted(need, key=lambda n: -len(need[n]))[:18]:
            print("   %6d  %s" % (len(need[name]), name))
        return

    cache = T.Cache("wiktionary_examples.json")
    if args.fetch:
        fetch_phase(need, cache, args.limit)
    if args.apply:
        apply_phase(need, cache)


def fetch_phase(need, cache, limit):
    jobs = []
    seen = set()
    for name, words in need.items():
        lang = U.lang_of(name)
        for w in words:
            key = "%s|%s" % (lang, w)
            if key in seen or cache.get(key) is not None:
                continue
            seen.add(key)
            jobs.append((key, w, lang))
    if limit:
        jobs = jobs[:limit]
    print("fetching Wiktionary citations for %d word(s)" % len(jobs), flush=True)

    # One request per 50 headwords, grouped by the wiki that will answer.
    by_site = collections.defaultdict(list)
    for key, w, lang in jobs:
        by_site[lang if lang in ("de", "fr") else "en"].append((key, w, lang))
    done = hits = 0
    for site, group in by_site.items():
        for i in range(0, len(group), W.BATCH):
            chunk = group[i:i + W.BATCH]
            titles = {w: U.headword(w, lang) for _k, w, lang in chunk}
            texts = W.fetch_many(sorted(set(titles.values())), site)
            for key, w, lang in chunk:
                wt = texts.get(titles[w])
                if wt is None:
                    continue                 # transient -> leave uncached
                good = [tidy(s) for s in examples_from(wt, site, lang)
                        if usable(w, s, lang)]
                cache.put(key, good)
                done += 1
                if good:
                    hits += 1
            print("\r  %s %d/%d  usable %d" % (site, done, len(jobs), hits),
                  end="", flush=True)
    cache.save()
    print("\n  %d/%d fetched, %d with a usable sentence" % (done, len(jobs), hits))


def apply_phase(need, cache):
    tcache = T.Cache("example_tr.json")
    # collect the sentences we will actually write, so translation is one pass
    chosen = {}
    for name, words in need.items():
        lang = U.lang_of(name)
        for w in words:
            got = cache.get("%s|%s" % (lang, w))
            s = pick(got) if got else None
            if s:
                chosen[(name, w)] = s
    sentences = sorted(set(chosen.values()))
    print("translating %d sentence(s) to Turkish" % len(sentences))
    by_lang = collections.defaultdict(set)
    for (name, _w), s in chosen.items():
        by_lang[U.lang_of(name)].add(s)
    tr = {}
    for lang, group in by_lang.items():
        tr.update(T.translate_many(tcache, sorted(group), lang, "tr", progress=True))
    tcache.save()

    written = cleared = 0
    for name, words in need.items():
        lang = U.lang_of(name)
        ed = U.Editor(name)
        for e in ed.entries:
            w = U.unescape(e["word"])
            if w not in words:
                continue
            s = chosen.get((name, w))
            if s and tr.get(s):
                ed.set_bilingual(e, "example", s, tr[s])
                written += 1
            elif words[w] in ("noword", "recycled"):
                # A wrong or recycled sentence is worse than an honest gap:
                # it teaches the learner a false association.
                ed.set_bilingual(e, "example", FALLBACK[lang], FALLBACK_TR)
                cleared += 1
        if ed.changed_fields:
            ed.save()
            print("  %-20s %d field(s)" % (name, ed.changed_fields))
    print("\nreal examples written: %d, wrong examples replaced by fallback: %d"
          % (written, cleared))


if __name__ == "__main__":
    main()
