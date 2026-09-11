# Native game attribution / progress handoff

## Scope

Changed only the two owned native partials, the allowed game engine and this
games-test directory. No Models, LearningEngine, Study, Library, AppStorage,
ExperienceStrings, Localizer or application-XAML edits were made by this work.
Existing/concurrent owners' changes were retained. No app/UI was launched, no
packaging was run and no real YDKE profile was read or modified.

## Attribution contract

- `ResolveGameAnswerAsync` requires `IReadOnlyList<string> reviewedKeys` separately
  from `feedback`. There is no display-answer lookup and no legacy
  `CorrectAnswers` / `WrongAnswers` / `RegisterActivity` / `Rate` writer in the
  two owned partials. The unused generic `RegisterAnswerAsync` was removed.
- The save helper freezes keys and local date before any await, calls
  `LearningEngine.RecordScoredAnswer(progress, keys, correct, true, today)` inside
  `MutateStudyAsync`, and retries the same save intent after snapshot rollback.
- One accepted scored attempt is **one game answer**, even if it tests two
  vocabulary keys. Word/day credit is deduplicated by LearningEngine, not by
  feedback, spelling normalization or number of objective attempts. No game
  action changes card self-rating counters or manual known marks.
- Memory scores each selected pair: a match credits one word; a mismatch tests
  only the two selected words, not the other four board words. Bingo scores each
  requested definition's word, not all 16 cells. Boss Rush scores every question
  before changing HP/hearts. CodyCross scores each submitted clue before changing
  solved rows. Accepted full-word Word Guess / Scramble tries are scored too;
  Hangman's individual letter selections are not full-word scored attempts.
- Crossword now submits Across first, then Down. Wrong entries can be retried;
  each submit scores only its active entry. Solved Across is disabled and cannot
  be rescored or marked wrong because Down is partial/incorrect.
- Those renderers explicitly pass `answerAlreadyRecorded: true` to final round
  resolution. Final feedback/round points do not add an extra objective answer
  or repeat the final word review. Board state advances only after successful
  persistence; captured round epochs reject stale asynchronous continuations.
- Sentence puzzles retain their source VocabularyEntry and require that word to
  occur in the usable example. Cloze, audio and composite feedback retain their
  explicit source key. Reading passes empty keys, so its objective count/streak
  changes but no vocabulary credit is invented.
- Semantic pairs credit only ordinal-exact headwords present in the current
  language/level vocabulary. Missing terms, different articles, case, accents or
  levels do not manufacture keys. Category/rack input validation can recognize a
  real submitted word; unknown input gets objective credit only. Rack feedback's
  seed solution is not treated as a word the player failed to recall.

## Completion and display limits

- `RegisterGameCompleted` is called only from `CompleteGameAsync`, in the same
  snapshot/save as the scoped best score. A per-session `GameSaveGate` commits
  only after successful persistence. Failed saves retry both changes together;
  duplicate callbacks cannot inflate completion counts.
- Completion is **per game session**, not per word, boss, clue or puzzle board.
  Standard/practice sessions keep the existing finite round/life limits; daily
  challenge has one round; Survival ends on its first miss rather than a hidden
  ten-round cap. Timed games have no ten-round limit, and end on time/life limits
  (or category exhaustion). This does not migrate old mixed totals or best scores.
- Answer saves are retained on abandon. Leaving during an unsaved completion
  retry does not count a completion or save a best score. There is no resumable
  active-game state or cross-restart completion-id journal; a failed/abandoned
  game is not reconstructed as completed. Final round feedback still waits for
  Continue before completing the session.
- The main timed progress bar is **elapsed active seconds / 60** and updates on
  every timer tick, independently of round count. The small time HUD remains
  remaining seconds. Finite untimed progress uses its real round limit (daily
  `/1`); Survival's misleading finite bar is hidden. This does not change the
  existing dispatcher-tick active-time policy into wall-clock timing.
- Clock-based mechanics under practice advertise unlimited **time**, never 60
  seconds; their round and life limits still apply. Their current duration is
  `practice`, so the 60-second filter does not match them. Mode labels reuse the
  existing exact resource keys `Games.Mode.practice`, `Games.Mode.timed` and
  `Games.Mode.standard` (lowercase suffixes in the current resource table).
- `RenderStats()` delegates solely to `AddLearningStats()`; Profile uses
  `MarkedKnownLabel`. No duplicate scoped-score/statistics block remains.

## New U keys — localization-owner manifest

These defaults are at actual U call sites. Resource tables were **not** edited;
until the localization owner adds these keys, Turkish uses its fallback below
and the other UI languages use the English fallback. Do not treat that fallback
as seven-language translation coverage.

| Key | English | Turkish |
| --- | --- | --- |
| Games.Duration.Practice | Unlimited time (practice) | Sınırsız süre (alıştırma) |
| Games.Instructions.PracticeRace | Untimed practice: type the word matching each definition. Unlimited time; round and life limits still apply. | Süresiz alıştırma: her tanıma uyan kelimeyi yazın. Süre sınırsızdır; tur ve can sınırları geçerlidir. |
| Games.Instructions.PracticeCategory | Untimed practice: recall different words from one fixed category in this language and level. Unlimited time; each word scores once, and round and life limits still apply. | Süresiz alıştırma: bu dil ve seviyede sabit bir kategoriden farklı kelimeler hatırlayın. Süre sınırsızdır; her kelime bir kez puanlanır, tur ve can sınırları geçerlidir. |
| Games.Instructions.PracticeSpeed | Untimed practice: choose the word matching each meaning. Unlimited time; round and life limits still apply. | Süresiz alıştırma: her anlama uyan kelimeyi seçin. Süre sınırsızdır; tur ve can sınırları geçerlidir. |
| Games.Progress.Elapsed | Active seconds elapsed | Geçen etkin saniye |
| Games.CompletionRetry | Game completion and best score were not saved. Continue retries; leaving keeps recorded answers but does not count this completion. | Oyun tamamlanması ve en iyi puan kaydedilmedi. Devam tekrar dener; çıkmak kaydedilen yanıtları korur ancak bu tamamlanmayı saymaz. |
| Games.Crossword.Atomic | Submit Across first, then Down. Each entry is scored separately; a wrong entry never marks the other one wrong. | Önce yatay, sonra dikey yanıtı gönderin. Her kelime ayrı puanlanır; yanlış yanıt diğer kelimeyi yanlış saymaz. |

## Verification — 2026-09-08

- Existing console games harness: **106/106 passed**, including **24 new**
  source/attribution/progress regressions. Run using `dotnet run`, not
  `dotnet test`; the latter would not execute these console assertions.
- SDK Roslyn parses the actual native source and audits **28 Resolve calls in
  21 renderer methods** (shared by the 25 catalog games). Every caller's fifth
  argument and every atomic renderer's final-recorded flag are checked. New or
  removed callers fail the explicit inventory until reviewed.
- A runtime probe extracts and compiles the actual native resolver, save,
  sub-answer and completion methods. It uses inert controls and in-memory
  snapshot rollback, not retyped copies of product scoring logic. Tests cover
  feedback independence, multi-key objective counts, reading/semantic keys,
  duplicate callbacks, failed-save retries, mutable-input snapshots, abandoned
  answers/completions and stale continuations.
- Native Release build: **succeeded, 0 warnings / 0 errors**. The concurrent
  storage API integration was available; no storage stubs or retry build loop
  were needed. `git diff --check` passed.
- German semantic data still reports **C1/C2 unavailable** due to no reciprocal
  evidence. No data or eligibility fallback was fabricated.
- **Not verified here:** live WinUI interactions, animation/focus behavior,
  Narrator, wall-clock timing precision, on-disk crash recovery or all-language
  localization coverage. Board ordering tests are static contracts plus scoring
  probes, not a native end-to-end UI test. Storage snapshot atomicity remains
  the storage owner's contract and separate test responsibility.