# Native verification and current improvement scope

This document describes the current WinUI application, not the archived web
application. Study content is bundled locally, and learning is saved on this
device by default. The product now runs local-only: no Google sign-in, browser
handoff, online account state, or cloud backup/sync path is present. Backup and
restore are explicit local file actions. A successful build is not an
accessibility certification.

## Current study flows

- The app starts in true full screen. The fixed toolbar switches **Windowed /
  Full screen**; **F11** toggles and **Esc** exits full screen. Windowed mode
  keeps a DPI-aware minimum size where the display allows it.
- Home prioritizes Cards, Quiz and Games, with daily progress, a word-summary
  popup and saved sessions.
- Cards reveal meanings before the **Help me / Hard / I knew it / Easy!**
  ratings (the underlying Again/Hard/Good/Easy scheduling is unchanged). Ratings
  save locally and advance to the next unanswered card. **Undo my choice**
  reverses the last rating before another learning action or leaving Cards.
  Favorite/known tools and instructions open in popups.
- Quiz records an answer once and shows the right word plus the selected word
  if different. **Continue** waits for the saved answer; **Look at the answer**
  opens examples/comparison. Completion offers missed-word practice.
- Word Library provides search, category/favorite/known/due filters, result
  counts and **Previous word / Next word** paging through one expanded word at
  a time. **Clear filters** resets the search; matching results can start a quiz.
- Game catalogs show up to four games per page. The game toolbar and HUD stay
  compact, with the visual/board beside the question. Reading uses short text
  pages; CodyCross shows only the active clue. **How to play** pauses the clock;
  answer feedback waits for **Next**. Leaving an active game requires
  confirmation and ends that game; saved learning remains.
- Statistics has **Overview**, **Answers & games**, and **Cards & history**
  tabs, paged game scores and optional history details. Each different word
  contributes once per day to the shared goal. Self-ratings, known marks and
  objective quiz/game accuracy are different measures.
- Settings groups **My practice**, **Colors & text**, and **For grown-ups**.
  Appearance changes are previews until **Save**. **Reset preview** does not
  save. Language and level remain in the persistent top toolbar.
- **Settings > For grown-ups** contains local **Make a copy / Open a copy…**.
  Opening a copy replaces local settings/progress only after checking the
  backup, asking for confirmation and saving a safety copy.

These describe implemented paths, not a claim that every combination of data,
language, game, assistive technology and screen size has passed verification.

## Current multilingual Help

The native [Help renderer](src/YDKE.Windows/MainPage.Shell.cs) uses the explicit
[translation table](src/YDKE.Windows/ExperienceStrings.cs) for all seven UI
languages: Turkish, English, German, French, Spanish, Portuguese and Dutch.
Italian is a study language, not an additional UI language.

Help has eight topics: Screen & keys, Cards, Quiz, My words, Game instructions,
Settings, Statistics, and Backups & privacy. A topic opens readable, short pages
in a native dialog with Previous/Next, an explicit Close button and, where
applicable, an Open this screen action. Instructions are no longer available
only on hover, and the dialog does not light-dismiss when focus changes.
Topic paging preserves the no-scroll learning layout.

Automated UI test scripts were removed from this repository. Use the manual
accessibility/usability checklist below when validating UI behavior.

## Non-UI pipeline

Prerequisites: Windows, PowerShell 7, Node.js 22+, and the .NET SDK selected by
[global.json](../global.json) (10.0.400, latest patch allowed). The native build
also requires its Windows SDK / WinUI build dependencies. CI installs .NET and
Node on `windows-latest`; restore/setup may download SDKs and NuGet packages.
Tests themselves need no network services, credentials or real user profile.

Run from the repository root:

```powershell
pwsh -NoProfile -File ./windows/verification/Invoke-NativeValidation.ps1
```

[Invoke-NativeValidation.ps1](verification/Invoke-NativeValidation.ps1) runs every
step, retaining later diagnostics even after an earlier failure:

1. Data-validator self-test.
2. Read-only root data audit.
3. [Core harness](tests/YDKE.Core.Tests/YDKE.Core.Tests.csproj), Release console run.
4. [Games harness](tests/YDKE.Games.Tests/YDKE.Games.Tests.csproj), Release console run
   with the absolute root dataset directory as its first application argument.
5. [Localization harness](tests/YDKE.Localization.Tests/YDKE.Localization.Tests.csproj),
   Release console run with the absolute repository root as its first argument.
6. [windows/build.ps1](build.ps1), Release, **without** `-Run`.

Individual test commands (these are executable console harnesses, not `dotnet test` projects):

```powershell
dotnet run --project windows/tests/YDKE.Core.Tests/YDKE.Core.Tests.csproj -c Release
dotnet run --project windows/tests/YDKE.Games.Tests/YDKE.Games.Tests.csproj -c Release -- "$PWD/data"
dotnet run --project windows/tests/YDKE.Localization.Tests/YDKE.Localization.Tests.csproj -c Release -- "$PWD"
pwsh -NoProfile -File windows/verification/Test-DataQuality.ps1 -SelfTest
pwsh -NoProfile -File windows/verification/Test-DataQuality.ps1
```

The runner returns **0** only when every step succeeds, otherwise **1**. Missing
projects fail rather than passing as skips. `-OutputDirectory` overrides the
default ignored build-output location beneath windows/build. It contains
per-step logs, a data-quality report and an aggregate JSON summary with exit
codes, durations and `uiExecuted: false`. Console output is one JSON object per
step. Invocation/prerequisite errors can also terminate PowerShell nonzero.

[Native validation CI](../.github/workflows/native-validation.yml) runs on
relevant pushes, pull requests and manual dispatch. Reports are uploaded even
on failure. It does not launch the application or require an interactive
desktop. It builds the native app, not installers or signed release packages.

## Dataset validation semantics

[Test-DataQuality.ps1](verification/Test-DataQuality.ps1) reads the authoritative
root [data/manifest.json](../data/manifest.json) and all adjacent JavaScript
datasets. It does not use archived web content or regenerate a manifest.

- Checks actual file inventory, byte counts and SHA-256 against the manifest.
- Runs `node --check` on each dataset; invalid syntax fails.
- Parses only literal `window.NAME = [...]` assignments, comments, nested
  arrays/objects and string concatenation. No `eval`, `vm`, imports, callbacks,
  network calls or dataset JavaScript execution. Unsupported syntax fails
  explicitly rather than silently skipping validation.
- Detects duplicate object properties (before overwrite), manifest entries,
  dataset global names and exact record keys within each dataset. Vocabulary
  keys use level plus word; passage keys use level/grade plus title. This is not
  fuzzy linguistic deduplication or a ban on legitimate cross-level homographs.
- Requires word/level/category/definition/example and, for nonsynonym word
  datasets, part of speech. CEFR word-file levels must match their filename.
  Reading records require title, text, level/grade and valid questions/options
  with an in-range answer index. Semantic rows need at least one relationship.
- Flags known placeholder phrases as **warnings**, not proof of inaccurate
  translations. Empty required fields, duplicates and integrity drift are
  **errors**. Valid syntax and complete fields do not establish teaching quality,
  correct meanings, CEFR suitability or game eligibility.

Exit codes: **0** no errors (warnings may exist), **1** audit findings, **2**
prerequisite/runner failure. JSON is printed to stdout; `-ReportPath` also writes
it to an explicit file in an existing directory. `-DataDirectory` selects a
separate local fixture directory; `-SelfTest` exercises positive and negative
parser/schema cases without reading the real datasets.

**Do not automatically replace hashes or data to make CI green.** Inspect each
change, correct structural problems through the dataset owner, then deliberately
review and update the manifest to match approved content. A hash mismatch can
mean the manifest is stale after legitimate editing, not necessarily corruption.
Do not run old data-sync/import scripts as an automatic repair.

## Manual native accessibility / usability checklist

Record OS/build, display scale, window dimensions, UI/study language, dataset,
assistive technology and each observed result. Use a disposable profile.

- **Narrator:** navigate all pages using keyboard only; names, roles, selected
  states and disabled actions are meaningful. Reveal/answer feedback and changing
  counts are announced once, without exposing hidden answers. Verify focus after
  render, Continue, undo and dialog dismissal. Dialog default is Stay/Cancel;
  focus stays in the dialog until it closes and returns sensibly afterward.
- **Shortcuts:** F11 switches full screen; Esc leaves it (or dismisses the
  active popup). Cards Space/1–4/arrows/U and Quiz 1–4/Enter act once. Editing
  search, preferences or combo boxes does not trigger study shortcuts. Held keys
  do not duplicate saves. Every action remains reachable by Tab/Shift+Tab.
- **High contrast:** use Windows contrast themes; verify text, focus outlines,
  selected/disabled choices, reveal panels, dialogs and game feedback. Correctness
  must not depend only on green/red colors or animation.
- **Reduced motion:** enable app reduced motion and Windows animation settings;
  verify entrances, pulses and timer transitions respect preferences. Feedback
  remains understandable without motion. This is not established by the smoke's
  check that a preference is loaded.
- **Scaling/layout:** test Windows **140% DPI** where available, separately the
  app's **140% text size**, and **800×680 logical** window size. Also test the
  combination and common 100/150/200% OS scales. Distinguish physical pixels from
  logical units. No clipped headings/buttons/dialogs; reach all content with
  buttons, tabs or popup pages rather than vertical or horizontal scrolling.
- **Long translations:** repeat with German, French, Dutch and other supported
  UI languages; inspect wrapping, combo widths, instructions, summary and leave
  dialogs. English smoke selectors are not a multilingual accessibility audit.
- **Audio:** check correct installed local voice, missing-voice message,
  cancellation on navigation, repeated playback and no network requirement.
- **Recovery:** manually exercise restart resume and backup export/import with
  valid, invalid and incompatible files; rejected imports must preserve state.

## Availability and evidence limits

The catalog contains 25 games, but not every game can start for every language,
level or device. Reading and semantic relationships currently use dedicated
English/German/French data. Sparse or nonreciprocal relationship evidence may
leave a level unavailable. Cloze/sentence games need usable examples; category,
class, crossword and rack games need eligible metadata/words. Audio depends on
installed local speech voices. An unavailable route must explain the missing
requirement; it must not invent answers or silently substitute another mechanic.

The games harness reports local dataset eligibility, including unavailable
cases. That is evidence of guarded behavior, not proof of universal coverage.
The older content-quality documents and archived analyses are not current audit
results. No defect percentages or full-feature support claims are inferred from
them. Keep raw current reports with the commit/build identifier when reviewing a
release, and treat data drift and localization contract failures as gates to
investigate rather than suppress.