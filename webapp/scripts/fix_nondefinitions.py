#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Replace definitions that are not definitions (finding #4).

A -- data/synantde.js, 2,769 entries whose "definition" just restates the
     entry's own `synonyms` field ("Ähnlich wie: exhumieren, umbetten."), so
     the card shows the learner a list they already have.  Replaced with a
     real German gloss read from the German Wiktionary's structured
     {{Bedeutungen}} block, plus a Turkish translation of that gloss.
     The English Wiktionary API used by the original import is exactly what
     failed for these words, which is why they fell back to a synonym list --
     de.wiktionary is a different corpus and covers many of them.

B -- 431 French entries whose definition is just the English cognate
     ("possible" -> "possible.", "long" -> "long."), which tells a Turkish
     learner nothing.  The French word files state their definitions in
     ENGLISH ("Bonjour" -> "Hello / Good morning. - Merhaba..."), so the
     replacement is the English Wiktionary's gloss for the FRENCH sense of
     the word -- a real explanation instead of the word itself -- plus a
     Turkish translation of that gloss.

C -- English C1/C2 entries carrying a Wiktionary gloss for a different part
     of speech than the `pos` field claims ("accusative", pos "adjective",
     defined as "(grammar) The accusative case.").  The gloss is real, so the
     cheap and honest repair is to correct `pos` to the sense actually shown
     rather than throw the definition away.

Usage:
    python scripts/fix_nondefinitions.py --plan
    python scripts/fix_nondefinitions.py --fetch [--limit N]
    python scripts/fix_nondefinitions.py --apply
"""
import argparse
import collections
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _udsp_data as U
import _udsp_quality as Q
import _udsp_translate as T
import _wiktionary as W

FR_FILES = ["wordsa1fr.js", "wordsa2fr.js", "wordsb1fr.js", "wordsb2fr.js",
            "wordsc1fr.js", "wordsc2fr.js", "phrasalverbsfr.js", "synantfr.js"]
EN_FILES = ["wordsa1.js", "wordsa2.js", "wordsb1.js", "wordsb2.js",
            "wordsc1.js", "wordsc2.js", "toefl.js", "phrasalverbsen.js"]

VERB_DEF_RE = re.compile(r"^\s*(?:\([^)]*\)\s*)?to\s+\w", re.I)
NOUN_DEF_RE = re.compile(r"^\s*(?:\([^)]*\)\s*)?(?:the|an?)\s+\w", re.I)
# "To a different place" is a PLACE, not a verb -- an infinitive marker is only
# an infinitive when a verb follows it, never a determiner or a preposition.
NOT_INFINITIVE = {
    "a", "an", "the", "this", "that", "these", "those", "some", "any", "each",
    "every", "another", "one", "two", "which", "whom", "what", "whose", "all",
    "both", "his", "her", "its", "their", "my", "your", "our", "such", "no",
    "it", "him", "them", "us", "me", "you", "someone", "something", "somewhere",
    "here", "there", "where", "when", "how", "much", "many", "more", "most",
    "left", "right", "up", "down", "in", "on", "at", "by", "for", "from",
    "with", "within", "without", "into", "onto", "over", "under", "and", "or",
}
# synantde restates the entry's own synonym/antonym list where a gloss belongs.
SYNLIST_RE = re.compile(r"^\s*(?:\u00c4hnlich wie|Gegenteil von|Synonym[e]? f\u00fcr)\s*:", re.I)


def is_cognate(word, definition, lang):
    """Definition's native half is just the headword echoed back."""
    core = definition.strip().rstrip(".").strip()
    if not core:
        return False
    w = U.unescape(word)
    if lang in U.ARTICLE_RE:
        w = U.ARTICLE_RE[lang].sub("", w)
    return Q.fold(core) == Q.fold(w)


def plan_pos():
    """-> [(file, word, old_pos, new_pos)]

    Only unambiguous mismatches: an infinitive gloss under a non-verb pos, or
    a noun-phrase gloss under `verb`.  Anything that merely *looks* like the
    other part of speech is left alone -- a wrong `pos` is a cosmetic defect,
    but a wrongly "corrected" one is a new error.
    """
    out = []
    for name in EN_FILES:
        _, entries = U.load(name)
        for e in entries:
            pos = U.unescape(e.get("pos") or "").lower()
            d = e.native("definition")
            if not d or Q.is_no_definition(d):
                continue
            m = VERB_DEF_RE.match(d)
            if m:
                nxt = d[m.end() - 1:].split()[0].strip(",.;:").lower()
                if nxt in NOT_INFINITIVE:
                    m = None
            if m and pos in ("noun", "adjective", "adverb"):
                out.append((name, U.unescape(e["word"]), pos, "verb"))
            elif not m and pos == "verb" and NOUN_DEF_RE.match(d):
                out.append((name, U.unescape(e["word"]), pos, "noun"))
    return out


def plan_defs():
    """-> {"de": [words], "fr": [(file, word)]}"""
    de_words = []
    _, entries = U.load("synantde.js")
    for e in entries:
        d = e.native("definition")
        if not d:
            continue
        if Q.is_stub_definition(d) or SYNLIST_RE.match(d):
            de_words.append(U.unescape(e["word"]))
    fr_words = []
    for name in FR_FILES:
        if not os.path.exists(U.path_of(name)):
            continue
        _, entries = U.load(name)
        for e in entries:
            d = e.native("definition")
            if Q.is_no_definition(d) or not d:
                continue
            if is_cognate(e["word"], d, "fr"):
                fr_words.append((name, U.unescape(e["word"])))
    return {"de": de_words, "fr": fr_words}


def fetch_phase(limit):
    cache = T.Cache("wikt_native_defs.json")
    plans = plan_defs()
    jobs, seen = [], set()
    for site, word in ([("de", w) for w in plans["de"]]
                       + [("fren", w) for _n, w in plans["fr"]]):
        key = "%s|%s" % (site, word)
        if key in seen or cache.get(key) is not None:
            continue
        seen.add(key)
        jobs.append((site, word))
    if limit:
        jobs = jobs[:limit]
    print("fetching %d Wiktionary gloss(es)" % len(jobs), flush=True)

    done = hits = 0
    for site in ("de", "fren"):
        group = [w for s, w in jobs if s == site]
        # "fren" means: the FRENCH word, glossed in English, from en.wiktionary
        wiki = "de" if site == "de" else "en"
        for i in range(0, len(group), W.BATCH):
            titles = {w: U.headword(w, "de" if site == "de" else "fr")
                      for w in group[i:i + W.BATCH]}
            texts = W.fetch_many(sorted(set(titles.values())), wiki)
            for word, title in titles.items():
                wt = texts.get(title)
                if wt is None:
                    continue
                glosses = (W.german_meanings(wt) if site == "de"
                           else W.english_glosses(wt, "French"))
                glosses = [g for g in glosses
                           if 3 < len(g) <= 180
                           and not (site == "fren" and is_cognate(word, g, "fr"))]
                cache.put("%s|%s" % (site, word), glosses)
                done += 1
                if glosses:
                    hits += 1
            print("\r  %s %d/%d  with gloss %d" % (site, done, len(jobs), hits),
                  end="", flush=True)
    cache.save()

    # Where the English Wiktionary only echoes the cognate back ("nasal" ->
    # "nasal"), fr.wiktionary still has a real French explanation, which the
    # apply step translates into English and Turkish.  Computed from the whole
    # plan rather than from `jobs`, so a resumed run still covers the French
    # words whose English lookup was already cached as empty.
    leftover = [w for _n, w in plans["fr"]
                if not cache.get("fren|%s" % w)
                and cache.get("frfr|%s" % w) is None]
    leftover = sorted(set(leftover))
    if leftover:
        print("\n  %d French word(s) need fr.wiktionary" % len(leftover), flush=True)
        for i in range(0, len(leftover), W.BATCH):
            titles = {w: U.headword(w, "fr") for w in leftover[i:i + W.BATCH]}
            texts = W.fetch_many(sorted(set(titles.values())), "fr")
            for word, title in titles.items():
                wt = texts.get(title)
                if wt is None:
                    continue
                gl = [g for g in W.french_meanings(wt) if 3 < len(g) <= 180]
                cache.put("frfr|%s" % word, gl)
            print("\r    %d/%d" % (min(i + W.BATCH, len(leftover)), len(leftover)),
                  end="", flush=True)
        cache.save()
    print("\n  %d/%d fetched, %d with a gloss" % (done, len(jobs), hits))


def best_gloss(glosses):
    """The most explanatory gloss -- a longer one teaches more than a synonym."""
    return max(glosses, key=len) if glosses else None


def apply_phase():
    cache = T.Cache("wikt_native_defs.json")
    tcache = T.Cache("def_tr.json")
    plans = plan_defs()

    wanted = collections.defaultdict(dict)      # file -> {word: (gloss, src)}
    for w in plans["de"]:
        g = best_gloss(cache.get("de|%s" % w) or [])
        if g:
            wanted["synantde.js"][w] = (g, "de")
    for name, w in plans["fr"]:
        g = best_gloss(cache.get("fren|%s" % w) or [])
        if g:
            wanted[name][w] = (g, "en")
            continue
        g = best_gloss(cache.get("frfr|%s" % w) or [])
        if g:
            wanted[name][w] = (g, "fr")

    by_src = collections.defaultdict(set)
    for mapping in wanted.values():
        for g, src in mapping.values():
            by_src[src].add(g)
    print("glosses to translate: " + ", ".join(
        "%s %d" % (s, len(v)) for s, v in sorted(by_src.items())))

    tr, to_en = {}, {}
    for src, texts in by_src.items():
        tr.update(T.translate_many(tcache, sorted(texts), src, "tr", progress=True))
    if by_src.get("fr"):
        # a French gloss has to become English before it can be shown, because
        # the French word files state their definitions in English
        to_en = T.translate_many(tcache, sorted(by_src["fr"]), "fr", "en",
                                 progress=True)
    tcache.save()

    n_def = 0
    for name, mapping in wanted.items():
        ed = U.Editor(name)
        for e in ed.entries:
            w = U.unescape(e["word"])
            got = mapping.get(w)
            if not got:
                continue
            g, src = got
            native = to_en.get(g, g) if src == "fr" else g
            turkish = tr.get(g)
            if not native or not turkish:
                continue
            ed.set_bilingual(e, "definition",
                             T.ensure_period(T.titlecase_first(native)),
                             T.ensure_period(T.titlecase_first(turkish)))
        if ed.changed_fields:
            ed.save()
            n_def += ed.changed_fields
            print("  %-18s %d definition(s)" % (name, ed.changed_fields))

    # C -- part-of-speech corrections
    fixes = collections.defaultdict(dict)
    for name, w, _old, new in plan_pos():
        fixes[name][w] = new
    n_pos = 0
    for name, mapping in fixes.items():
        ed = U.Editor(name)
        for e in ed.entries:
            w = U.unescape(e["word"])
            if w in mapping:
                ed.set_field(e, "pos", mapping[w])
        if ed.changed_fields:
            ed.save()
            n_pos += ed.changed_fields
            print("  %-18s %d pos correction(s)" % (name, ed.changed_fields))
    print("\nreal definitions written: %d, pos corrected: %d" % (n_def, n_pos))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--plan", action="store_true")
    ap.add_argument("--fetch", action="store_true")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--limit", type=int, default=0)
    args = ap.parse_args()
    if args.fetch:
        fetch_phase(args.limit)
        return
    if args.apply:
        apply_phase()
        return
    plans = plan_defs()
    pos = plan_pos()
    print("A synantde synonym-list definitions : %d" % len(plans["de"]))
    print("B French cognate definitions        : %d" % len(plans["fr"]))
    for n, c in collections.Counter(n for n, _ in plans["fr"]).most_common():
        print("     %5d  %s" % (c, n))
    print("C English pos/definition mismatches : %d" % len(pos))
    for n, c in collections.Counter(p[0] for p in pos).most_common():
        print("     %5d  %s" % (c, n))
    for row in pos[:10]:
        print("        . %-14s %-18s %s -> %s" % row)


if __name__ == "__main__":
    main()
