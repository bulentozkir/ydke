# -*- coding: utf-8 -*-
"""Add Portuguese everywhere Spanish already appears.

Adding a language to this app touches far more than the data files. This
script performs the mechanical, repetitive part of that sweep - the language
<option> on every mode page, the Turkish/English phrases that enumerate the
supported languages, and the SEO metadata - so the only hand edits left are
the genuinely structural ones (a new help.html section, an about.html list
item, home.js language maps).

Two traps this script is written to avoid, both caught in an earlier dry run:

1. CASCADING RULES. The Turkish enumeration appears with several line-wrap
   points, so the rule list contains both "... Italyanca ve Ispanyolca" and
   "... Italyanca, Ispanyolca". Applied naively the second rule re-matches the
   output of the first and produces "... ve Portekizce ve Portekizce". Every
   replacement therefore writes a sentinel that no later rule can match, and
   the sentinels are expanded once at the end.

2. OVER-BROAD KEYWORD RULE. "Ispanyolca kelime ezberleme" is almost always
   part of the long enumeration handled above; it stands alone only in
   index.html's keywords meta. Appending to it blindly corrupts the Spanish
   `tagline` and `description` in shared.js, so that rule matches the longer
   keyword pair instead.

Run: python scripts/add_portuguese.py --apply
"""
import io
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# (search, replace). More specific forms of the same sentence come first;
# sentinels stop them cascading into each other.
RULES = [
    # language picker on every mode page
    ('            <option value="es">Spanish</option>\n',
     '            <option value="es">Spanish</option>\n'
     '            <option value="pt">Portuguese</option>\n'),

    # Turkish enumerations - every observed line-wrap variant
    ("İngilizce, Almanca, Fransızca, İtalyanca ve İspanyolca",
     "İngilizce, Almanca, Fransızca, İtalyanca, İspanyolca ve Portekizce"),
    ("İngilizce, Almanca, Fransızca, İtalyanca, İspanyolca",
     "İngilizce, Almanca, Fransızca, İtalyanca, İspanyolca, Portekizce"),
    ("Almanca, Fransızca, İtalyanca ve İspanyolca",
     "Almanca, Fransızca, İtalyanca, İspanyolca ve Portekizce"),
    ("İngilizce, Almanca, Fransızca ve İspanyolca",
     "İngilizce, Almanca, Fransızca, İspanyolca ve Portekizce"),

    # English enumerations
    ("English, German, French, Italian and Spanish",
     "English, German, French, Italian, Spanish and Portuguese"),
    ("English, German, French, Italian, Spanish",
     "English, German, French, Italian, Spanish, Portuguese"),

    # counts
    ("5 dil", "6 dil"),
    ("5 languages", "6 languages"),
    ("beş dil", "altı dil"),

    # SEO keywords - the long pair, so shared.js's Spanish tagline is untouched
    ("İspanyolca kelime ezberleme, İspanyolca kelime öğrenme",
     "İspanyolca kelime ezberleme, İspanyolca kelime öğrenme, "
     "Portekizce kelime ezberleme, Portekizce kelime öğrenme"),
    ("spanish vocabulary", "spanish vocabulary, portuguese vocabulary"),

    # locale alternates
    ('<meta property="og:locale:alternate" content="es_ES" />',
     '<meta property="og:locale:alternate" content="es_ES" />\n'
     '    <meta property="og:locale:alternate" content="pt_BR" />'),
]

SENTINEL = "\x00UDSP%d\x00"


def main():
    apply_changes = "--apply" in sys.argv
    names = sorted(n for n in os.listdir(ROOT)
                   if n.endswith((".html", ".js", ".webmanifest")))
    changed = 0

    for name in names:
        path = os.path.join(ROOT, name)
        if not os.path.isfile(path):
            continue
        text = io.open(path, encoding="utf-8").read()
        original = text
        hits = []

        # pass 1: each rule writes a sentinel later rules cannot re-match
        for i, (search, replace) in enumerate(RULES):
            if search not in text or replace in text:
                continue
            hits.append("%s x%d" % (search[:30].replace("\n", "").strip(),
                                    text.count(search)))
            text = text.replace(search, SENTINEL % i)

        # pass 2: expand the sentinels
        for i, (_search, replace) in enumerate(RULES):
            text = text.replace(SENTINEL % i, replace)

        assert "\x00" not in text, name
        if text != original:
            changed += 1
            print("%-28s %s" % (name, "; ".join(hits)))
            if apply_changes:
                io.open(path, "w", encoding="utf-8", newline="\n").write(text)

    print("\n%d files %s"
          % (changed, "updated" if apply_changes else "would change (DRY RUN)"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
