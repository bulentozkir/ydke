#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Minimal Wiktionary wikitext reader for definition lines.

The REST definition API only exposes the ENGLISH Wiktionary's glosses.  For a
German gloss of a German word (and a French gloss of a French word) the
native-language Wiktionary has to be read directly -- de.wiktionary keeps its
meanings in a structured `{{Bedeutungen}}` block and fr.wiktionary in `#`
lines under the language section, both of which parse reliably enough for a
one-line gloss.
"""
import json
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

sys.path.insert(0, __file__.rsplit("\\", 1)[0] if "\\" in __file__ else ".")
import _udsp_translate as T

LIMITER = T.RateLimiter(0.2)          # 5 req/s
HEADERS = {"User-Agent": "udsp-vocab-content/1.0 (offline study app; contact via repo)"}

REF_RE = re.compile(r"<ref[^>]*>.*?</ref>|<ref[^>]*/>", re.S)
TAG_RE = re.compile(r"<[^>]+>")
TMPL_RE = re.compile(r"\{\{([^{}]*)\}\}")
LINK_RE = re.compile(r"\[\[(?:[^\]|]*\|)?([^\]|]*)\]\]")
ITAL_RE = re.compile(r"'{2,}")
BRACKET_NUM_RE = re.compile(r"^\s*:?\s*\[[\d,\s\u2013-]*\]\s*")
WS_RE = re.compile(r"\s+")
# Templates worth keeping as a usage label, rendered as plain text.
KEEP_TMPL = {"ugs.", "fig.", "übertragen", "veraltet", "regional", "salopp"}


def _clean(s):
    s = REF_RE.sub("", s or "")
    s = LINK_RE.sub(r"\1", s)

    def tmpl(m):
        parts = m.group(1).split("|")
        head = parts[0].strip()
        return head if head in KEEP_TMPL else ""

    prev = None
    while prev != s:
        prev = s
        s = TMPL_RE.sub(tmpl, s)
    s = TAG_RE.sub("", s)
    s = ITAL_RE.sub("", s)
    s = BRACKET_NUM_RE.sub("", s)
    s = s.replace("[", "").replace("]", "")
    return WS_RE.sub(" ", s).strip(" ;:,")


def fetch_wikitext(word, site):
    url = ("https://%s.wiktionary.org/w/api.php?action=parse&prop=wikitext"
           "&format=json&formatversion=2&page=%s"
           % (site, urllib.parse.quote(word.replace(" ", "_"), safe="")))
    for attempt in range(3):
        LIMITER.wait()
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=20) as resp:
                data = json.loads(resp.read().decode("utf-8"))
        except urllib.error.HTTPError as exc:
            if exc.code in (400, 404):
                return None
            continue
        except Exception:                       # noqa: BLE001
            continue
        if "error" in data:
            return ""                            # page really does not exist
        return data.get("parse", {}).get("wikitext", "")
    return None                                  # transient


BATCH = 50           # the API's cap for anonymous callers
BATCH_LIMITER = T.RateLimiter(1.5)
MAX_INTERVAL = 12.0


def _slow_down():
    """A 429 means the pace itself is wrong, not just this one request."""
    BATCH_LIMITER.min_interval = min(MAX_INTERVAL,
                                     BATCH_LIMITER.min_interval * 1.6)


def _speed_up():
    # Recover briskly: one bad minute should not throttle the next hour.
    BATCH_LIMITER.min_interval = max(1.5, BATCH_LIMITER.min_interval * 0.9)


def fetch_many(words, site, retries=6):
    """{word: wikitext or None} for up to BATCH pages in ONE request.

    Fetching pages one at a time gets throttled to a standstill -- anonymous
    callers start receiving HTTP 429 within a few hundred requests, and 24k
    headwords is far past that.  `action=query&prop=revisions` accepts 50
    titles per call, so the same corpus costs ~500 requests instead of 24,000
    and stays comfortably inside the limit.  None means "ask again later";
    "" means the page genuinely does not exist.
    """
    out = {}
    todo = [w for w in words if w and "|" not in w]
    for i in range(0, len(todo), BATCH):
        chunk = todo[i:i + BATCH]
        base = {
            "action": "query", "prop": "revisions", "rvprop": "content",
            "rvslots": "main", "format": "json", "formatversion": "2",
            "redirects": "1", "titles": "|".join(chunk),
        }
        url = "https://%s.wiktionary.org/w/api.php" % site
        alias, content = {}, {}
        cont, failed = {}, False
        # The API caps how much page content one response may carry, so a
        # 50-title request comes back partial with a `continue` token.  Without
        # following it the missing pages look like non-existent ones -- that is
        # what silently emptied 62% of the German glosses on the first run.
        for _round in range(25):
            body = urllib.parse.urlencode(dict(base, **cont)).encode("utf-8")
            data = None
            for attempt in range(retries):
                BATCH_LIMITER.wait()
                try:
                    req = urllib.request.Request(url, data=body, headers=HEADERS)
                    with urllib.request.urlopen(req, timeout=60) as resp:
                        data = json.loads(resp.read().decode("utf-8"))
                    _speed_up()
                    break
                except urllib.error.HTTPError as exc:
                    if exc.code == 429:
                        _slow_down()
                        wait = exc.headers.get("Retry-After")
                        time.sleep(float(wait) if wait and wait.isdigit()
                                   else min(60, 2 ** attempt * 5))
                        continue
                    if exc.code in (400, 404):
                        break
                    time.sleep(min(30, 2 ** attempt))
                except Exception:                # noqa: BLE001
                    time.sleep(min(30, 2 ** attempt))
            if not data or "query" not in data:
                failed = True
                break
            q = data["query"]
            # follow title normalisation and redirects back to what we asked for
            for step in ("normalized", "redirects"):
                for m in q.get(step, []) or []:
                    alias[m["to"]] = alias.get(m["from"], m["from"])
            for page in q.get("pages", []) or []:
                title = page.get("title", "")
                if page.get("missing"):
                    content.setdefault(title, "")
                    continue
                revs = page.get("revisions") or []
                if not revs:
                    continue                   # content still to come
                slot = (revs[0].get("slots") or {}).get("main") or {}
                content[title] = slot.get("content") or ""
            if "continue" in data:
                cont = {k: v for k, v in data["continue"].items()}
                continue
            break
        if failed:
            for w in chunk:
                out[w] = None                  # transient -> retry next run
            continue
        resolved = {}
        for title, text in content.items():
            resolved[alias.get(title, title)] = text
            resolved.setdefault(title, text)
        for w in chunk:
            out[w] = resolved.get(w, "")
    return out


def german_meanings(wikitext):
    """Gloss lines under {{Bedeutungen}} in the German section."""
    if not wikitext:
        return []
    i = wikitext.find("{{Bedeutungen}}")
    if i == -1:
        return []
    out = []
    for line in wikitext[i:].splitlines()[1:]:
        if line.startswith("{{") or line.startswith("=="):
            break
        if not line.startswith(":"):
            if out:
                break
            continue
        s = _clean(line)
        if s:
            out.append(s)
    return out


def french_meanings(wikitext):
    """`#` gloss lines inside the {{langue|fr}} section."""
    if not wikitext:
        return []
    i = wikitext.find("{{langue|fr}}")
    if i == -1:
        return []
    tail = wikitext[i:]
    j = tail.find("\n== ")
    if j != -1:
        tail = tail[:j]
    out = []
    for line in tail.splitlines():
        if line.startswith("#") and not line.startswith(("#*", "#:", "##")):
            s = _clean(line[1:])
            if s:
                out.append(s)
    return out


def _split_template(body):
    """Split a template body on top-level `|`."""
    parts, depth, cur = [], 0, []
    i = 0
    while i < len(body):
        if body.startswith(("{{", "[["), i):
            depth += 1
            cur.append(body[i:i + 2])
            i += 2
            continue
        if body.startswith(("}}", "]]"), i):
            depth -= 1
            cur.append(body[i:i + 2])
            i += 2
            continue
        if body[i] == "|" and depth == 0:
            parts.append("".join(cur))
            cur = []
        else:
            cur.append(body[i])
        i += 1
    parts.append("".join(cur))
    return parts


def _expand_example_templates(s):
    """Unwrap the templates that CARRY an example sentence.

    _clean() deletes every unrecognised template, so without this step the
    sentence itself is thrown away: en.wiktionary writes usage examples as
    {{ux|en|...}} and quotations as {{quote-book|...|passage=...}}, while
    fr.wiktionary uses {{exemple|...}}.
    """
    out, i = [], 0
    while i < len(s):
        j = s.find("{{", i)
        if j == -1:
            out.append(s[i:])
            break
        out.append(s[i:j])
        depth, k = 0, j
        while k < len(s):
            if s.startswith("{{", k):
                depth += 1
                k += 2
            elif s.startswith("}}", k):
                depth -= 1
                k += 2
                if depth == 0:
                    break
            else:
                k += 1
        parts = _split_template(s[j + 2:k - 2])
        name = parts[0].strip().lower()
        positional, named = [], {}
        for p in parts[1:]:
            key, eq, val = p.partition("=")
            if eq and " " not in key.strip() and len(key.strip()) < 24:
                named[key.strip().lower()] = val
            else:
                positional.append(p)
        if name in ("ux", "uxi", "usex", "ux-lite", "ux-inline", "eg"):
            text = positional[1] if len(positional) > 1 else ""
        elif name in ("exemple", "example"):
            text = named.get("1") or (positional[0] if positional else "")
        elif name.startswith("quote") or name.startswith("cite"):
            text = named.get("passage", "") or named.get("text", "")
        else:
            text = ""
        out.append(text)
        i = k
    return "".join(out)


def _language_section(wikitext, language):
    """The slice of an en.wiktionary page that belongs to one language."""
    marker = "==%s==" % language
    i = (wikitext or "").find(marker)
    if i == -1:
        return ""
    tail = wikitext[i + len(marker):]
    nxt = re.search(r"^==[^=]", tail, re.M)
    return tail[:nxt.start()] if nxt else tail


def english_glosses(wikitext, language="English"):
    """`#` gloss lines from one language section of an en.wiktionary page.

    The French word files state their meanings in English, so a French
    headword's replacement definition has to come from the French section of
    the ENGLISH Wiktionary rather than from fr.wiktionary.
    """
    out = []
    for line in _language_section(wikitext, language).splitlines():
        if line.startswith("#") and not line.startswith(("#:", "#*", "##")):
            s = _clean(_expand_example_templates(line[1:]))
            if s:
                out.append(s)
    return out


def english_examples(wikitext, language="English"):
    """`#:` usage lines, then quotation passages, in one language section.

    The REST definition API exposes almost none of these -- it returned no
    examples at all for `laconic` or `perspicacious`, both of which do carry
    usage sentences in their wikitext.
    """
    primary, quotes = [], []
    for line in _language_section(wikitext, language).splitlines():
        if not line.startswith("#"):
            continue
        body = line.lstrip("#")
        if body.startswith(":"):
            bucket = primary
        elif body.startswith("*"):
            bucket = quotes
        else:
            continue
        s = _clean(_expand_example_templates(body.lstrip(":* ")))
        if s:
            bucket.append(s)
    return primary + quotes


def german_examples(wikitext):
    """Sentences under {{Beispiele}} in the German section.

    de.wiktionary keeps hand-written usage sentences in their own block, which
    is far better coverage for German headwords than the English Wiktionary's
    citations for the same word.
    """
    if not wikitext:
        return []
    i = wikitext.find("{{Beispiele}}")
    if i == -1:
        return []
    out = []
    for line in wikitext[i:].splitlines()[1:]:
        if line.startswith("{{") or line.startswith("=="):
            break
        if not line.startswith(":"):
            if out:
                break
            continue
        s = _clean(_expand_example_templates(line))
        if s:
            out.append(s)
    return out


def french_examples(wikitext):
    """`#*` sentences inside the {{langue|fr}} section."""
    if not wikitext:
        return []
    i = wikitext.find("{{langue|fr}}")
    if i == -1:
        return []
    tail = wikitext[i:]
    j = tail.find("\n== ")
    if j != -1:
        tail = tail[:j]
    out = []
    for line in tail.splitlines():
        if line.startswith("#*") and not line.startswith("#*:"):
            s = _clean(_expand_example_templates(line[2:]))
            if s:
                out.append(s)
    return out
