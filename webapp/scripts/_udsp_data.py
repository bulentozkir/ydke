#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Shared read/edit layer for every data/*.js word file.

The files use several on-disk shapes that differ in whitespace, field set and
bilingual separator:

  data/words{a1..c2}.js, toefl.js, partikelverbde.js, phrasalverbs*.js
      one field per line; fields word/pos/level/category/definition/example
  data/words{a1..c2}{es,it,pt}.js
      one object per line, same fields
  data/synant{en,de,fr}.js
      one object per line, NO `pos` field, separator ";" not " - "

Rather than parse-then-reserialise (which loses blank-line grouping, trailing
newlines and per-file indent style -- verified: 26 of 43 files differ on a
naive round-trip), this module returns byte SPANS and edits are applied as
splices into the original text.  Untouched bytes stay untouched by
construction, so a pass that rewrites 12 definitions produces a 12-line diff.
"""
import io
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")

VAR_RE = re.compile(r"window\.([A-Za-z0-9_]+)\s*=\s*\[")
FIELD_RE = re.compile(r'([A-Za-z_][A-Za-z0-9_]*)\s*:\s*"((?:\\.|[^"\\])*)"')
WORD_ONLY_RE = re.compile(r'word:\s*"((?:\\.|[^"\\])*)"')
UNESCAPE_RE = re.compile(r"\\(.)")

FLAT_FILES = {"synanten.js", "synantde.js", "synantfr.js"}
SKIP_PREFIX = ("readingcomp",)


def is_flat(name):
    return os.path.basename(name) in FLAT_FILES


def sep_for(name):
    return ";" if is_flat(name) else " - "


def path_of(name):
    return name if os.path.isabs(name) else os.path.join(DATA, name)


def word_files():
    """Every data file that uses the word/level/definition/example schema."""
    return [n for n in sorted(os.listdir(DATA))
            if n.endswith(".js") and not n.startswith(SKIP_PREFIX)]


def unescape(s):
    return UNESCAPE_RE.sub(r"\1", s or "")


def escape(s):
    return s.replace("\\", "\\\\").replace('"', '\\"')


# --- parsing ------------------------------------------------------------
def _object_spans(text, start):
    """Depth-1 {...} spans inside the array literal beginning at `start`.

    A hand-rolled scanner rather than a regex: values legitimately contain
    braces and escaped quotes, and a regex trying to cope with both is exactly
    where silent entry-dropping bugs come from.
    """
    spans = []
    i = start
    n = len(text)
    depth = 0
    obj_start = -1
    in_str = False
    while i < n:
        c = text[i]
        if in_str:
            if c == "\\":
                i += 2
                continue
            if c == '"':
                in_str = False
        elif c == '"':
            in_str = True
        elif c == "{":
            if depth == 0:
                obj_start = i
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                spans.append((obj_start, i + 1))
            elif depth < 0:
                break
        elif c == "]" and depth == 0:
            break
        i += 1
    return spans


class Entry(dict):
    """A parsed entry.  Field values are RAW (still JS-escaped).

    Extra keys:
      _span   (start, end) of the whole `{...}` object
      _fields {name: (value_start, value_end)} absolute spans of each value
      _file   file basename
    """

    def native(self, field):
        return native(self.get(field), self["_file"])

    def turkish(self, field):
        return turkish(self.get(field), self["_file"])

    def plain(self, field):
        return unescape(self.get(field))


def load(name):
    """-> (text, [Entry]).  Aborts if any `word:` key was missed."""
    path = path_of(name)
    base = os.path.basename(path)
    text = io.open(path, encoding="utf-8").read()
    m = VAR_RE.search(text)
    if not m:
        raise SystemExit("no `window.<VAR> = [` marker in %s" % path)
    entries = []
    for a, b in _object_spans(text, m.end() - 1):
        e = Entry(_span=(a, b), _fields={}, _file=base)
        for fm in FIELD_RE.finditer(text, a, b):
            e[fm.group(1)] = fm.group(2)
            e["_fields"][fm.group(1)] = (fm.start(2), fm.end(2))
        if "word" in e:
            entries.append(e)
    expected = len(WORD_ONLY_RE.findall(text, m.end()))
    if len(entries) != expected:
        raise SystemExit(
            "ABORT %s: parsed %d entries but %d `word:` keys exist -- schema "
            "drift would silently drop entries." % (path, len(entries), expected))
    return text, entries


# --- editing ------------------------------------------------------------
class Editor(object):
    """Collects splices and applies them right-to-left in one pass."""

    def __init__(self, name):
        self.name = os.path.basename(path_of(name))
        self.path = path_of(name)
        self.text, self.entries = load(name)
        self._edits = []          # (start, end, replacement)
        self.changed_fields = 0
        self.deleted = 0

    def set_field(self, entry, field, plain_value):
        """Replace one field's value.  `plain_value` is UNESCAPED text."""
        span = entry["_fields"].get(field)
        if span is None:
            raise KeyError("%s has no field %r (word=%s)"
                           % (self.name, field, entry.get("word")))
        new = escape(plain_value)
        if entry.get(field) == new:
            return False
        self._edits.append((span[0], span[1], new))
        entry[field] = new
        self.changed_fields += 1
        return True

    def set_bilingual(self, entry, field, nat, tr):
        return self.set_field(entry, field, nat + sep_for(self.name) + tr)

    def insert_field(self, entry, field, plain_value, after):
        """Add a missing field directly after `after`, matching local style.

        The separator between fields is copied from the gap that already
        follows `after` in this very entry, so one-object-per-line files stay
        on one line and one-field-per-line files stay indented.
        """
        if field in entry:
            return False
        anchor = entry["_fields"][after]
        i = anchor[1] + 1                      # just past the closing quote
        j = i
        if j < len(self.text) and self.text[j] == ",":
            j += 1
        k = j
        while k < len(self.text) and self.text[k] in " \t\r\n":
            k += 1
        gap = self.text[j:k] or " "
        # Insert BEFORE the existing comma so we contribute our own separator
        # and the following field keeps the one it already had.
        self._edits.append((i, i, ',%s%s: "%s"' % (gap, field, escape(plain_value))))
        entry[field] = escape(plain_value)
        self.changed_fields += 1
        return True

    def delete(self, entry):
        """Remove the whole entry, its trailing comma and its own line(s)."""
        a, b = entry["_span"]
        while b < len(self.text) and self.text[b] in " \t":
            b += 1
        if b < len(self.text) and self.text[b] == ",":
            b += 1
        while b < len(self.text) and self.text[b] in " \t\r":
            b += 1
        if b < len(self.text) and self.text[b] == "\n":
            b += 1
        while a > 0 and self.text[a - 1] in " \t":
            a -= 1
        self._edits.append((a, b, ""))
        self.deleted += 1

    def save(self, dry_run=False):
        if not self._edits:
            return self.text
        out = self.text
        for a, b, rep in sorted(self._edits, key=lambda t: t[0], reverse=True):
            out = out[:a] + rep + out[b:]
        if not dry_run:
            with io.open(self.path, "w", encoding="utf-8", newline="") as f:
                f.write(out)
        return out


# --- bilingual values ---------------------------------------------------
def native(value, name):
    s = unescape(value)
    sep = sep_for(name)
    i = s.find(sep)
    return s[:i] if i != -1 else s


def turkish(value, name):
    s = unescape(value)
    sep = sep_for(name)
    i = s.find(sep)
    return s[i + len(sep):] if i != -1 else ""


# --- language helpers ---------------------------------------------------
# NOTE the `\s+` on every non-apostrophe article.  Without it these patterns
# eat the start of ordinary words -- "descendre" becomes "cendre", "lever"
# becomes "ver", "latin" becomes "tin" -- which silently merges unrelated
# headwords in any duplicate check built on top of bare().
ARTICLE_RE = {
    "de": re.compile(r"^(?:der|die|das)\s+", re.I),
    "fr": re.compile(r"^(?:(?:le|la|les|un|une|des|du)\s+|l['\u2019]\s*)", re.I),
    "es": re.compile(r"^(?:el|la|los|las|un|una)\s+", re.I),
    "it": re.compile(r"^(?:(?:il|lo|la|i|gli|le|un|uno|una)\s+|l['\u2019]\s*)", re.I),
    "pt": re.compile(r"^(?:o|a|os|as|um|uma)\s+", re.I),
}


def lang_of(name):
    n = os.path.basename(name)
    if "gode" in n or n in ("partikelverbde.js", "synantde.js"):
        return "de"
    if n.endswith("fr.js"):
        return "fr"
    if n.endswith("es.js"):
        return "es"
    if n.endswith("it.js"):
        return "it"
    if n.endswith("pt.js"):
        return "pt"
    return "en"


def bare(word, lang):
    """Headword without a leading article, casefolded.

    casefold() not lower() -- lower() leaves the German sz-ligature alone, so
    the sz- and ss-spellings compare unequal and duplicate checks silently miss.
    """
    w = unescape(word).strip()
    rx = ARTICLE_RE.get(lang)
    if rx:
        w = rx.sub("", w)
    return w.casefold()


def headword(word, lang):
    """Headword as a dictionary lists it: no leading article, original case.

    The French decks store `la table` and `le telephone`; no wiktionary page
    carries the article, so a lookup has to drop it while keeping the case a
    German noun depends on.
    """
    w = unescape(word).strip()
    rx = ARTICLE_RE.get(lang)
    return (rx.sub("", w).strip() if rx else w) or w


LEVELS = ["A1", "A2", "B1", "B2", "C1", "C2"]
