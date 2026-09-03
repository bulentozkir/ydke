#!/usr/bin/env python3
"""Prepend the PolyForm Noncommercial 1.0.0 licence banner to every .js file.

Re-runnable: a file that already carries the banner is left untouched, so this
can be run again after new scripts are added. Run from the repo root:

    python scripts/add_license_headers.py            # apply
    python scripts/add_license_headers.py --check    # report only, exit 1 if
                                                     # anything is missing

Deliberate details:

* Line endings are preserved PER FILE. The repo is a mix of CRLF and LF and
  rewriting them all one way would turn a 3-line licence change into an
  88-file whole-content diff that hides the real edit.
* Files are read and written as UTF-8 WITHOUT a BOM, which is what they
  already are -- the word lists are full of Turkish/German/French characters
  and a stray BOM would show up as a mojibake glyph at the top of a served
  script.
* data/*.js gets an EXTRA block. Those files bundle third-party definitions
  and example sentences (Wiktionary CC BY-SA 4.0, Tatoeba CC BY 2.0 FR,
  WordNet and others -- see NOTICE). CC BY-SA in particular forbids applying
  further restrictions to the licensed material, so the block there has to
  claim the compilation (selection, arrangement, levelling, translations)
  while explicitly NOT asserting the noncommercial term over the sourced
  items. Do not "simplify" that block away.
* If DATA_NOTE is ever reworded, add the old text to LEGACY_DATA_NOTES and a
  normal run will swap it out in place. Without that the run is a no-op,
  because files that already contain MARKER are skipped.
* A shebang, if one is ever added, has to stay on line 1, so the banner is
  inserted after it.
"""

import argparse
import pathlib
import sys

MARKER = "PolyForm Noncommercial"

SKIP_DIRS = {"node_modules", ".venv", ".git", "dist", "build"}

BANNER = """\
/*! Top Words (udsp) — Copyright 2026 Bulent Ozkir, Ahmet Arda Ozkir, Halit Eren Ozkir
 * Licensed under the PolyForm Noncommercial License 1.0.0 — NONCOMMERCIAL USE ONLY.
 * <https://polyformproject.org/licenses/noncommercial/1.0.0>
 *
 * Any commercial use requires prior written permission from the copyright
 * holders. Written permission from any ONE of bulentozkir@hotmail.com,
 * bulentozkir@gmail.com, ahmetardaozkir@gmail.com or haliterenozkir@gmail.com
 * is sufficient and binding on all of them.
 *
 * Required Notice: Copyright 2026 Bulent Ozkir, Ahmet Arda Ozkir, Halit Eren
 * Ozkir (https://udsp.vercel.app)
 * Full terms: see LICENSE and NOTICE in this repository.
 */"""

DATA_NOTE = """\
/*! Compilation notice — read together with the licence above.
 * The SELECTION, ARRANGEMENT, CEFR levelling, editing and Turkish
 * translations in this file are the copyright holders' own work and ARE
 * covered by the PolyForm Noncommercial licence above. Copying this list, or
 * any substantial part of it, into a commercial product or service requires
 * prior written permission from one of the addresses above.
 *
 * Some individual definitions and example sentences come from open community
 * projects (Wiktionary CC BY-SA 4.0, Tatoeba CC BY 2.0 FR, WordNet and
 * others — see NOTICE). Those items keep THEIR OWN licence and the
 * noncommercial term is not asserted over them.
 */"""

# Earlier wordings of DATA_NOTE. A normal run replaces any of these in place,
# which is the only way to update files that already contain MARKER.
LEGACY_DATA_NOTES = [
    """\
/*! Third-party content: this file bundles definitions and example sentences
 * from open community projects (Wiktionary CC BY-SA 4.0, Tatoeba CC BY 2.0 FR,
 * WordNet and others — see NOTICE). That content keeps ITS OWN licence; the
 * noncommercial term above applies to this project's code and to the
 * compilation, not to the sourced content.
 */""",
]


def read_raw(path: pathlib.Path) -> str:
    """Read WITHOUT universal-newline translation.

    pathlib's read_text() turns every \r\n into \n before you ever see it, so
    a naive round-trip silently normalises the whole file and buries a 14-line
    licence edit inside a 60,000-line whitespace diff.
    """
    with open(path, encoding="utf-8", newline="") as fh:
        return fh.read()


def eol_of(text: str) -> str:
    """The file's own newline, so we don't rewrite every line of a 5 MB list."""
    i = text.find("\n")
    if i > 0 and text[i - 1] == "\r":
        return "\r\n"
    return "\n"


def banner_for(path: pathlib.Path, root: pathlib.Path) -> str:
    parts = path.relative_to(root).parts
    if parts and parts[0] == "data":
        return BANNER + "\n" + DATA_NOTE
    return BANNER


def refreshed_note(text: str, eol: str) -> str | None:
    """Swap a superseded DATA_NOTE for the current one, or None if up to date."""
    for old in LEGACY_DATA_NOTES:
        needle = old.replace("\n", eol)
        if needle in text:
            return text.replace(needle, DATA_NOTE.replace("\n", eol))
    return None


def write_raw(path: pathlib.Path, text: str) -> None:
    """utf-8 (not utf-8-sig) keeps the file BOM-free; newline='' keeps our EOLs."""
    with open(path, "w", encoding="utf-8", newline="") as fh:
        fh.write(text)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="report only, change nothing")
    args = ap.parse_args()

    root = pathlib.Path(__file__).resolve().parent.parent
    files = sorted(
        p
        for p in root.rglob("*.js")
        if not SKIP_DIRS.intersection(p.relative_to(root).parts)
    )

    missing, changed, stale = [], [], []
    for path in files:
        text = read_raw(path)
        eol = eol_of(text)
        name = path.relative_to(root).as_posix()

        if MARKER in text[:2000]:
            updated = refreshed_note(text, eol)
            if updated is not None:
                stale.append(name)
                if not args.check:
                    write_raw(path, updated)
            continue

        missing.append(name)
        if args.check:
            continue

        banner = banner_for(path, root).replace("\n", eol)

        # A shebang must stay on line 1.
        if text.startswith("#!"):
            cut = text.find("\n")
            cut = len(text) if cut == -1 else cut + 1
            body = text[:cut] + banner + eol + eol + text[cut:]
        else:
            body = banner + eol + eol + text

        write_raw(path, body)
        changed.append(name)

    if args.check:
        for name in missing:
            print("missing banner: " + name)
        for name in stale:
            print("outdated data notice: " + name)
        print(f"{len(files) - len(missing)}/{len(files)} .js files carry the banner")
        return 1 if missing or stale else 0

    for name in changed:
        print("banner added: " + name)
    for name in stale:
        print("data notice refreshed: " + name)
    print(
        f"{len(changed)} added, {len(stale)} refreshed, "
        f"{len(files) - len(changed)} already had the banner, {len(files)} total"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
