#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Content-quality predicates shared by the fix passes.

PLACEHOLDER_EXAMPLE_RE must stay in lockstep with isPlaceholderExample() in
shared.js -- the app hides the "Example" button and skips Cloze/Sentence
Scramble for anything it matches, so a wording that exists in the data but
not in one of the two regexes silently leaks a content-free sentence into a
game.
"""
import re
import unicodedata

# Mirrors shared.js isPlaceholderExample()
PLACEHOLDER_EXAMPLE_RE = re.compile(
    r"^(I am learning the word|Ich lerne das Wort|J['\u2019]apprends le mot"
    r"|No example sentence available|Kein Beispielsatz|Aucune phrase d'exemple"
    r"|Estoy aprendiendo la palabra|Sto imparando la parola"
    r"|Estou a aprender a palavra|Nessuna frase di esempio"
    r"|No hay frase de ejemplo|Nenhuma frase de exemplo)\b")

# Mirrors shared.js STUB_DEFINITION
STUB_DEFINITION_RE = re.compile(
    r"^\s*(Similar to|\u00c4hnlich wie|Semblable \u00e0|Simile a|Similar a"
    r"|Benzer|\u015euna benzer|Benzeri)\s*:", re.I)

NO_DEFINITION_RE = re.compile(
    r"^\s*(No dictionary definition available|Bu kelime i\u00e7in s\u00f6zl\u00fck)",
    re.I)

TOKEN_RE = re.compile(r"[^\W\d_]+", re.UNICODE)


def is_placeholder_example(example):
    return bool(example) and bool(PLACEHOLDER_EXAMPLE_RE.match(example.strip()))


def is_stub_definition(definition):
    return bool(definition) and bool(STUB_DEFINITION_RE.match(definition))


def is_no_definition(definition):
    return bool(definition) and bool(NO_DEFINITION_RE.match(definition))


def fold(s):
    """Case- and diacritic-insensitive fold (mirrors shared.js foldText)."""
    s = (s or "").replace("\u00df", "ss")
    s = unicodedata.normalize("NFD", s)
    s = "".join(c for c in s if not unicodedata.combining(c))
    return s.lower().strip()


def _prefix_len(a, b):
    n = min(len(a), len(b))
    i = 0
    while i < n and a[i] == b[i]:
        i += 1
    return i


# A shared prefix is a plausible inflection only for regular morphology.  These
# English verbs change their stem outright ("sell" -> "sold", "take" -> "took"),
# and they head most of phrasalverbsen.js, so without them 421 perfectly good
# example sentences look as though they never use their own headword.
_IRREGULAR = {
    "be": "am is are was were been being", "begin": "began begun",
    "bite": "bit bitten", "blow": "blew blown", "break": "broke broken",
    "bring": "brought", "build": "built", "burn": "burnt", "buy": "bought",
    "catch": "caught", "choose": "chose chosen", "come": "came",
    "deal": "dealt", "dig": "dug", "do": "does did done",
    "draw": "drew drawn", "dream": "dreamt", "drink": "drank drunk",
    "drive": "drove driven", "eat": "ate eaten", "fall": "fell fallen",
    "feed": "fed", "feel": "felt", "fight": "fought", "find": "found",
    "fly": "flew flown", "forget": "forgot forgotten",
    "forgive": "forgave forgiven", "freeze": "froze frozen",
    "get": "got gotten", "give": "gave given", "go": "goes went gone",
    "grow": "grew grown", "hang": "hung", "have": "has had having",
    "hear": "heard", "hide": "hid hidden", "hold": "held", "keep": "kept",
    "know": "knew known", "lay": "laid", "lead": "led", "learn": "learnt",
    "leave": "left", "lend": "lent", "lie": "lay lain", "light": "lit",
    "lose": "lost", "make": "made", "mean": "meant", "meet": "met",
    "pay": "paid", "ride": "rode ridden", "ring": "rang rung",
    "rise": "rose risen", "run": "ran", "say": "said says",
    "see": "saw seen", "seek": "sought", "sell": "sold", "send": "sent",
    "shake": "shook shaken", "shine": "shone", "shoot": "shot",
    "show": "showed shown", "sing": "sang sung", "sink": "sank sunk",
    "sit": "sat", "sleep": "slept", "slide": "slid", "speak": "spoke spoken",
    "spend": "spent", "spin": "spun", "spread": "spread", "stand": "stood",
    "steal": "stole stolen", "stick": "stuck", "sting": "stung",
    "strike": "struck", "swear": "swore sworn", "sweep": "swept",
    "swim": "swam swum", "swing": "swung", "take": "took taken",
    "teach": "taught", "tear": "tore torn", "tell": "told",
    "think": "thought", "throw": "threw thrown",
    "understand": "understood", "wake": "woke woken", "wear": "wore worn",
    "win": "won", "write": "wrote written",
    "bend": "bent", "bind": "bound", "bleed": "bled", "breed": "bred",
    "cling": "clung", "creep": "crept", "flee": "fled", "fling": "flung",
    "grind": "ground", "kneel": "knelt", "leap": "leapt", "sling": "slung",
    "shrink": "shrank shrunk", "spit": "spat", "split": "split",
    "stink": "stank stunk", "string": "strung", "swell": "swollen",
    "tread": "trod trodden", "weep": "wept", "wind": "wound", "wring": "wrung",
    "cost": "cost", "cut": "cut", "hit": "hit", "hurt": "hurt", "let": "let",
    "put": "put", "read": "read", "set": "set", "shut": "shut",
}
IRREGULAR_FORMS = {k: set(v.split()) | {k} for k, v in _IRREGULAR.items()}

# German separable prefixes, longest first so "zusammen" wins over "zu".
DE_SEP_PREFIXES = sorted(
    """zusammen zurecht zurueck zuruck entgegen entlang herunter hinunter
    hervor herauf heraus herein herbei herab heran herum hinauf hinaus
    hinein hinzu hinab voran vorbei vorueber voruber weiter wieder statt
    davon dabei dazu durch empor fest fort frei gegen hinter nach nieder
    teil vor weg auf aus bei ein mit los umher zu ab an um her hin
    ueber uber unter voll hoch heim fehl gut kaputt offen tot wach wahr weh
    hierher runter drauf rauf raus rein drin drum dran gern rum ran still
    """.split(), key=len, reverse=True)

# Tokens a headword may carry as a slot rather than as literal text --
# "throw yourself into" is written "She THREW herself INTO the project."
PLACEHOLDER_TOKENS = {
    "yourself", "oneself", "myself", "himself", "herself", "itself",
    "themselves", "ourselves", "yourselves", "somebody", "someone",
    "something", "sth", "sb", "your", "his", "her", "their", "its", "our",
    "sich", "jemanden", "jemandem", "jemand", "etwas", "jdn", "jdm", "etw",
    "se", "quelqu", "quelque", "chose",
}
ARTICLE_TOKENS = {
    "der", "die", "das", "le", "la", "les", "un", "une", "des",
    "el", "los", "las", "il", "lo", "gli", "i", "o", "a", "os",
    "as", "um", "uma", "uno", "to", "l",
}


def _en_variants(wt):
    """Spellings that share this token's lemma.

    A vocabulary list stores the form the learner sees ("winning", "ruling"),
    while the sentence uses whichever form it needs ("win", "rules"), so both
    have to be reduced towards the same stem before they can be compared.
    """
    out = {wt}
    for suf in ("ing", "ed", "es", "s"):
        if wt.endswith(suf) and len(wt) - len(suf) >= 3:
            base = wt[:-len(suf)]
            out.add(base)
            out.add(base + "e")
            if len(base) >= 2 and base[-1] == base[-2]:
                out.add(base[:-1])           # winning -> winn -> win
    if wt.endswith("e"):
        out.add(wt[:-1])                     # ride -> rid(ing), move -> mov(ing)
    if wt.endswith("y") and len(wt) > 3:
        out.add(wt[:-1] + "i")               # carry -> carried
    return out


def _matches(wt, e_tokens, e_set, need_ratio=0.65, need_floor=4):
    """One headword token against the sentence, allowing regular inflection."""
    for v in _en_variants(wt):
        if v in e_set:
            return True
        forms = IRREGULAR_FORMS.get(v)
        if forms and (forms & e_set):
            return True
        need = min(max(need_floor, int(len(v) * need_ratio)), len(v))
        if any(_prefix_len(v, et) >= need and abs(len(v) - len(et)) <= 5
               for et in e_tokens):
            return True
    return False


def _skeleton(s):
    return "".join(c for c in s if c not in "aeiouy")


def _de_strong(wt, e_tokens):
    """German strong verbs change their vowel, never their consonants.

    "brennen" appears as "brannte", "quellen" as "quillt", "erkennen" as
    "erkannt" -- a shared prefix test can never see those, but the consonant
    skeleton survives the gradation intact.
    """
    if len(wt) < 5:
        return False
    a = _skeleton(wt)
    if len(a) < 3:
        return False
    for et in e_tokens:
        if len(et) < 4:
            continue
        b = _skeleton(et)
        if abs(len(a) - len(b)) <= 2 and _prefix_len(a, b) >= min(3, len(a)) \
                and _prefix_len(a, b) >= len(a) - 1:
            return True
    return False


def _de_separable(wt, e_tokens, e_set):
    """German separable verb: the prefix detaches ("stehen ... ab")."""
    for p in DE_SEP_PREFIXES:
        if not wt.startswith(p) or len(wt) - len(p) < 3:
            continue
        stem = wt[len(p):]
        # split form -- prefix stands alone at the end of the clause
        if p in e_set and (_matches(stem, e_tokens, e_set, 0.5, 3)
                           or _de_strong(stem, e_tokens)):
            return True
        # fused form -- "abgehoert", "aufzubessern", "hingefallen"
        for t in e_tokens:
            if not t.startswith(p) or len(t) - len(p) < 3:
                continue
            rest = t[len(p):]
            for cut in ("ge", "zu"):
                if rest.startswith(cut) and len(rest) > len(cut) + 2:
                    rest = rest[len(cut):]
                    break
            if _matches(stem, [rest], {rest}, 0.5, 3) or stem[0] == rest[0]:
                return True
        return False
    return False


def example_contains_word(word, example, lang="en"):
    """True if `example` plausibly uses `word`, allowing inflection.

    Multi-word headwords ("give up", "compter sur") match if every one of
    their tokens appears.  Single words match verbatim, or fuzzily when the
    shared prefix is long enough to be an inflection rather than a coincidence
    (the same heuristic czFindSpan() uses in shared.js), or via an irregular
    form, or -- in German -- as a separable verb whose prefix has moved.
    """
    if not word or not example:
        return False
    w_tokens = [fold(t) for t in TOKEN_RE.findall(word)]
    w_tokens = [t for t in w_tokens if t not in ARTICLE_TOKENS] or w_tokens
    core = [t for t in w_tokens if t not in PLACEHOLDER_TOKENS]
    if core:
        w_tokens = core
    if not w_tokens:
        return False
    e_tokens = [fold(t) for t in TOKEN_RE.findall(example)]
    if not e_tokens:
        return False
    if lang == "de":
        # a past participle hides the stem behind "ge-": "gemacht" -> "macht"
        e_tokens = e_tokens + [t[2:] for t in e_tokens
                               if t.startswith("ge") and len(t) > 4]
    e_set = set(e_tokens)
    for wt in w_tokens:
        if _matches(wt, e_tokens, e_set):
            continue
        if lang == "de":
            if _de_separable(wt, e_tokens, e_set):
                continue
            if _de_strong(wt, e_tokens):
                continue
            # German builds compounds freely: "Dekade" really is demonstrated
            # by a sentence about the "Junidekade", "Rohr" by "Zuckerrohr".
            if len(wt) >= 5 and any(wt in t for t in e_tokens):
                continue
            if len(wt) >= 4 and any(t.startswith(wt) or t.endswith(wt)
                                    for t in e_tokens if len(t) > len(wt)):
                continue
        return False
    return True
