"""Find elements that JS/HTML hides via the `hidden` attribute but whose CSS
sets a `display` value -- which silently outranks the UA stylesheet's
`[hidden] { display: none }` and leaves the element on screen.

This project already carries five hand-written `.foo[hidden] { display: none }`
workarounds (.info-popover, .info-popover-backdrop, .more-sheet, .intro-langs,
.intro-next), which is the tell that the trap keeps getting re-hit. Run this
after adding any new hidden-toggled element.

    python scripts/_diag_hidden_css.py
"""

import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def read(path):
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def hidden_targets():
    """ids and classes that are hidden via the `hidden` attribute somewhere."""
    ids, classes = set(), set()
    for name in sorted(os.listdir(ROOT)):
        path = os.path.join(ROOT, name)
        if not os.path.isfile(path):
            continue
        if name.endswith(".html"):
            text = read(path)
            # <div id="x" class="a b" hidden>  (attribute order varies)
            for tag in re.findall(r"<[a-zA-Z][^>]*\bhidden\b[^>]*>", text):
                m = re.search(r'\bid="([^"]+)"', tag)
                if m:
                    ids.add(m.group(1))
                m = re.search(r'\bclass="([^"]+)"', tag)
                if m:
                    classes.update(m.group(1).split())
        elif name.endswith(".js"):
            text = read(path)
            for m in re.finditer(r'setHidden\(\s*"([^"]+)"', text):
                ids.add(m.group(1))
            for m in re.finditer(r'\$\(\s*"([^"]+)"\s*\)\.hidden\s*=', text):
                ids.add(m.group(1))
    return ids, classes


CSS = re.sub(r"/\*.*?\*/", "", read(os.path.join(ROOT, "styles.css")), flags=re.S)


def rules():
    """Yield (selector, body, media_context) for every rule in styles.css."""
    out = []
    depth = 0
    media = []
    i = 0
    buf = ""
    while i < len(CSS):
        c = CSS[i]
        if c == "{":
            sel = buf.strip()
            buf = ""
            if sel.startswith("@"):
                media.append((depth, sel))
                depth += 1
            else:
                # rule body: read to the matching close brace
                j = i + 1
                lvl = 1
                while j < len(CSS) and lvl:
                    if CSS[j] == "{":
                        lvl += 1
                    elif CSS[j] == "}":
                        lvl -= 1
                    j += 1
                out.append((sel, CSS[i + 1 : j - 1], " ".join(m[1] for m in media)))
                i = j
                continue
        elif c == "}":
            depth -= 1
            while media and media[-1][0] >= depth:
                media.pop()
            buf = ""
        else:
            buf += c
        i += 1
    return out


def main():
    ids, classes = hidden_targets()
    all_rules = rules()

    # Every "<target>[hidden]" guard that actually sets display:none.
    guarded = set()
    for sel, body, _media in all_rules:
        if "[hidden]" not in sel or not re.search(r"display\s*:\s*none", body):
            continue
        for part in sel.split(","):
            m = re.search(r"([#.][\w-]+)\[hidden\]", part.strip())
            if m:
                guarded.add(m.group(1))

    problems = []
    scoped = []
    for sel, body, media in all_rules:
        if "[hidden]" in sel:
            continue
        m = re.search(r"(?<![\w-])display\s*:\s*([\w-]+)", body)
        if not m or m.group(1) == "none":
            continue
        for part in sel.split(","):
            part = part.strip()
            if not part:
                continue
            # Only the rule's own subject matters -- the last compound selector.
            pieces = re.split(r"[\s>+~]+", part)
            last = pieces[-1]
            for tok in re.findall(r"[#.][\w-]+", last):
                name = tok[1:]
                is_target = (tok[0] == "#" and name in ids) or (
                    tok[0] == "." and name in classes
                )
                if not is_target or tok in guarded:
                    continue
                # A descendant-qualified rule (".a .b") only applies inside that
                # ancestor, which this scan cannot prove the hidden element is
                # in -- report separately rather than as a definite defect.
                (scoped if len(pieces) > 1 else problems).append(
                    (tok, m.group(1), part, media)
                )

    def show(rows):
        seen = set()
        for tok, disp, sel, media in rows:
            key = (tok, media)
            if key in seen:
                continue
            seen.add(key)
            where = (" inside " + media) if media else ""
            print("%-24s display:%-14s %s%s" % (tok, disp, sel, where))
        return seen

    hits = show(problems)
    if scoped:
        print("")
        print("scoped rules -- only a defect if the element sits inside that ancestor:")
        show(scoped)

    print("")
    print("%d unguarded hidden-toggled element(s)" % len(hits))
    return 1 if hits else 0


if __name__ == "__main__":
    sys.exit(main())
