# -*- coding: utf-8 -*-
"""Print the full text of fields that still hold more than one " - "."""
import io
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")
DATA = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "data")
ENTRY_RE = re.compile(r"\{[^{}]*\}", re.S)
F = {k: re.compile(r'%s:\s*"((?:\\.|[^"\\])*)"' % k)
     for k in ("word", "definition", "example")}

for name in sorted(os.listdir(DATA)):
    if not name.endswith(".js"):
        continue
    text = io.open(os.path.join(DATA, name), encoding="utf-8").read()
    for block in ENTRY_RE.findall(text):
        w = F["word"].search(block)
        if not w:
            continue
        for k in ("definition", "example"):
            m = F[k].search(block)
            if m and m.group(1).count(" - ") > 1:
                print("[%s] %s (%s)\n    %s\n" % (name, w.group(1), k, m.group(1)))
