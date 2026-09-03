"""How much placeholder/stub content actually reaches the screen?

Compares the regex the APP uses to gate content (shared.js isPlaceholderExample)
against the broader one the AUDIT uses (scripts/_diag_wordquality.js FALLBACK),
so the gap between "flagged by the audit" and "actually hidden in the UI" is
visible. Also counts synonym-list stub definitions and over-long definitions.

    python scripts/_diag_ui_leaks.py
"""

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")

# What shared.js currently gates on.
APP = re.compile(
    r"^(I am learning the word|Ich lerne das Wort|J['\u2019]apprends le mot"
    r"|No example sentence available|Kein Beispielsatz|Aucune phrase d'exemple"
    r"|No hay frase de ejemplo|Nessuna frase di esempio|Nenhuma frase de exemplo)\b"
)
# What the audit flags (scripts/_diag_wordquality.js).
AUDIT = re.compile(
    r"No example sentence available|No dictionary definition available"
    r"|No definition available|Kein Beispielsatz|Aucune phrase d'exemple"
    r"|Bu kelime i\u00e7in (\u00f6rnek c\u00fcmle|s\u00f6zl\u00fck tan\u0131m\u0131)"
    r"|\u00d6rnek c\u00fcmle (bulunmuyor|mevcut de\u011fil)"
    r"|Tan\u0131m (bulunmuyor|mevcut de\u011fil)",
    re.I,
)
SYNLIST = re.compile(
    r"^\s*(Similar to|\u00c4hnlich wie|Semblable \u00e0|Simile a|Similar a"
    r"|Benzer|\u015euna benzer|Benzeri)\s*:",
    re.I,
)

FIELD = re.compile(r'(definition|example)\s*:\s*"((?:[^"\\]|\\.)*)"')


def native(s):
    # data/*.js pack "native - Turkish" (words*.js) or "native;Turkish" (synant*).
    for sep in (" - ", ";"):
        i = s.find(sep)
        if i != -1:
            return s[:i]
    return s


def main():
    counts = {
        "examples": 0,
        "ex_app": 0,
        "ex_audit": 0,
        "ex_leak": 0,
        "defs": 0,
        "def_fallback": 0,
        "def_synlist": 0,
        "def_160": 0,
        "def_300": 0,
    }
    for name in sorted(os.listdir(DATA)):
        if not name.endswith(".js"):
            continue
        with open(os.path.join(DATA, name), encoding="utf-8") as fh:
            text = fh.read()
        for kind, raw in FIELD.findall(text):
            val = raw.encode().decode("unicode_escape", "ignore") if "\\u" in raw else raw
            if kind == "example":
                counts["examples"] += 1
                a, b = bool(APP.match(val.strip())), bool(AUDIT.search(val))
                counts["ex_app"] += a
                counts["ex_audit"] += b
                counts["ex_leak"] += b and not a
            else:
                counts["defs"] += 1
                n = native(val)
                counts["def_fallback"] += bool(AUDIT.search(val))
                counts["def_synlist"] += bool(SYNLIST.match(n))
                if len(n) > 300:
                    counts["def_300"] += 1
                elif len(n) > 160:
                    counts["def_160"] += 1

    print("examples scanned            %7d" % counts["examples"])
    print("  placeholder (audit)       %7d" % counts["ex_audit"])
    print("  caught by shared.js       %7d" % counts["ex_app"])
    print("  LEAKING to the UI         %7d" % counts["ex_leak"])
    print("")
    print("definitions scanned         %7d" % counts["defs"])
    print("  'no definition' fallback  %7d" % counts["def_fallback"])
    print("  synonym-list stub         %7d" % counts["def_synlist"])
    print("  over 160 chars            %7d" % counts["def_160"])
    print("  over 300 chars            %7d" % counts["def_300"])
    return 0


if __name__ == "__main__":
    sys.exit(main())
