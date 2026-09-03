"""Remove low-quality entries from data/wordsa1.js (English A1).

The removal list below was produced by running
  scripts/_diag_cefrj_membership.py wordsa1.js A1
and then hand-reviewing every candidate it reported. Words are dropped only
for a concrete, statable reason:

  NAME      a personal / place / brand name, not vocabulary
            (henry, berlin, batman, bobby, ...)
  ADVANCED  real word, but nowhere near A1
            (artillery, magistrate, psychiatrist, ...)
  CRUDE     unsuitable for a beginner list aimed at all ages
            (goddamn, negro, junkie, ...)
  BROKEN    the entry is internally wrong - definition belongs to a different
            word, or the example does not use the headword
            (takin, haven, dough, drone, scotch, ...)
  INFLECT   an inflected form of a word this same file already teaches
            (beaten, bigger, dozens, closest, ...)

Words that CEFR-J happens not to list but which are perfectly good beginner
vocabulary (attic, bunny, doorbell, ordinals, ...) are deliberately kept.
"""
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATH = os.path.join(ROOT, "data", "wordsa1.js")

NAME = """
amazon bailey batman benjamin benny berlin billy bobby bonnie brazil brent
carter cassie chandler charlotte china clarence collins daphne dexter dolly
donna drake franklin freeman gemma gilbert graham griffin hamlet harper harry
heather henry hogan holly homer japan jasmine jersey jimmy kitty manhattan
marcel marge maria martin mason maxwell merlin miller molly murphy napoleon
nelson newton oxford paddy patty perry phoebe piper poppy rosemary sherlock
skipper superman tammy teddy terry timothy tucker violet wally willow easter
fisher archer
"""

ADVANCED = """
almighty artillery asteroid attorney bachelor bandit cartel cavalry cocaine
congressman convent coroner counselor countess courier dagger deacon duchess
empress federation fiance fiancee forensics fortress godfather graveyard
grenade heroin highness homicide informant interrogation lieutenant mafia
magistrate magnum mademoiselle maiden marijuana marina marquis medic mermaid
missus mistress morphine paladin paranoid pistol preacher prophet psychiatrist
puppet rabbi republican reverend samurai senate shipment shotgun shuttle sniper
sperm suspenseful swine undercover werewolf ethics operations circumstances
"""

CRUDE = """
butch chick douche goddamn gypsy junkie moron negro phony psycho squeal
stalker weirdo filth madman comrade
"""

BROKEN = """
takin haven dough drone stash scotch witch chopper needs cooper ferry pickle
bleep indistinctly hallelujah hooray howdy hunch twitter upside anyways
"""

INFLECT = """
affairs allies announcer authorities awfully beaten beats bigger blown brakes
built buster buzzer chosen closer closest colours compliments conditions costs
dearest details dozens driven drove drunken earlier effects falls feelings
flies folks fries funds greatest grown hands heard heavens higher hours humans
instructions larger letters links loads locker longer loser masses mates
minutes numbers older organs pains papers peanuts profits rates recorder
regrets relations rings roads sales scores seats services shoes shooter sights
sincerely shook spent spoke stole struck thrown understood eldest
"""

REASONS = [("NAME", NAME), ("ADVANCED", ADVANCED), ("CRUDE", CRUDE),
           ("BROKEN", BROKEN), ("INFLECT", INFLECT)]

DROP = {}
for label, blob in REASONS:
    for w in blob.split():
        DROP.setdefault(w, label)

ENTRY_RE = re.compile(r'\n  \{\s*\n?(?:[^{}]|\{[^{}]*\})*?\n?  \},', re.S)
WORD_RE = re.compile(r'word:\s*"((?:\\.|[^"\\])*)"')


def main():
    dry = "--apply" not in sys.argv
    text = io.open(PATH, encoding="utf-8").read()
    before = len(WORD_RE.findall(text))

    removed, counts = [], {}

    def repl(m):
        block = m.group(0)
        wm = WORD_RE.search(block)
        if not wm:
            return block
        w = wm.group(1).strip().lower()
        reason = DROP.get(w)
        if not reason:
            return block
        removed.append((w, reason))
        counts[reason] = counts.get(reason, 0) + 1
        return ""

    out = ENTRY_RE.sub(repl, text)
    after = len(WORD_RE.findall(out))

    print("entries %d -> %d   (removed %d)" % (before, after, before - after))
    for label, _ in REASONS:
        print("  %-9s %4d" % (label, counts.get(label, 0)))
    missed = sorted(set(DROP) - {w for w, _ in removed})
    if missed:
        print("\nlisted but not found in file (%d): %s" % (len(missed), ", ".join(missed)))

    if dry:
        print("\nDRY RUN - pass --apply to write")
        return
    if out.count("{") != out.count("}"):
        print("ABORT: brace imbalance after edit")
        return 1
    io.open(PATH, "w", encoding="utf-8", newline="\n").write(out)
    print("\nwritten")


if __name__ == "__main__":
    sys.exit(main() or 0)
