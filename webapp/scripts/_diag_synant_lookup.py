import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fix_synant_formof as F  # noqa: E402

WANT = [
    "Kind", "Brief", "Kratze", "Brause", "Keim", "Ahn", "Abgabe", "Hieb",
    "Blatt", "Ding", "Gut", "Ma\u00df", "Decke", "Detail", "Gattung",
    "\u00e4u\u00dferst", "\u00fcberh\u00e4ufen", "\u00c4u\u00dferes", "letzt", "letzte", "divers",
    "inner", "schnell", "all", "allen", "ander", "anderer", "einig",
    "der", "dieser", "irgendein", "alternativ", "Alternative",
    "Nachkomme", "Bekannte", "Kraft", "Schuh", "Strumpf", "Million",
    "Nudel", "Chance", "Fluse", "All\u00fcre", "Stichpunkt",
    "Gro\u00dfbuchstabe", "Hautschuppe", "Intimit\u00e4t", "H\u00fclsenfrucht",
    "Gewissensbiss", "Bauchschmerz", "Handgreiflichkeit", "Daten", "Datum",
    "Gefolgsmann", "Gefolgsleute", "Besonderheit", "Anlage", "Angabe",
]

_, entries = F.parse()
exact = {e["word"] for e in entries}
fold = {}
for e in entries:
    fold.setdefault(e["word"].casefold(), []).append(e["word"])

for w in WANT:
    print(f"{w:24} exact={w in exact!s:5} fold={fold.get(w.casefold(), [])}")
