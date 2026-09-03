"""Compare validation findings between git HEAD and the working tree."""
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FIELD_RE = re.compile(
    r'(?P<key>\bword|\blevel|\bcategory|\bdefinition|\bexample|\bsynonyms|\bantonyms)'
    r'\s*:\s*"(?P<val>(?:[^"\\]|\\.)*)"'
)
ENTRY_RE = re.compile(r"\{(?P<body>[^{}]*)\}")


def findings(text):
    out = {}
    out["cr_lines"] = [i + 1 for i, ln in enumerate(text.split("\n")) if "\r" in ln]
    out["dbl"] = [m.start() for m in re.finditer(r"\\\\", text)]
    entries = []
    for m in ENTRY_RE.finditer(text):
        d = dict(FIELD_RE.findall(m.group("body")))
        if "word" in d:
            entries.append(d)
    out["n"] = len(entries)
    out["badsep"] = sorted(
        e["word"] for e in entries
        if e.get("definition", "").count(";") != 1 or e.get("example", "").count(";") != 1
    )
    return out


head = subprocess.run(
    ["git", "show", "HEAD:data/synantde.js"], cwd=ROOT, capture_output=True, check=True
).stdout.decode("utf-8")
with open(os.path.join(ROOT, "data", "synantde.js"), encoding="utf-8", newline="") as fh:
    cur = fh.read()

a, b = findings(head), findings(cur)
print(f"entries   HEAD={a['n']}  now={b['n']}  (delta {b['n'] - a['n']})")
print(f"CR lines  HEAD={len(a['cr_lines'])}  now={len(b['cr_lines'])}  -> {b['cr_lines'][:10]}")
print(f"'\\\\\\\\'    HEAD={len(a['dbl'])}  now={len(b['dbl'])}")
print(f"badsep    HEAD={len(a['badsep'])}  now={len(b['badsep'])}")
new_bad = sorted(set(b["badsep"]) - set(a["badsep"]))
gone_bad = sorted(set(a["badsep"]) - set(b["badsep"]))
print(f"  NEW badsep introduced by this pass: {len(new_bad)} {new_bad}")
print(f"  badsep removed by this pass       : {len(gone_bad)} {gone_bad[:20]}")
sys.exit(0)
