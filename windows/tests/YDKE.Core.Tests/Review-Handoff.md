# Native study review integration handoff

## Owned changes

Only [Models.cs](../../src/YDKE.Windows/Models.cs),
[LearningEngine.cs](../../src/YDKE.Windows/LearningEngine.cs),
[MainPage.Study.cs](../../src/YDKE.Windows/MainPage.Study.cs),
[MainPage.Library.cs](../../src/YDKE.Windows/MainPage.Library.cs) and this test
directory were edited. Existing pending work was preserved. No application was
launched and no real user YDKE data was inspected or changed.

## Exact game-owner APIs

These are public static methods on the internal `YDKE_Windows.LearningEngine`,
available directly inside the native assembly:

- `void RecordCardReview(ProgressState progress, string key, RecallRating rating, DateOnly today)`
  calls the scheduler, credits one distinct word for the local day, increments
  `RecallRatings[rating.ToString()]`, and updates the streak. This is **self-rated
  cards only**, not game/quiz correctness. No objective or legacy totals change.
- `void RecordScoredAnswer(ProgressState progress, IEnumerable<string> wordKeys, bool correct, bool isGame, DateOnly today)`
  materializes bounded input, deduplicates **ordinal exact keys**, validates all
  keys, capacity and dates before mutation, rates each distinct word Good/Again,
  credits each daily word once, and increments exactly **one** answer in either
  `GameCorrectAnswers`/`GameWrongAnswers` (`isGame: true`) or
  `QuizCorrectAnswers`/`QuizWrongAnswers` (`false`). No recall-rating count changes.
  A multi-word board/round is one scored attempt, not one answer per word.
  Pass an **empty collection for reading without vocabulary**: objective count
  and streak advance, but no invented review or daily word credit is created.
- `void RegisterGameCompleted(ProgressState progress)` increments only
  `CompletedGames`. Call exactly once at the successful end-of-game commit point,
  not on every render, round, or abandon. It has no date argument or streak side
  effect; an accepted answer is the meaningful learning action.

Callers must own duplicate-submit/completion guards and the mutable models on
the UI thread, and persist the resulting progress snapshot. Each helper rejects
invalid input before changing progress. Counters saturate at `MaxCounter`
(1,000,000,000). Game result and completion changes can share one save/rollback
transaction in the caller. Do **not** additionally call `Rate`, `RegisterActivity`,
or increment answer counters after `RecordScoredAnswer`.

`CorrectAnswers`, `WrongAnswers` and `DailyActivity` are preserved **historical
mixed values only**. The game owner must remove all remaining writers to them,
including the old generic answer path in
[MainPage.xaml.cs](../../src/YDKE.Windows/MainPage.xaml.cs).
No historical quiz accuracy, game accuracy or unique-word credit is inferred.

### Data schema (version remains 1, missing new fields start at zero/empty)

| Property | Type and meaning |
|---|---|
| `QuizCorrectAnswers`, `QuizWrongAnswers` | `int`, first accepted responses in normal quiz sessions |
| `GameCorrectAnswers`, `GameWrongAnswers` | `int`, one per scored game attempt |
| `CompletedGames` | `int`, one per completed game |
| `RecallRatings` | `Dictionary<string,int>`, exact names `Again`, `Hard`, `Good`, `Easy`; nonnegative bounded values |
| `DailyReviewedWords` | `Dictionary<string,HashSet<string>>`, invariant ISO local day `yyyy-MM-dd` to exact vocabulary keys, all languages/modes |
| `LastStudyLevels` (settings) | `Dictionary<string,string>`, valid study language to CEFR level |
| `StudySessionState.IsRetry` | `bool`, default false; true for explicit finite missed-item practice |

Explicit null collections, invalid enum names, non-ISO dates, malformed word
keys, invalid language/level pairs, and out-of-range counters/collections reject
validation. New collections use the existing 100,000-entry cap (sessions 10,000;
recall names 4; remembered languages 7). Storage's existing byte cap still applies.

## Manual known, sessions and first-attempt accuracy

- `KnownWords` stays a manual bookmark only; no rating/scored action edits it.
  UI wording is **Marked known**, with U to toggle the card/focused library word.
- `IReadOnlyList<VocabularyEntry> DueAndNew(IEnumerable<VocabularyEntry> words, ProgressState progress, DateOnly today)`
  returns reviewed due words first, then unreviewed words excluding manually
  known words. Reviewed due words remain due regardless of the manual mark.
  Null-last-review placeholders follow the unreviewed rule.
- `StudySessionState CreateSession(IEnumerable<string> wordKeys, bool isRetry = false)`
  creates a finite distinct same-context queue, with empty answers and index 0.
- `IReadOnlyList<string> MissedKeys(StudySessionState session)` returns only
  explicitly false responses in queue order; unanswered words are not misses.
- `int NextUnansweredIndex(StudySessionState session)` returns the first
  unanswered index or `WordKeys.Count`. No action ever appends/requeues cards.
- `bool RecordQuizResponse(ProgressState progress, StudySessionState session, string key, bool correct, DateOnly today)`
  is the UI/test accepted-response gate: mismatched/already-answered/completed
  responses are no-ops. Normal sessions use `RecordScoredAnswer`; retry sessions
  update schedule, daily words and streak without changing objective accuracy
  or card self-rating counts. Returns true only for an accepted response.

Cards and quizzes both persist completed-session missed responses and show them
again after restart. Explicit **Retry missed cards / Practice missed words**
creates a fresh finite subset through the same creation/save path; another miss
still ends that pass. Again's long-term schedule remains tomorrow. Undo restores
the whole pre-rating snapshot, including new metrics, daily word sets, streak
and session position; no automatic mastery/quick-mastery behavior was added.

## Settings and storage-owner integration

- `void LearningEngine.ChangeStudySelection(UserSettings settings, string language, string? level = null)`
  validates before changing state; stores outgoing and incoming remembered levels
  in the same settings mutation. UI saves once and restores its snapshot on a
  failed save. Missing other-language selection defaults to A1; there is no
  session-history heuristic or ephemeral language-level dictionary.
- `UserSettings` implements `IJsonOnDeserialized`: validate the original shape,
  then assign `LastStudyLevels[StudyLanguage] = Level`. Thus the current `Level`
  is authoritative during legacy load, import and restart even if the remembered
  current entry is stale. No extra main-page migration hook is needed.
- The library now calls the agreed storage APIs:
  `public string FolderPath { get; }` and
  `public Task ApplyImportAsync(UserSettings settings, ProgressState progress)`.
  **The storage owner must implement both.** No stubs or storage edits were made.
- Import first exports its safety backup to `FolderPath`, then invokes
  `ApplyImportAsync` once. That method must own validation and the crash-recovery
  journal for the settings/progress pair. Only after success does the UI replace
  in-memory models, invalidate undo/focus/library state, reload vocabulary and
  render. On failure it retains old in-memory state and reports the safety path;
  it does not issue compensating two-file writes. Recovery must restore a
  coherent old/new pair before a subsequent load/save/export proceeds.
- Export's local-folder restriction and safety location use `FolderPath`, not
  a guessed user-profile path. A separate `before-import-<guid>` safety export
  inside that folder must remain allowed by storage's managed-file protection.

## Required main-page-owner changes (not edited here)

In [MainPage.xaml.cs](../../src/YDKE.Windows/MainPage.xaml.cs):

1. Replace the **entire `RenderStats()` body** with `AddLearningStats();`.
   That helper now adds the page header, all-language unique-word day/week goal
   figures, separate card ratings/quiz accuracy/game accuracy/completions,
   language/latest-review figures, clearly labeled legacy history, and the
   existing `AddScopedGameStatistics()` call. Do not call the scoped helper twice
   or keep the old mixed-counters accuracy grid.
2. In `RenderProfile()`, the local summary currently interpolates
   `T("Home.Known")`. Replace that expression with `MarkedKnownLabel`; keep its
   known count and favorites count unchanged. Home and Library already use this
   new label. The profile learning controls also explain manual marking.
3. Remove remaining new-action writes to legacy totals/activity with the game
   owner. Use the scored APIs above and preserve existing game completion guards.

## Focus/accessibility handoff

Requests capture expected page, study context, element ID, source focus and a
monotonic token before rebuilding. Restoration runs after Loaded on an attached,
enabled target, rejects stale contexts/tokens, and yields to editing/new focus.
Card favorites update their own content/automation name in place (no render).
Library actions preserve expanded keys; after filter removal focus goes to the
next visible result (or previous last result), or search when none remain. A
retained collapsed result restores its expander rather than a hidden button.
Space/Enter on a native button are not also handled as custom shortcuts (original
event source and current focus are checked); U/number/arrows retain their guards.

Stable AutomationIds:

- `cards.Start`, `cards.Reveal`, `cards.Listen`, `cards.Previous`, `cards.Next`,
  `cards.Undo`, `cards.Rating.Again/Hard/Good/Easy`, `cards.RetryMissed`.
- `quiz.Start`, `quiz.Choice.0` through `quiz.Choice.3`, `quiz.Continue`, `quiz.RetryMissed`.
- `favorite.<wordkey>`, `known.<wordkey>` (card or library page context).
- `library.Search`, `library.Filter.Favorites/Known/Due`, `library.Category`,
  `library.Practice`, `library.More`, `library.Word.<wordkey>`, `library.Listen.<wordkey>`.

## Localization manifest: new U keys and exact defaults

All new strings are U calls. Existing reused keys (such as `Rating.*`,
`Quiz.Missed`, `Favorite.*`, `Session.*` already present) keep their defaults.
No edits were made to ExperienceStrings or Localizer.

| Key | English | Turkish |
|---|---|---|
| Known.Marked | Marked known | Biliniyor olarak işaretli |
| Known.Add | Mark known (U) | Biliniyor olarak işaretle (U) |
| Known.Remove | ✓ Marked known — unmark (U) | ✓ Biliniyor olarak işaretli — kaldır (U) |
| Known.ManualHint | Marked known (U) is a manual bookmark, not automatic mastery. It skips unreviewed new words, but never hides a scheduled due review. | Biliniyor işareti (U) elle konan bir yer imidir, otomatik ustalık değildir. Tekrar edilmemiş yeni kelimeleri atlar, ancak zamanı gelen tekrarı asla gizlemez. |
| Session.RetryPractice | Missed-word practice — does not change first-attempt quiz accuracy. | Yanlış kelime alıştırması — ilk deneme test doğruluğunu değiştirmez. |
| Session.MissedCount | Missed words | Yanlış kelimeler |
| Cards.RetryMissed | Retry missed cards | Yanlış kartları tekrar et |
| Cards.AgainHint | Again is due tomorrow and appears in the end-of-session summary. Choose Retry missed cards there for a finite extra pass; cards are never automatically requeued. | Tekrar seçilen kart yarın tekrar edilir ve oturum sonu özetinde görünür. Sonlu bir ek tur için orada Yanlış kartları tekrar et'i seçin; kartlar otomatik olarak sıraya eklenmez. |
| Goal.UniqueToday | Daily word goal — unique words, all languages | Günlük kelime hedefi — farklı kelimeler, tüm diller |
| Goal.UniqueTarget | Daily unique-word target — all languages (1–10000) | Günlük farklı kelime hedefi — tüm diller (1–10000) |
| Stats.SeparateMetrics | Recall ratings, quiz answers and game answers are separate measures. | Hatırlama puanları, test yanıtları ve oyun yanıtları ayrı ölçülür. |
| Stats.UniqueDays | Unique reviewed words — today / last 7 days, all languages | Farklı tekrarlanan kelimeler — bugün / son 7 gün, tüm diller |
| Stats.GoalScope | Cards, quizzes and games share word credit. Repeating a word on the same day counts once. Reading answers without vocabulary count only as scored answers. | Kartlar, testler ve oyunlar kelime sayımını paylaşır. Aynı gün tekrarlanan kelime bir kez sayılır. Kelime içermeyen okuma yanıtları yalnızca puanlanan yanıt sayılır. |
| Stats.Recall | Card self-ratings — not accuracy | Kart öz değerlendirmeleri — doğruluk değildir |
| Stats.QuizAnswers | Quiz answers — first attempts, all languages | Test yanıtları — ilk denemeler, tüm diller |
| Stats.GameAnswers | Game answers — scored attempts, all languages | Oyun yanıtları — puanlanan denemeler, tüm diller |
| Stats.CompletedGames | Completed games | Tamamlanan oyunlar |
| Stats.Legacy | Legacy counters — retained history only | Eski sayaçlar — yalnızca saklanan geçmiş |
| Stats.LegacyActions | Historical mixed activity actions | Geçmiş karma etkinlik işlemleri |
| Stats.LegacyHint | Old counters mixed self-ratings and scored answers. They are not converted into quiz accuracy, game accuracy or unique-word goals. New metrics start at zero when absent. | Eski sayaçlar öz değerlendirmeleri ve puanlanan yanıtları karıştırıyordu. Test doğruluğu, oyun doğruluğu veya farklı kelime hedeflerine dönüştürülmezler. Eksik yeni ölçümler sıfırdan başlar. |

## Verification

- Core harness: **24/24 passed**; all storage tests use unique temporary folders.
- **65 malformed/hostile import payloads rejected** without changing local storage.
  The harness is an executable: use `dotnet run`, not `dotnet test` (which only
  restores/builds this project and does not execute its console assertions).
- `git diff --check`: passed.
- One native build attempted: expected missing `AppStorage.FolderPath` (two
  references) and `AppStorage.ApplyImportAsync` (one), plus WinUI markup compiler
  WMC1509/WMC9999 fallout. No retry loop, stub APIs or off-scope edits.
- No live WinUI focus test was run: native build is blocked on the storage API.
  The runtime focus/keyboard/expander acceptance checks still need to be run by
  the integration owner after all branches/files are combined.