# -*- coding: utf-8 -*-
"""Download and cache a gazetteer of personal and place names.

The single biggest junk class in the bulk-harvested vocabulary is proper
nouns presented as words to learn (henry, berlin, bobby, bolivia). Detecting
them by capitalisation fails badly (see repo memory: it flags every month and
weekday), so instead we check membership in real name lists.

Two tiers are returned, because they need different handling:

  people  first names + surnames. Safe to act on directly, because a surname
          that is also an everyday word is rare.
  places  cities + countries. Must be used with care: the world-cities file
          contains Bath, Reading, Mobile, Orange, Nice and Split, which are
          ordinary English words. Callers should only treat a place name as
          junk when it is NOT in a general dictionary.

Sources are plain raw-text files on GitHub, no API key. Each is optional: if
one is unreachable the rest still work.
"""
import io
import os
import sys
import tempfile
import urllib.request

CACHE_DIR = tempfile.gettempdir()
CACHES = {"people": os.path.join(CACHE_DIR, "udsp_names_people.txt"),
          "places": os.path.join(CACHE_DIR, "udsp_names_places.txt")}

SOURCES = {
    "people": [
        ("first names", "https://raw.githubusercontent.com/dominictarr/"
                        "random-name/master/first-names.txt"),
        ("surnames", "https://raw.githubusercontent.com/dominictarr/"
                     "random-name/master/names.txt"),
    ],
    "places": [
        ("world cities", "https://raw.githubusercontent.com/datasets/"
                         "world-cities/master/data/world-cities.csv"),
        ("countries", "https://raw.githubusercontent.com/datasets/"
                      "country-list/master/data.csv"),
    ],
}


def fetch(url):
    req = urllib.request.Request(url, headers={"User-Agent": "udsp-quality"})
    with urllib.request.urlopen(req, timeout=60) as fh:
        return fh.read().decode("utf-8", "replace")


def build(tier, verbose):
    names = set()
    for label, url in SOURCES[tier]:
        try:
            text = fetch(url)
        except Exception as exc:                                  # noqa: BLE001
            if verbose:
                print("  SKIP %-13s %s" % (label, exc))
            continue
        before = len(names)
        for line in text.splitlines():
            for cell in line.split(","):
                cell = cell.strip().strip('"').lower()
                # Multi-word names ("new york") cannot collide with a single
                # headword, so keep only single alphabetic tokens.
                if cell.isalpha() and 3 <= len(cell) <= 20:
                    names.add(cell)
        if verbose:
            print("  %-13s +%d" % (label, len(names) - before))
    return names


def load(verbose=True):
    out = {}
    for tier, cache in CACHES.items():
        if os.path.exists(cache):
            with io.open(cache, encoding="utf-8") as fh:
                out[tier] = {ln.strip() for ln in fh if ln.strip()}
        else:
            if verbose:
                print("building %s gazetteer..." % tier)
            out[tier] = build(tier, verbose)
            if out[tier]:
                with io.open(cache, "w", encoding="utf-8", newline="\n") as fh:
                    fh.write("\n".join(sorted(out[tier])))
    if verbose:
        print("gazetteer: %d people, %d places"
              % (len(out["people"]), len(out["places"])))
    return out["people"], out["places"]


if __name__ == "__main__":
    people, places = load()
    probe = ["henry", "berlin", "bobby", "bolivia", "judas", "martin", "harry",
             "january", "friday", "ministry", "admiral", "python", "become",
             "split", "bath", "reading", "mobile", "orange", "nice"]
    print("\nprobe            people places")
    for w in probe:
        print("  %-10s %-6s %s" % (w, w in people, w in places))
    sys.exit(0)
