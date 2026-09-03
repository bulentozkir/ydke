#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""content_safety.py  --  single source of truth for content-safety filtering.

Shared by remove_inappropriate_words.py (the cleanup pass) and every
expand_*.py harvest script, so crude/vulgar/explicit/slur words are excluded
BOTH when new words are harvested AND caught if any slip through.

Key design point learned the hard way in this project: the harvest scripts
originally used EXACT-set blocklists, so inflected/prefixed forms
("durchbumsen" vs "bumsen", "godemiche" vs "godemichet", "niggardly" vs
"nigger") slipped straight through. These rules are therefore STEM/substring
based and word-boundary anchored so they match inflections without matching
legitimate homographs (swanky~"wank", fickle~"fick", communiquer~"nique",
cornichon~"nichon", Pute[turkey]~"pute", enculturation~"encul", etc.).

Precision over recall: every rule is tuned to avoid false positives (verified
against the real data). Clinical/anatomical/medical vocabulary is intentionally
NOT removed -- only unambiguously crude slang, explicit sex-act terms and slurs.
"""
import re

# GLOBAL = English/international crude, applied to every language.
REMOVE_GLOBAL = re.compile(
    r"fuck|"
    r"\bshit|bullshit|shitty|shithole|"
    r"\bcunt|\bwhore|\bsluts?\b|"
    r"faggot|nigger|nigga|niggard|\bkike\b|\bwop\b|tranny|"
    r"blowjob|handjob|fellati|cunnilingus|dildo|"
    r"\bporn|masturbat|nymphoman|nymphet|molest|prostitu|"
    r"\brapist|\braping\b|\braped\b|gigolo|orgasm|\bjizz\b|"
    r"jerk\s?off|jack\s?off|\bwank(?:er|ers|ing|ed|s)?\b|"
    r"sodom|\bincest|inceste|bestialit|coitus|co\u00eft|p(?:a?e)dophil",
    re.IGNORECASE,
)
# GERMAN-only crude stems (applied only to de content).
REMOVE_DE = re.compile(
    r"ficken|gefickt|fickbar|verfickt|durchfick|\bfick\b|"
    r"schei(?:ss|\u00df)|beschissen|\bschiss\b|"
    r"verarsch|arschloch|arschkriech|\barsch\b|"
    r"verpiss|anpiss|\bpissen\b|"
    r"poppen|bumsen|kacken|wichs|muschi|titten|hurensohn|"
    r"schlampe|versaut|schwuchtel|\bnutte|\bfotze|\bhure\b|schwanzlutsch",
    re.IGNORECASE,
)
# FRENCH-only crude stems (applied only to fr content).
REMOVE_FR = re.compile(
    r"\bputain|\bpute\b|connard|connasse|\bsalope\b|"
    r"encul(?:er|eur|euse|\u00e9|\u00e9e|\u00e9s|ade|age|ait|ons|ez)|"
    r"merde|foutre|foutrement|\bfoutu|"
    r"zizi|\bzob\b|\bnichon|foufoune|\bbranl|p\u00e9tasse|"
    r"godemich|couille|\bbite\b|\bgode\b|chiotte|salaud",
    re.IGNORECASE,
)

# Per-language EXACT headword removals for unambiguous terms awkward to stem.
REMOVE_EXACT = {
    "en": {"twat", "wanker", "bollocks", "arse", "arsehole", "asshole"},
    "de": set(),
    "fr": set(),
}

# Allowlist: NEVER flag these (confirmed benign homographs / legit vocabulary).
# NOTE: "bite" is intentionally NOT here -- EN "bite" (to bite) is already safe
# because only REMOVE_FR contains it, so it's flagged only in French content.
KEEP_EXACT = {
    "dick", "retard", "queue", "sau", "mist", "bordel",
    "swanky", "swank", "fickle", "fickleness", "trafficker",
    "wanken", "schwanken", "schwankend", "schwankenden", "wankelm\u00fctig",
    "schwank", "drehzahlschwankung",
    "pute", "salopette", "saloperie", "saloper",
    "pique-niquer", "pique-nique", "communiquer", "paniquer",
    "\u00e9branler", "cornichon", "pissenlit", "enculturation",
    "nympholept", "nympholepsy", "forniquer", "branle-bas",
}

# Hard, unambiguous profanity in EXAMPLE text (keep the word, replace only the
# example). Deliberately tiny/anchored -- broad terms and Turkish tokens are
# excluded (re.IGNORECASE folds Turkish dotless-\u0131 into ascii i, so
# "am\u0131na" would match "ex-amina-tion").
EXAMPLE_CRUDE_RE = re.compile(
    r"\bfuck|motherfuck|\bcunt(?:s|y|ish)?\b|nigger|faggot|arschkriech|arschloch|"
    r"\bficken\b|gefickt|\bfotze|\bwichser|hurensohn|schei\u00dfkerl|casse-couille",
    re.IGNORECASE,
)

_ART_RE = re.compile(r"^(?:der|die|das|le|la|les|un|une|l['\u2019])\s*", re.IGNORECASE)


def bare(word):
    """Article-stripped, casefolded headword for comparison."""
    return _ART_RE.sub("", word or "").strip().casefold()


def is_inappropriate(word, lang="en"):
    """True if `word` is crude/vulgar/explicit/slur and should be excluded."""
    b = bare(word)
    if not b or b in KEEP_EXACT:
        return False
    if b in REMOVE_EXACT.get(lang, set()):
        return True
    if REMOVE_GLOBAL.search(b):
        return True
    if lang == "de" and REMOVE_DE.search(b):
        return True
    if lang == "fr" and REMOVE_FR.search(b):
        return True
    return False


def has_crude_example(example):
    """True if an example sentence contains hard profanity (scrub it)."""
    return bool(example) and bool(EXAMPLE_CRUDE_RE.search(example))
