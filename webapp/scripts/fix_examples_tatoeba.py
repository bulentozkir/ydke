#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Fill the example-sentence gaps Wiktionary could not cover, from Tatoeba.

Wiktionary only carries a usage citation for a minority of headwords, which
leaves ~17k entries with no example.  Tatoeba publishes a bulk per-language
sentence export (CC BY 2.0 FR) -- a few tens of megabytes per language, no
API key, no rate limit.  Downloading it once and matching locally turns a
"one HTTP request per word" problem into a single streaming scan.

    python scripts/fix_examples_tatoeba.py --download
    python scripts/fix_examples_tatoeba.py --plan
    python scripts/fix_examples_tatoeba.py --apply
"""
import argparse
import bz2
import collections
import os
import sys
import urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _udsp_data as U
import _udsp_quality as Q
import _udsp_translate as T
import fix_examples as F

CACHE = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".cache", "tatoeba")
ISO3 = {"en": "eng", "de": "deu", "fr": "fra",
        "es": "spa", "it": "ita", "pt": "por"}
URL = "https://downloads.tatoeba.org/exports/per_language/%s/%s_sentences.tsv.bz2"
HEADERS = {"User-Agent": "udsp-wordlist-quality/1.0 (offline data cleanup)"}
KEEP = 8          # candidate sentences retained per headword


def download():
    os.makedirs(CACHE, exist_ok=True)
    for lang, iso in sorted(ISO3.items()):
        dest = os.path.join(CACHE, "%s_sentences.tsv.bz2" % iso)
        if os.path.exists(dest) and os.path.getsize(dest) > 1000:
            print("  %s already present (%.1f MB)"
                  % (iso, os.path.getsize(dest) / 1e6))
            continue
        url = URL % (iso, iso)
        print("  downloading %s ..." % url, flush=True)
        req = urllib.request.Request(url, headers=HEADERS)
        tmp = dest + ".part"
        with urllib.request.urlopen(req, timeout=180) as r, open(tmp, "wb") as fh:
            while True:
                chunk = r.read(1 << 20)
                if not chunk:
                    break
                fh.write(chunk)
        os.replace(tmp, dest)
        print("     %.1f MB" % (os.path.getsize(dest) / 1e6))


def sentences(lang):
    """Stream the export: id \\t lang \\t text."""
    path = os.path.join(CACHE, "%s_sentences.tsv.bz2" % ISO3[lang])
    if not os.path.exists(path):
        return
    with bz2.open(path, "rt", encoding="utf-8", errors="replace") as fh:
        for line in fh:
            parts = line.rstrip("\n").split("\t")
            if len(parts) == 3:
                yield parts[2]


def wanted(need):
    """-> {lang: {word: headword}} for entries still lacking an example."""
    out = collections.defaultdict(dict)
    for name, words in need.items():
        lang = U.lang_of(name)
        if lang not in ISO3:
            continue
        for w in words:
            out[lang][w] = U.headword(w, lang)
    return out


# A headword may pick up an inflectional ending, but nothing longer: allowing
# any short suffix lets "centime" match "centimeter" and "strip-tease" match
# "strip-teaseur".
INFLECT = {"", "s", "es", "e", "en", "n", "em", "er", "et", "t", "st",
           "ne", "nt", "re", "rs", "ns", "ts", "as", "os", "is", "x",
           "a", "o", "i", "d", "ed", "ing"}
# English inflects far less, so the permissive set above is pure risk here:
# "-er" alone pairs "kingfish" with "kingfisher".
INFLECT_EN = {"", "s", "es", "ed", "d", "ing"}


def strict_hit(head, sentence, lang):
    """Does the sentence really use this headword?

    A Wiktionary citation is safe by provenance -- it was filed under the
    word's own entry.  A Tatoeba sentence is matched purely on string shape,
    so the same tolerance that makes example_contains_word() useful there is
    actively harmful here: it happily accepts "Schlauchboot" for
    "Schlauchbinder", "crucial" for "crucifer" and "pudding" for
    "puddingwife".  Require the headword itself, optionally carrying a short
    inflectional ending (Schokoladenriegel -> Schokoladenriegeln).
    """
    f = Q.fold(head)
    if not f or any(c.isdigit() for c in f):
        return False
    ok = INFLECT_EN if lang == "en" else INFLECT
    if len(Q.TOKEN_RE.findall(head)) > 1:
        return f in Q.fold(sentence)
    for tok in Q.TOKEN_RE.findall(sentence):
        t = Q.fold(tok)
        if t.startswith(f) and t[len(f):] in ok:
            return True
    return False


def acceptable(head, s, lang):
    s = F.tidy(s)
    if not (12 <= len(s) <= 200):
        return False
    if not F.looks_like_sentence(s):
        return False
    return strict_hit(head, s, lang)


def _fuzzy(head, tok):
    """Does `tok` look like an inflected form of `head`?

    Cheap prefix test rather than a stemmer: the corpus writes "quaffed"
    for "quaff" and "Schokoladenriegeln" for "Schokoladenriegel", and an
    exact-form index would miss every one of them.  False positives are
    harmless -- example_contains_word() is the authority and rejects them.
    """
    if abs(len(head) - len(tok)) > 5:
        return False
    n = min(len(head), len(tok))
    i = 0
    while i < n and head[i] == tok[i]:
        i += 1
    return i >= max(5, int(0.65 * len(head)))


def harvest(lang, words):
    """One streaming pass: {word: [candidate sentences]}.

    Indexing by token keeps the scan linear in the corpus rather than in
    words x corpus.  Multi-word headwords are rare enough to test by
    substring against only that subset.
    """
    by_token = collections.defaultdict(list)
    by_prefix = collections.defaultdict(list)
    phrases = []
    for w, head in words.items():
        toks = Q.TOKEN_RE.findall(head)
        if len(toks) == 1:
            f = Q.fold(toks[0])
            by_token[f].append(w)
            if len(f) >= 5:
                by_prefix[f[:5]].append((w, f))
        elif toks:
            phrases.append((w, Q.fold(head)))
    found = collections.defaultdict(list)
    full = set()
    for s in sentences(lang):
        hits = set()
        for tok in Q.TOKEN_RE.findall(s):
            f = Q.fold(tok)
            for w in by_token.get(f, ()):
                hits.add(w)
            if len(f) >= 5:
                for w, head in by_prefix.get(f[:5], ()):
                    if _fuzzy(head, f):
                        hits.add(w)
        if phrases:
            folded = Q.fold(s)
            for w, ph in phrases:
                if ph in folded:
                    hits.add(w)
        for w in hits:
            if w in full:
                continue
            if acceptable(words[w], s, lang):
                found[w].append(F.tidy(s))
                if len(found[w]) >= KEEP:
                    full.add(w)
    return found


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--download", action="store_true")
    ap.add_argument("--plan", action="store_true")
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--from-cache", action="store_true",
                    help="reuse the previous scan instead of re-reading 68 MB")
    args = ap.parse_args()

    if args.download:
        download()
        if not (args.plan or args.apply):
            return

    need = F.build_plan()
    want = wanted(need)
    print("entries still without an example: %d"
          % sum(len(v) for v in want.values()))

    cache = T.Cache("tatoeba_examples.json")
    chosen = {}
    for lang in sorted(want):
        if args.from_cache:
            n = 0
            for w in want[lang]:
                key = "%s|%s" % (lang, w)
                got = cache.get(key)
                # Re-check against the current rule so a tightened test can be
                # applied without re-reading the corpora.
                if got and strict_hit(want[lang][w], got[0], lang):
                    chosen[key] = got[0]
                    n += 1
        else:
            got = harvest(lang, want[lang])
            n = 0
            for w, cands in got.items():
                s = F.pick(cands)
                if s:
                    chosen["%s|%s" % (lang, w)] = s
                    cache.put("%s|%s" % (lang, w), [s])
                    n += 1
        print("   %-3s %6d / %-6d matched" % (lang, n, len(want[lang])))
    cache.save()

    if not args.apply:
        import random
        random.seed(7)
        rows = sorted(chosen.items())
        for lang in sorted(want):
            sub = [(k, v) for k, v in rows if k.startswith(lang + "|")]
            if not sub:
                continue
            print("\n--- %s sample ---" % lang)
            for k, v in random.sample(sub, min(8, len(sub))):
                print("   %-20s %s" % (k.split("|", 1)[1][:20], v))
        return

    tcache = T.Cache("example_tr.json")
    by_lang = collections.defaultdict(set)
    for key, s in chosen.items():
        by_lang[key.split("|", 1)[0]].add(s)
    tr = {}
    for lang, group in sorted(by_lang.items()):
        tr.update(T.translate_many(tcache, sorted(group), lang, "tr", progress=True))
    tcache.save()

    written = 0
    for name, words in need.items():
        lang = U.lang_of(name)
        if lang not in ISO3:
            continue
        ed = U.Editor(name)
        # One sentence per file: a corpus sentence often contains several of
        # the words we are looking for, and reusing it is finding #6 again.
        used = {U.unescape(e["example"]).split(U.sep_for(name))[0].strip()
                for e in ed.entries if e.get("example")}
        for e in ed.entries:
            w = U.unescape(e["word"])
            if w not in words:
                continue
            s = chosen.get("%s|%s" % (lang, w))
            if s and s not in used and tr.get(s):
                ed.set_bilingual(e, "example", s, tr[s])
                used.add(s)
                written += 1
        if ed.changed_fields:
            ed.save()
            print("  %-20s %d field(s)" % (name, ed.changed_fields))
    print("\nexamples written from Tatoeba: %d" % written)


if __name__ == "__main__":
    main()
