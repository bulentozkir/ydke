#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Scan every data/*.js example and definition for biased or unsuitable content.

Four categories, deliberately separated because they need different handling:

  PROFANITY  swearing in any of the seven languages present (6 source + tr)
  SEXUAL     explicit sexual acts or anatomy used sexually
  RELIGION   takes a POSITION on religion (denies/asserts God, proselytises,
             mocks belief) -- as opposed to merely mentioning a church
  POLITICS   partisan content: named living politicians, party advocacy,
             ideological or nationalist assertions

Merely topical sentences ("The church was built in 1850", "She voted in the
election") are not bias and must not be flagged, so the religion and politics
patterns match stance constructions rather than bare topic nouns.

Usage:
  python scripts/_diag_bias.py                 summary + samples
  python scripts/_diag_bias.py --cat RELIGION  every hit in one category
  python scripts/_diag_bias.py --json out.json
"""
import collections
import json
import re
import sys

import _udsp_data as U

# ---------------------------------------------------------------- categories
# Bare stems; \b-wrapped and matched case-insensitively below. Kept explicit
# rather than clever so a false positive can be traced to one entry.
PROFANITY = [
    # en
    r"fuck\w*", r"shit\w*", r"bullshit", r"bitch(es|y)?", r"bastard\w*",
    r"asshole\w*", r"arsehole\w*", r"dickhead\w*", r"motherfucker\w*",
    r"wanker\w*", r"prick(s)?\b(?! up)", r"twat\w*", r"cunt\w*",
    r"goddamn\w*", r"god damn", r"damn it", r"dammit", r"piss(ed|ing)? off",
    r"son of a bitch", r"crap(py)?\b", r"bloody hell", r"screw you",
    # de
    r"schei(ss|ß)e", r"arschloch\w*", r"verdammte?r?\b", r"wichser",
    r"fotze\w*", r"hurensohn\w*",
    # fr
    r"merde\w*", r"putain\w*", r"connard\w*", r"connasse\w*", r"enculé\w*",
    r"salope\w*", r"bordel de merde",
    # es
    r"mierda\w*", r"joder", r"cabrón\w*", r"gilipollas", r"hijo de puta",
    r"coño\w*", r"puta madre",
    # it
    r"cazzo\w*", r"stronzo\w*", r"vaffanculo", r"merda\w*", r"figlio di puttana",
    # pt
    r"merda\w*", r"caralho\w*", r"foda-se", r"filho da puta", r"porra\b",
    # tr
    r"siktir", r"orospu\w*", r"piç\w*", r"amcık\w*", r"göt veren",
    r"yarrak\w*", r"kahrolası", r"lanet olsun",
]

SEXUAL = [
    r"blow ?job\w*", r"hand ?job\w*", r"masturbat\w*", r"onanis\w*",
    r"orgasm\w*", r"ejaculat\w*", r"cunnilingus", r"fellatio", r"anal sex",
    r"oral sex", r"have sex with", r"having sex", r"had sex",
    r"sexual intercourse", r"make love to", r"making love", r"porn\w*",
    r"prostitut\w*", r"whore(s|house)?", r"brothel\w*", r"strip ?club\w*",
    r"erotic\w*", r"aroused sexually", r"sexually aroused", r"horny",
    r"genital\w*", r"testicle\w*", r"scrotum", r"vagina\w*", r"penis\w*",
    r"clitoris", r"nipple\w*", r"buttock\w*",
    r"onanieren", r"geschlechtsverkehr", r"schwanz lutsch\w*",
    r"masturbation", r"rapport sexuel\w*", r"faire l'amour",
    r"relación sexual\w*", r"hacer el amor", r"rapporto sessuale",
    r"fare l'amore", r"relação sexual", r"fazer amor",
    r"cinsel ilişki\w*", r"sevişmek", r"mastürbasyon",
]

# Stance, not topic. "God" alone is a topic (mythology, "Gottesacker" = graveyard,
# a definition of "blasphemy"); denying or asserting God is not. An earlier
# version of this list missed "Not only is THERE NO GOD, but try getting a
# plumber on weekends" because it only matched the "there is no god" word order,
# so every existence claim is now matched order-independently.
RELIGION = [
    r"no god\b", r"there (is|was) no god", r"(is|was|are|were) there no god",
    r"no such thing as god", r"god (does|did|doesn't|does not) exist",
    r"god is (dead|a myth|an? illusion|imaginary|fiction)",
    r"religion is (the )?(opium|poison|a lie|nonsense|superstition|evil)",
    r"believe in god", r"belief in god", r"faith in god",
    r"god (created|made) (the world|man|us|everything)",
    r"accept (jesus|christ)", r"jesus (saves|is lord|died for)",
    r"the true (faith|religion)", r"the only (true )?(god|religion|faith)",
    r"(atheis|theis)\w* (is|are) (right|wrong|true|false)",
    r"burn in hell", r"go to hell(?! and back)", r"eternal damnation",
    r"heathen\w*", r"heretic\w*", r"blasphem\w*",
    r"holy war", r"jihad\w*", r"crusade against",
    r"kein(en)? gott\b", r"es gibt keinen gott", r"gott existiert nicht",
    r"gott ist tot", r"gott.{0,20}nicht existier\w*",
    r"pas de dieu\b", r"dieu n'existe pas", r"il n'y a pas de dieu",
    r"dieu est mort",
    r"no (hay|existe) dios", r"dios no existe", r"dios (ha )?muerto",
    r"non (c'è|esiste) dio", r"dio non esiste", r"dio è morto",
    r"n(ã|a)o (h(á|a)|existe) deus", r"deus n(ã|a)o existe", r"deus (está )?morto",
    r"tanrı yok\w*", r"tanrı(nın)? (olmama|olmadığı|öldü)\w*", r"tanrı öl\w*",
    r"tanrı(ya)? inan\w*", r"allah yoktur", r"din bir (afyon|yalan)",
]

POLITICS = [
    # Named contemporary figures and parties: any sentence built around one of
    # these is taking a side by construction in a vocabulary drill.
    r"trump\b(?!s?\b.{0,12}(card|suit|ace))", r"biden", r"obama", r"clinton",
    r"putin", r"zelensky", r"netanyahu", r"erdo(g|ğ)an", r"merkel", r"macron",
    r"bolsonaro", r"maduro", r"hitler", r"stalin", r"mussolini", r"franco\b",
    r"mao zedong", r"pol pot", r"saddam", r"gaddafi", r"assad",
    r"republican party", r"democratic party", r"labour party", r"tory\w*",
    r"the (left|right) wing\w* (are|is)",
    # Ideological assertion rather than description.
    r"(communism|socialism|capitalism|fascism|zionism) (is|was) (evil|good|the best|a failure|a crime|superior)",
    r"immigrants? (are|take|steal|ruin)", r"refugees? (are|take|steal|ruin)",
    r"(muslims|jews|christians|arabs|blacks|whites|gypsies) (are|should|must)",
    r"should be (deported|expelled|banned)",
    r"race war", r"ethnic cleansing", r"master race", r"white power",
    r"terrorist state", r"illegal alien\w*",
    r"vote (for|against) (the )?(party|him|her|them)",
    r"long live the (revolution|party|leader)",
]

CATS = collections.OrderedDict([
    ("PROFANITY", PROFANITY),
    ("SEXUAL", SEXUAL),
    ("RELIGION", RELIGION),
    ("POLITICS", POLITICS),
])
RX = {c: re.compile(r"\b(?:" + "|".join(pats) + r")", re.I | re.U)
      for c, pats in CATS.items()}

cat_filter = None
if "--cat" in sys.argv:
    cat_filter = sys.argv[sys.argv.index("--cat") + 1].upper()
json_out = sys.argv[sys.argv.index("--json") + 1] if "--json" in sys.argv else None

found = []
counts = collections.Counter()
per_file = collections.Counter()

for name in U.word_files():
    text, entries = U.load(name)
    for e in entries:
        w = U.unescape(e.get("word", "")).strip()
        dfn = U.unescape(e.get("definition", ""))
        for field in ("example", "definition"):
            val = U.unescape(e.get(field, ""))
            if not val:
                continue
            for cat, rx in RX.items():
                m = rx.search(val)
                if not m:
                    continue
                counts[cat] += 1
                per_file[(cat, name)] += 1
                found.append({"file": name, "word": w, "field": field,
                              "cat": cat, "hit": m.group(0), "text": val,
                              "dfn": dfn})

print("")
for cat in CATS:
    if cat_filter and cat != cat_filter:
        continue
    print("== %-10s %5d" % (cat, counts[cat]))
    worst = sorted([(v, k[1]) for k, v in per_file.items() if k[0] == cat],
                   reverse=True)[:10]
    for n, f in worst:
        print("      %5d  %s" % (n, f))
    rows = [r for r in found if r["cat"] == cat]
    limit = len(rows) if cat_filter else 12
    for r in rows[:limit]:
        print("         [%s] %s (%s) <%s> %s"
              % (r["file"], r["word"], r["field"], r["hit"], r["text"][:110]))
    print("")

print("TOTAL flagged field values: %d" % len(found))
if json_out:
    with open(json_out, "w", encoding="utf-8") as fh:
        json.dump(found, fh, ensure_ascii=False, indent=1)
    print("wrote %s" % json_out)
