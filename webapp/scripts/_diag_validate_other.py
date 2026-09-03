"""Post-fix validation for synantde.js / toefl.js / partikelverbde.js."""
import os
import re
import sys
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FIELD_RE = re.compile(
    r'(?P<key>\bword|\blevel|\bcategory|\bdefinition|\bexample|\bsynonyms|\bantonyms|\bpos)'
    r'\s*:\s*"(?P<val>(?:[^"\\]|\\.)*)"'
)
ENTRY_RE = re.compile(r"\{(?P<body>[^{}]*)\}")

ok = True
for name, sep in (("synantde.js", ";"), ("toefl.js", " - "), ("partikelverbde.js", " - ")):
    path = os.path.join(ROOT, "data", name)
    with open(path, encoding="utf-8", newline="") as fh:
        text = fh.read()

    print(f"===== {name}")
    if text.count("{") != text.count("}"):
        ok = False
        print(f"  !! BRACE MISMATCH  {{={text.count('{')}  }}={text.count('}')}")
    if "\r" in text:
        ok = False
        print("  !! CR found (file must stay LF-only)")
    if re.search(r"\\\\", text):
        ok = False
        print("  !! double backslash found (escaping bug)")

    entries = [dict(FIELD_RE.findall(m.group("body"))) for m in ENTRY_RE.finditer(text)]
    entries = [{k: v for k, v in e.items()} for e in entries]
    entries = [e for e in entries if "word" in e]
    print(f"  entries: {len(entries)}")

    words = [e["word"] for e in entries]
    dupes = [w for w, c in Counter(words).items() if c > 1]
    if dupes:
        ok = False
        print(f"  !! EXACT DUPLICATE HEADWORDS: {len(dupes)} {dupes[:20]}")
    else:
        print("  exact duplicate headwords: 0")

    bad_sep = [e["word"] for e in entries
               if e.get("definition", "").count(sep) != 1
               or e.get("example", "").count(sep) != 1]
    if name == "synantde.js" and bad_sep:
        ok = False
        print(f"  !! BAD SEPARATOR COUNT: {len(bad_sep)} {bad_sep[:20]}")
    elif bad_sep:
        print(f"  entries without exactly one '{sep}': {len(bad_sep)} (informational)")

    blanks = [e["word"] for e in entries
              if not e.get("definition", "").strip() or not e.get("example", "").strip()]
    if blanks:
        ok = False
        print(f"  !! EMPTY definition/example: {len(blanks)} {blanks[:20]}")

print()
print("RESULT:", "PASS" if ok else "FAIL")
sys.exit(0 if ok else 1)
