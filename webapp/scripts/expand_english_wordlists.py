#!/usr/bin/env python3
"""Expand selected English vocab files to a minimum entry count.

Source (internet):
https://raw.githubusercontent.com/dwyl/english-words/master/words_alpha.txt

This script preserves existing entries and appends auto-generated entries until
all target files reach TARGET_PER_FILE words.
"""

import re
import urllib.request

TARGET_PER_FILE = 3000
SOURCE_URL = "https://raw.githubusercontent.com/dwyl/english-words/master/words_alpha.txt"

FILE_SPECS = [
    (r"c:\\gitrepo\\udsp\\data\\wordsb2.js", "B2"),
    (r"c:\\gitrepo\\udsp\\data\\wordsc1.js", "C1"),
    (r"c:\\gitrepo\\udsp\\data\\wordsc2.js", "C2"),
]

WORD_RE = re.compile(r'word:\s*"([^"]+)"')
VALID_WORD_RE = re.compile(r"^[a-z][a-z\-']{2,}$")


def js_escape(s: str) -> str:
    return s.replace("\\", "\\\\").replace('"', '\\"')


def infer_pos(word: str) -> str:
    if word.endswith(("ly",)):
        return "adverb"
    if word.endswith(("ing", "ed", "en", "ify", "ise", "ize")):
        return "verb"
    return "noun"


def parse_source_words() -> list[str]:
    req = urllib.request.Request(SOURCE_URL, headers={"User-Agent": "TopWordsApp/1.0"})
    with urllib.request.urlopen(req, timeout=30) as resp:
        data = resp.read().decode("utf-8", errors="replace")

    out = []
    seen = set()
    for raw in data.splitlines():
        word = raw.strip().lower()
        if not VALID_WORD_RE.match(word):
            continue
        if word in seen:
            continue
        seen.add(word)
        out.append(word)
    return out


def build_entry(word: str, level: str) -> str:
    pos = infer_pos(word)
    definition = "English vocabulary word from open-source list. - Açık kaynak listeden İngilizce kelime."
    example = f'I am learning the word "{word}". - "{word}" kelimesini öğreniyorum.'

    return (
        "  {\n"
        f'    word: "{js_escape(word)}",\n'
        f'    pos: "{pos}",\n'
        f'    level: "{level}",\n'
        f'    definition: "{js_escape(definition)}",\n'
        f'    example: "{js_escape(example)}",\n'
        "  },\n"
    )


def expand_file(path: str, level: str, source_words: list[str], used_global: set[str]) -> tuple[int, int]:
    text = open(path, encoding="utf-8").read()
    existing_words = WORD_RE.findall(text)
    existing_set = {w.casefold() for w in existing_words}

    current = len(existing_words)
    need = max(0, TARGET_PER_FILE - current)
    if need == 0:
        return current, 0

    additions = []
    for w in source_words:
        k = w.casefold()
        if k in existing_set or k in used_global:
            continue
        additions.append(w)
        used_global.add(k)
        if len(additions) >= need:
            break

    if not additions:
        return current, 0

    insert_at = text.rfind("];")
    if insert_at < 0:
        raise RuntimeError(f"Could not find array terminator in {path}")

    header_note = "// Expanded with internet source words from words_alpha.txt (GitHub).\n"
    if header_note not in text:
        first_newline = text.find("\n")
        if first_newline != -1:
            text = text[: first_newline + 1] + header_note + text[first_newline + 1 :]
            insert_at = text.rfind("];")

    blocks = "".join(build_entry(w, level) for w in additions)
    updated = text[:insert_at] + blocks + text[insert_at:]

    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(updated)

    return current, len(additions)


def main() -> None:
    source_words = parse_source_words()
    if not source_words:
        raise RuntimeError("No source words parsed from internet source")

    used_global: set[str] = set()
    for path, _level in FILE_SPECS:
        text = open(path, encoding="utf-8").read()
        for w in WORD_RE.findall(text):
            used_global.add(w.casefold())

    for path, level in FILE_SPECS:
        before, added = expand_file(path, level, source_words, used_global)
        after = before + added
        print(f"{path}: before={before}, added={added}, after={after}")


if __name__ == "__main__":
    main()
