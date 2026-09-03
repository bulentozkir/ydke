"""Normalise data/synantde.js back to LF-only line endings."""
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
p = os.path.join(ROOT, "data", "synantde.js")
with open(p, "rb") as fh:
    raw = fh.read()
before = raw.count(b"\r\n")
raw = raw.replace(b"\r\n", b"\n").replace(b"\r", b"\n")
with open(p, "wb") as fh:
    fh.write(raw)
print(f"converted {before} CRLF -> LF; remaining CR: {raw.count(b'\r')}")
