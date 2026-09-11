# Native localization console tests

Dependency-free .NET 10 console harness. Links the actual ExperienceStrings,
Localizer, Models and GameCatalog sources; only AppearancePalette is stubbed.
No WinUI, storage, user profile, audio, cloud, network service or other test
project is loaded. Only repository source files are read.

Run this project's Release console executable with an optional repository-root
argument. Without an argument it searches parent directories from the working
directory and executable location. Missing source fails rather than skipping.

## Coverage

- Regex scans every current native MainPage partial for T/U calls, including
  calls inside interpolated strings. Balanced arguments handle punctuation in
  fallback text. Both branches of literal-key conditionals are covered.
- Unknown dynamic expressions fail closed. Score-mode identifiers are parsed
  from ScoreMode and expanded to Games.Mode resources, even before UI adoption.
- The real game instruction switch is compared to the linked instruction helper
  for every catalog game and language. Unexpected default-switch games fail.
- All seven languages need explicit resources, not English/Turkish fallbacks.
  Every expected and declared resource is checked through both lookup routes;
  blank/raw-key results and long copied English translations fail.
- Game English fallback text is compared with the translations' English column.
  Reviewed per-language semantic fragments additionally protect durations,
  score scope, practice separation, letter feedback and actual game mechanics.
- GameSession's real timing defaults and source-level mechanic contracts are
  checked without running the UI or duplicating the existing game test suite.
- Parser/missing-key negative controls ensure new keys and unsupported dynamic
  expressions cannot silently produce a green result.

Counts are printed per language and in the final RESULT line. Any failure exits
with code 1. AUDIT_WARNING reports a known caller-side integration gap separately
from missing translations; a resource cannot translate UI text that bypasses T/U.

This is intentionally a source-aware localization regression suite, not a C#
parser or a proof of linguistic quality. A new dynamic call form or a deliberate
mechanic/copy change needs corresponding test review. No UI automation is run.