#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Remove two entries whose example sentences ship hate speech to learners.

wordsc1.js "kaffir"    -- a racial slur, filed under category "Food" with the
                          gloss for kaffir corn, and illustrated with a quoted
                          sentence comparing Black people to baboons, fully
                          translated into Turkish for study.
wordsc1.js "mongoloid" -- a disability slur, illustrated with "That mongoloid
                          talks too much.", also translated.

Neither headword is defensible study vocabulary and both definitions are wrong
for the word as spelled, so the entries go rather than just their examples.
"""
import _udsp_data as U

TARGETS = {"wordsc1.js": ["kaffir", "mongoloid"]}

for name, words in TARGETS.items():
    ed = U.Editor(name)
    gone = []
    for e in list(ed.entries):
        if U.unescape(e.get("word", "")).strip() in words:
            ed.delete(e)
            gone.append(U.unescape(e.get("word", "")).strip())
    ed.save()
    print("%s: removed %d entries %s" % (name, len(gone), gone))
