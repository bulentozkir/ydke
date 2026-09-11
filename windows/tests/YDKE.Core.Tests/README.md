# YDKE core regression harness

Dependency-free .NET 10 console tests, linking the actual native `Models.cs`,
`LearningEngine.cs`, and `AppStorage.cs`. Only the three model palette constants
are stubbed. No WinUI, application launch, network service, test-framework package,
or real user profile is needed. Each storage test uses a unique temporary folder.

Run from the repository root with the pinned .NET 10 SDK:

```powershell
# Same SDK selection as windows/build.ps1, without building the WinUI app.
$userSdk = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
if (Test-Path (Join-Path $userSdk 'sdk\10.0.400')) {
    $env:DOTNET_ROOT = $userSdk
    $env:PATH = "$userSdk;$env:PATH"
}
dotnet run --project windows/tests/YDKE.Core.Tests/YDKE.Core.Tests.csproj -c Release
```

The process exits nonzero on any failure. Tests exercise date boundaries,
same-day streaks/ratings, normalization without accent loss, rack multiplicity,
legacy migration, all new model fields, backup rotation/recovery, invalid import
payloads and file-size limits, rejected-write preservation, and concurrent I/O.

## UI integration contract

All core types retain the native `YDKE_Windows` namespace and `internal` visibility.
Methods listed below are public members available within the application assembly.

- `WordReview LearningEngine.Rate(ProgressState progress, string key, RecallRating rating, DateOnly today)`
  updates only `Reviews`. `RecallRating` is `Again`, `Hard`, `Good`, `Easy`.
  It does **not** alter legacy totals, known/favorite flags, or daily activity.
- `IReadOnlyList<VocabularyEntry> LearningEngine.DueWords(IEnumerable<VocabularyEntry> words, ProgressState progress, DateOnly today)`
  returns reviewed, due words only, ordered by due date then ordinal key.
- `void LearningEngine.RegisterActivity(ProgressState progress, DateOnly today)`
  increments the day's activity for every call, and the streak only once per day.
  **Legacy compatibility only. New flows must not call it.** Use the complete
  review/answer contracts in [Review-Handoff.md](Review-Handoff.md) instead.
- `string LearningEngine.NormalizeAnswer(string? answer, string? languageCode = null)`
  performs Unicode/case/whitespace/punctuation normalization and strips one article.
  Prefer an explicit study language to avoid cross-language article ambiguity.
- `bool LearningEngine.CanBuildFromRack(string? answer, string? rack)` uses literal
  Unicode tile counts; accents and duplicates matter, punctuation does not.
  Neither argument has an article stripped. Candidate comes first, rack second.
- `void LearningEngine.ValidateProgress(ProgressState progress)` and
  `void LearningEngine.ValidateSettings(UserSettings settings)` reject invalid state
  without repairing or mutating it. Explicit null collections are invalid; missing
  new properties in legacy JSON retain initialized defaults.

Progress retains all legacy members and adds:

| Property | Contract |
| --- | --- |
| `Reviews` | `Dictionary<string, WordReview>`, exact `VocabularyEntry.Key` |
| `DailyActivity` | `Dictionary<string, int>`, invariant `yyyy-MM-dd`, positive action counts |
| `CardPositions` | `Dictionary<string, string>`, `language:level` to a word key in that context |
| `Sessions` | `Dictionary<string, StudySessionState>`, `language:level:mode`; mode is a 1–64 character ASCII letter/digit/hyphen/underscore identifier |

`WordReview`: `DateOnly DueDate`, `int IntervalDays`, `int Repetitions`, `int Mistakes`,
`DateOnly? LastReviewed`. A null `LastReviewed` is an unreviewed placeholder with
zero interval/repetitions/mistakes. Reviewed entries require due date minus last
review date to equal the positive interval. Repetitions count consecutive non-Again
reviews, not total actions. Same-day retries do not multiply intervals.

`StudySessionState`: `List<string> WordKeys`, `int Index`,
`Dictionary<string, bool> Answers`. Keys are unique and belong to the session's
language/level; answers must refer to that list. Index is zero-based; `Count` means
completed. Empty sessions are permitted. Resume by key, resolving against the local
dataset; missing dataset words require UI handling, not an invented replacement.

Settings add `DailyGoal` (default 20, valid 1–10000), `ReduceMotion`, and
`UntimedPractice`. Settings and progress expose `SchemaVersion` (currently 1).

### Storage

- `AppStorage(string? folder = null)` defaults to the existing local YDKE folder.
- `Task<UserSettings> LoadSettingsAsync()`, `Task<ProgressState> LoadProgressAsync()`.
- `Task SaveSettingsAsync(UserSettings settings)`, `Task SaveProgressAsync(ProgressState progress)`.
- `Task ExportAsync(string path, UserSettings settings, ProgressState progress)`.
- `Task<(UserSettings Settings, ProgressState Progress)> ImportAsync(string path)`.
- `string? RecoveryMessage { get; }`, `void ClearRecoveryMessage()`.

Import **only reads and validates**, never writes. After success, obtain user
confirmation and explicitly save/apply both returned objects. A rejected import
leaves the input and all existing storage unchanged. Exports use an envelope with
`Format: "YDKE.Backup"`, `Version: 1`, `Settings`, and `Progress`. File size is capped
at 32 MiB; duplicate/unknown JSON properties, unsupported versions, invalid keys,
null collections, and invalid numeric/date ranges are rejected. Legacy unversioned
local settings/progress are accepted; raw local progress is not an import envelope.

Each save snapshots arguments before its first await. All in-process storage
instances serialize I/O with a semaphore. Unique same-directory temporary files
are flushed then renamed. The three most recent valid pre-save generations use
`.bak1`, `.bak2`, `.bak3` (newest first). Recovery tries these in order and leaves
the original untouched; failed validation is surfaced through `RecoveryMessage`.
Before replacing any invalid local source during an explicit save, an exact copy
is preserved under a unique `.corrupt-<guid>` suffix. Failure to preserve aborts
the save. There is no automatic deletion of preserved corruption evidence.

Storage clears `CloudConnected` on deserialized/cloned settings, without mutating
the caller's object. The old local-only `SaveCloudProfileAsync` remains obsolete
for source compatibility; remove UI calls (the app treats warnings as errors).

### Deliberate limits

- No cloud/auth/network operations; no change to offline/native architecture.
- Persistence locking is **in-process**, not a cross-process mutex. A save of
  settings followed by progress is **not a two-file transaction**.
- Mutable model ownership stays with the UI thread. The semaphore protects file
  I/O, not concurrent edits to dictionaries while taking an argument snapshot.
- Recovery notices are English diagnostic text containing local paths; the UI
  should show/localize a recovery explanation and clear the notice after display.
- Validation bounds individual collections to 100000 entries, sessions to 10000
  words, counters to 1000000000, and intervals to 36500 days. Word keys are checked
  structurally (known language and CEFR level), not against an installed dataset.
- Full WinUI compilation/UI integration and crash/power-loss or cross-process
  fault injection are outside this harness.

## Review fixes: current integration contracts

See [Review-Handoff.md](Review-Handoff.md) for the new accounting APIs, finite
retry/session semantics, persistent language-level migration, localization
manifest, and the exact main-page/storage changes required from their owners.
The harness also runs the native UI's extracted pure session and quiz-response
helpers, testing restart, finite missed queues, undo snapshots, first-attempt
accuracy, atomic batch validation and all-language unique-word goals.