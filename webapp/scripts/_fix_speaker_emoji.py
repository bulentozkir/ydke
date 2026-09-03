"""One-off: repair the corrupted speaker emoji in help.html.

The 🔊 (U+1F50A) in the three flashcard button descriptions (TR/EN/DE) was
mangled into invalid UTF-8 bytes during an earlier edit and now renders as
the replacement character. Read with surrogateescape so the bad bytes are
preserved, then replace the junk char sitting immediately before
'dinle' / 'listen' / 'anhören' with a correct 🔊.
"""
import re

PATH = r"c:\gitrepo\udsp\help.html"
SPK = "\U0001F50A"  # 🔊

with open(PATH, "r", encoding="utf-8", errors="surrogateescape") as f:
    text = f.read()

before = text
text = re.sub(r"(\n +)\S* dinle, ", r"\g<1>" + SPK + " dinle, ", text)
text = re.sub(r"Buttons: \S* listen, ", "Buttons: " + SPK + " listen, ", text)
text = re.sub(r"Tasten: \S* anh\u00f6ren, ", "Tasten: " + SPK + " anh\u00f6ren, ", text)

with open(PATH, "w", encoding="utf-8", errors="surrogateescape") as f:
    f.write(text)

print("changed:", before != text)

# Report any remaining lone surrogates / replacement chars anywhere in the file
bad = [(i + 1, ln) for i, ln in enumerate(text.splitlines())
       if any(0xDC80 <= ord(c) <= 0xDCFF or c == "\uFFFD" for c in ln)]
print("remaining bad-byte lines:", bad if bad else "none")

# Show the three repaired lines
for i, ln in enumerate(text.splitlines(), 1):
    if ("dinle, " in ln) or ("Buttons: " in ln and "listen" in ln) or ("Tasten: " in ln and "anh" in ln):
        print(i, repr(ln.strip()[:70]))
