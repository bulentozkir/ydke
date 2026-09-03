#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""German lemma helpers shared by the boilerplate-definition passes.

`lemma_from_example` recovers an adjective's base form from a sentence that
already uses it.  A declined form ("mühsamen") and its lemma ("mühsam") share
a long common prefix, so the token in the example with the longest shared
prefix is the lemma -- verified against real text rather than guessed by
stripping a suffix, which misfires on words that merely end in -en
("Wagen", "eben", "gegen").
"""
import re

DECL_DEF_RE = re.compile(r"^\s*(?:weak/mixed|strong|weak|mixed)\b.*\b(?:plural|singular|form)\b\s*$", re.I)
PARTIKEL_DEF_RE = re.compile(r"^\s*German separable verb from an open-source word list\.?\s*$", re.I)

TOKEN_RE = re.compile(r"[^\W\d_]+", re.UNICODE)

# Adjective/participle declension endings, longest first.
ENDINGS = ["sten", "stem", "ster", "stes", "ste",
           "eren", "erem", "erer", "eres", "ere",
           "en", "em", "er", "es", "e"]


def common_prefix(a, b):
    a = a.casefold()
    b = b.casefold()
    n = min(len(a), len(b))
    i = 0
    while i < n and a[i] == b[i]:
        i += 1
    return i


def strip_ending(word):
    """Candidate lemmas for a declined adjective, longest ending first."""
    out = []
    for suf in ENDINGS:
        if word.casefold().endswith(suf) and len(word) - len(suf) >= 3:
            out.append(word[: len(word) - len(suf)])
    out.append(word)
    return out


def lemma_from_example(word, example, min_prefix=4):
    """Token in `example` that is the base form of `word`, or None.

    Accepts a token that is a PREFIX of the headword (the declined form is the
    lemma plus an ending) and shares at least `min_prefix` characters.
    """
    best = None
    best_len = 0
    wl = word.casefold()
    for tok in TOKEN_RE.findall(example or ""):
        tl = tok.casefold()
        if len(tl) < min_prefix:
            continue
        p = common_prefix(wl, tl)
        # the lemma is a prefix of the declined form, or equal to it
        if p < min_prefix or p < len(tl):
            continue
        if p > best_len or (p == best_len and best and len(tok) < len(best)):
            best = tok
            best_len = p
    return best
