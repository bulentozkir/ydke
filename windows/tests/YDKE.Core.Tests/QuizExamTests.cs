using System.Globalization;
using System.Text.Json;
using YDKE_Windows;

// Runtime contracts against the linked production engine, not source-text checks.
// All vocabulary/history is synthetic; persistence uses JSON or the existing TestFolder.
internal static class QuizExamTests
{
    private const int FixtureCount = 1446;
    private const string Context = "en:A1";
    private const string LegacyKey = Context + ":quiz";
    private static readonly DateOnly Day = new(2026, 9, 12);

    public static void PoolAndIslands()
    {
        var expected = Fixture();
        var french = Fixture(27, "fr");
        var higherLevel = Fixture(24, level: "A2");
        var input = expected.Reverse().Concat(expected.Where((_, index) => index % 17 == 0))
            .Concat(french).Concat(higherLevel).Reverse().ToArray();
        var before = Json(input);
        var pool = QuizExamEngine.CreatePool(input, "en", "A1");

        Equal(20, QuizExamEngine.QuestionsPerExam, "The default island size must never revert to eight.");
        Equal(FixtureCount, pool.Length, "The entire level must remain available.");
        Sequence(expected.Select(word => word.Key), pool.Select(word => word.Key), "Stable sorted, deduplicated English pool");
        Sequence(pool.Select(word => word.Key), QuizExamEngine.CreatePool(input.Reverse(), "en", "A1").Select(word => word.Key),
            "Input order must not move words between islands");
        Sequence(french.Select(word => word.Key), QuizExamEngine.CreatePool(input, "fr", "A1").Select(word => word.Key), "Language isolation");
        Sequence(higherLevel.Select(word => word.Key), QuizExamEngine.CreatePool(input, "en", "A2").Select(word => word.Key), "Level isolation");
        Equal(before, Json(input), "Pool construction must not mutate the source.");

        Equal(73, QuizExamEngine.ExamCount(pool.Length), "1446 words require 73 numbered islands.");
        var allKeys = new List<string>();
        for (var index = 0; index < 73; index++)
        {
            var island = QuizExamEngine.ExamWords(pool, index);
            Equal(index == 72 ? 6 : 20, island.Length, $"Island {index + 1} question count");
            Sequence(expected.Skip(index * 20).Take(20).Select(word => word.Key), island.Select(word => word.Key),
                $"Island {index + 1} exact boundary");
            allKeys.AddRange(island.Select(word => word.Key));
        }
        Sequence(expected.Select(word => word.Key), allKeys, "Every word appears once across the islands");
        Equal(FixtureCount, allKeys.Distinct(StringComparer.Ordinal).Count(), "Islands must not overlap.");
        foreach (var (words, islands) in new[] { (0, 0), (1, 1), (8, 1), (19, 1), (20, 1), (21, 2), (40, 2), (1440, 72), (1446, 73) })
            Equal(islands, QuizExamEngine.ExamCount(words), $"ExamCount({words})");
        var empty = QuizExamEngine.CreatePool(input, "nl", "C2");
        Equal(0, empty.Length, "A missing context must not borrow another language's words.");
        Equal(0, QuizExamEngine.ExamCount(empty.Length), "An empty pool has no islands.");

        var firstApple = Word("apple") with { Definition = "first duplicate retained" };
        VocabularyEntry[] ties = [Word("éclair"), Word("ibis"), Word("Banana"), firstApple, Word("APPLE"), Word("zebra"), Word("Ibis"), Word("apple")];
        var culture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var name in new[] { "en-US", "tr-TR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                var sorted = QuizExamEngine.CreatePool(ties, "en", "A1");
                Sequence(["APPLE", "apple", "Banana", "Ibis", "ibis", "zebra", "éclair"], sorted.Select(word => word.Word),
                    $"Ordinal ordering and deterministic case ties under {name}");
                Check(ReferenceEquals(firstApple, sorted.Single(word => word.Word == "apple")), "Deduplication retains the first exact key.");
            }
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    public static void ExactResumeBoundaries()
    {
        var pool = Fixture();
        var reordered = pool.Take(20).Select(word => word.Key).ToArray();
        (reordered[1], reordered[2]) = (reordered[2], reordered[1]); // Keep the valid first key; test the entire sequence.
        var cases = new (string Name, int ExpectedIndex, string[] Keys)[]
        {
            ("legacy first-eight prefix", -1, pool.Take(8).Select(word => word.Key).ToArray()),
            ("twenty words from the wrong boundary", -1, pool.Skip(1).Take(20).Select(word => word.Key).ToArray()),
            ("reordered twenty after a valid first key", -1, reordered),
            ("nineteen-word middle island", -1, pool.Skip(20).Take(19).Select(word => word.Key).ToArray()),
            ("six-word middle prefix is not the last remainder", -1, pool.Skip(20).Take(6).Select(word => word.Key).ToArray()),
            ("truncated last remainder", -1, pool.Skip(1440).Take(5).Select(word => word.Key).ToArray()),
            ("overlong island", -1, pool.Take(21).Select(word => word.Key).ToArray()),
            ("empty session", -1, []),
            ("first full island", 0, pool.Take(20).Select(word => word.Key).ToArray()),
            ("second full island", 1, pool.Skip(20).Take(20).Select(word => word.Key).ToArray()),
            ("valid last six", 72, pool.Skip(1440).Select(word => word.Key).ToArray()),
        };
        Check(!QuizExamEngine.TryGetExamIndex(pool, null, out var missingIndex), "A missing session cannot identify an island.");
        Equal(-1, missingIndex, "Missing session index");

        foreach (var (name, expectedIndex, keys) in cases)
        {
            var candidate = LearningEngine.CreateSession(keys);
            Equal(expectedIndex >= 0, QuizExamEngine.TryGetExamIndex(pool, candidate, out var found), name);
            Equal(expectedIndex, found, name + " reported index");
            foreach (var storageKey in new[] { LegacyKey, QuizExamEngine.SessionKey(Context, Math.Max(0, expectedIndex)) })
            {
                var progress = History();
                // Supply intact options so an invalid session cannot pass merely because options were missing.
                var saved = SeedSaved(progress, pool, storageKey, keys);
                LearningEngine.ValidateProgress(progress);
                var before = Json(progress);
                for (var index = 0; index < 73; index++)
                {
                    var resumed = QuizExamEngine.SavedExam(progress, pool, index);
                    Check(index == expectedIndex ? ReferenceEquals(saved, resumed) : resumed is null,
                        $"{name} stored at {storageKey} must only resume its exact island, not {index}.");
                }
                Equal(before, Json(progress), name + " lookup must be read-only");
            }
        }

        var wrongSlot = History();
        SeedSaved(wrongSlot, pool, QuizExamEngine.SessionKey(Context, 0), pool.Skip(20).Take(20).Select(word => word.Key));
        var wrongSlotBefore = Json(wrongSlot);
        Check(QuizExamEngine.SavedExam(wrongSlot, pool, 0) is null, "A valid second island saved under island zero cannot resume there.");
        Check(QuizExamEngine.SavedExam(wrongSlot, pool, 1) is null, "Lookup must not relocate a wrongly keyed named session.");
        Equal(wrongSlotBefore, Json(wrongSlot), "Wrong-slot lookup must not rewrite history.");
    }

    public static async Task IndependentIslandsRoundtrip()
    {
        using var folder = new TestFolder();
        var storage = new AppStorage(folder.Path);
        var pool = Fixture();
        var progress = History();
        var initial = Json(progress);
        for (var index = 0; index < 73; index++)
            Check(QuizExamEngine.SavedExam(progress, pool, index) is null, "Listing islands must not create an active session.");
        Equal(initial, Json(progress), "Unselected islands remain unstarted.");

        var firstKey = QuizExamEngine.SessionKey(Context, 0);
        var secondKey = QuizExamEngine.SessionKey(Context, 1);
        var metrics = NonSessionJson(progress);
        var first = QuizExamEngine.OpenExam(progress, pool, 0, new Random(101));
        CheckFreshExam(progress, pool, 0, 20);
        Equal(metrics, NonSessionJson(progress), "Opening island zero must not count as studying.");
        Check(QuizExamEngine.SavedExam(progress, pool, 1) is null, "Opening one island must not start another.");
        Check(!progress.Sessions.ContainsKey(LegacyKey), "New islands must not create the old global quiz alias.");
        AnswerCurrent(progress, firstKey, true);
        first.Index = 1;
        var wrongChoice = AnswerCurrent(progress, firstKey, false); // Leave feedback visible at a nonzero index.
        var optionOrder = progress.Sessions[QuizExamEngine.OptionsKey(Context, 0, 1)].WordKeys.ToArray();
        var firstSnapshot = SessionJson(progress, firstKey);
        var beforeSave = Json(progress);
        await storage.SaveProgressAsync(progress);
        progress = await new AppStorage(folder.Path).LoadProgressAsync();
        Equal(beforeSave, Json(progress), "The first island survives an isolated storage reload.");

        metrics = NonSessionJson(progress);
        var second = QuizExamEngine.OpenExam(progress, pool, 1, new Random(202));
        CheckFreshExam(progress, pool, 1, 20);
        Equal(metrics, NonSessionJson(progress), "Switching to island one must not alter counters or reviews.");
        Equal(firstSnapshot, SessionJson(progress, firstKey), "Switching must preserve every first-island answer and option.");
        Check(!ReferenceEquals(progress.Sessions[firstKey].Answers, second.Answers), "Islands must not share answer dictionaries.");
        AnswerCurrent(progress, secondKey, false);
        second.Index = 1;
        var secondSnapshot = SessionJson(progress, secondKey);
        beforeSave = Json(progress);
        await storage.SaveProgressAsync(progress);
        progress = await new AppStorage(folder.Path).LoadProgressAsync();
        Equal(beforeSave, Json(progress), "Both independently answered islands survive storage restart.");

        var beforeResume = Json(progress);
        var resumed = QuizExamEngine.OpenExam(progress, pool, 0, new Random(999));
        Equal(beforeResume, Json(progress), "Returning 0 -> 1 -> 0 is an exact no-op, including counters.");
        Check(!ReferenceEquals(first, resumed), "The resumed session must come from reloaded state, not the pre-save object.");
        Check(ReferenceEquals(progress.Sessions[firstKey], resumed), "Resume returns the stored named island.");
        Equal(1, resumed.Index, "The question cursor must not reset to zero.");
        Check(resumed.Answers.TryGetValue(pool[1].Key, out var correct) && !correct, "The previously wrong response stays wrong.");
        var selectedOptions = progress.Sessions[QuizExamEngine.OptionsKey(Context, 0, 1)];
        Equal(wrongChoice, selectedOptions.Answers.Single().Key, "The exact selected distractor survives restart.");
        Check(!selectedOptions.Answers.Single().Value, "The saved distractor must not become correct.");
        Equal(4, selectedOptions.Index, "The answered options retain their completed cursor.");
        Sequence(optionOrder, selectedOptions.WordKeys, "Resume must not reshuffle saved choices");
        Equal(firstSnapshot, SessionJson(progress, firstKey), "All first-island option orders and indices survive.");
        Equal(secondSnapshot, SessionJson(progress, secondKey), "Returning to zero leaves island one untouched.");

        metrics = NonSessionJson(progress);
        QuizExamEngine.OpenExam(progress, pool, 72, new Random(303));
        CheckFreshExam(progress, pool, 72, 6);
        Equal(metrics, NonSessionJson(progress), "Opening the last remainder must not change statistics.");
        Check(!progress.Sessions.ContainsKey(QuizExamEngine.OptionsKey(Context, 72, 6)), "The remainder must not acquire padded questions.");
        Equal(firstSnapshot, SessionJson(progress, firstKey), "The last island must not overwrite island zero.");
        Equal(secondSnapshot, SessionJson(progress, secondKey), "The last island must not overwrite island one.");
        beforeSave = Json(progress);
        await storage.SaveProgressAsync(progress);
        var reloadedStorage = new AppStorage(folder.Path);
        var reloaded = await reloadedStorage.LoadProgressAsync();
        Equal(beforeSave, Json(reloaded), "Full and remainder islands share the existing validated persistence schema.");
        Check(reloadedStorage.RecoveryMessage is null, "A valid island save must not silently recover a backup or defaults.");
        Equal(6, QuizExamEngine.SavedExam(reloaded, pool, 72)!.WordKeys.Count, "The persisted remainder resumes as six questions.");
    }

    public static void LegacyMigrationOnSelection()
    {
        var pool = Fixture();
        var progress = History();
        var legacy = SeedSaved(progress, pool, LegacyKey, pool.Skip(20).Take(20).Select(word => word.Key));
        var wrongChoice = AnswerCurrent(progress, LegacyKey, false);
        legacy.Index = 1;
        AnswerCurrent(progress, LegacyKey, true);
        progress = Reload(progress);
        legacy = progress.Sessions[LegacyKey];
        var legacySnapshot = SessionJson(progress, LegacyKey);
        var beforeLookup = Json(progress);
        for (var index = 0; index < 73; index++)
            Check(index == 1 ? ReferenceEquals(legacy, QuizExamEngine.SavedExam(progress, pool, index))
                : QuizExamEngine.SavedExam(progress, pool, index) is null, "Legacy lookup must identify only its aligned twenty-word island.");
        Equal(beforeLookup, Json(progress), "Discovering a valid legacy exam must not migrate it.");

        var metrics = NonSessionJson(progress);
        QuizExamEngine.OpenExam(progress, pool, 0, new Random(404));
        Check(!progress.Sessions.ContainsKey(QuizExamEngine.SessionKey(Context, 1)), "Selecting another island must not migrate the legacy exam.");
        Equal(legacySnapshot, SessionJson(progress, LegacyKey), "Unselected legacy questions and choices remain untouched.");
        Equal(metrics, NonSessionJson(progress), "Opening another island must not reset legacy statistics.");
        var firstSnapshot = SessionJson(progress, QuizExamEngine.SessionKey(Context, 0));

        var migratedKey = QuizExamEngine.SessionKey(Context, 1);
        var migrated = QuizExamEngine.OpenExam(progress, pool, 1, new Random(505));
        Equal(Json(legacy), Json(migrated), "Selection migrates the exact answers, cursor, order and retry attribution.");
        Check(!ReferenceEquals(legacy, migrated) && !ReferenceEquals(legacy.WordKeys, migrated.WordKeys) &&
            !ReferenceEquals(legacy.Answers, migrated.Answers), "Migration must deep-copy the legacy session without aliases.");
        for (var index = 0; index < 20; index++)
        {
            var oldOptions = progress.Sessions[OptionKey(LegacyKey, index)];
            var newOptions = progress.Sessions[QuizExamEngine.OptionsKey(Context, 1, index)];
            Equal(Json(oldOptions), Json(newOptions), $"Legacy question {index} exact persisted options");
            Check(!ReferenceEquals(oldOptions, newOptions) && !ReferenceEquals(oldOptions.WordKeys, newOptions.WordKeys) &&
                !ReferenceEquals(oldOptions.Answers, newOptions.Answers), $"Question {index} options must not alias legacy history.");
        }
        Equal(wrongChoice, progress.Sessions[QuizExamEngine.OptionsKey(Context, 1, 0)].Answers.Single().Key, "Migration retains the selected wrong answer.");
        Equal(metrics, NonSessionJson(progress), "Migration is not a new scored attempt.");
        Equal(firstSnapshot, SessionJson(progress, QuizExamEngine.SessionKey(Context, 0)), "Migration leaves another island intact.");
        var beforeReopen = Json(progress);
        Check(ReferenceEquals(migrated, QuizExamEngine.OpenExam(progress, pool, 1, new Random(606))), "A second selection resumes, not remigrates.");
        Equal(beforeReopen, Json(progress), "Repeated migration selection is read-only.");
        migrated.Index = 2;
        AnswerCurrent(progress, migratedKey, false);
        Equal(legacySnapshot, SessionJson(progress, LegacyKey), "Answering the migrated copy must not write through to old session/options.");
        Equal(firstSnapshot, SessionJson(progress, QuizExamEngine.SessionKey(Context, 0)), "Migrated answers remain island-local.");
        LearningEngine.ValidateProgress(progress);

        var oldProgress = History();
        var oldEight = SeedSaved(oldProgress, pool, LegacyKey, pool.Take(8).Select(word => word.Key));
        AnswerCurrent(oldProgress, LegacyKey, false);
        oldEight.Index = 1;
        oldProgress = Reload(oldProgress);
        var eightSnapshot = SessionJson(oldProgress, LegacyKey);
        var eightBeforeLookup = Json(oldProgress);
        for (var index = 0; index < 73; index++)
            Check(QuizExamEngine.SavedExam(oldProgress, pool, index) is null, "The old eight-question session must never become active on this level.");
        Equal(eightBeforeLookup, Json(oldProgress), "Rejecting old eight-word history must not erase it.");
        metrics = NonSessionJson(oldProgress);
        var fresh = QuizExamEngine.OpenExam(oldProgress, pool, 0, new Random(707));
        CheckFreshExam(oldProgress, pool, 0, 20);
        Check(!ReferenceEquals(oldProgress.Sessions[LegacyKey], fresh), "An old eight-word quiz cannot be returned as the selected island.");
        Equal(eightSnapshot, SessionJson(oldProgress, LegacyKey), "Old eight-word answers/options remain inert and unchanged.");
        Equal(metrics, NonSessionJson(oldProgress), "Discarding an eight-word resume candidate must not reset any statistics.");
        LearningEngine.ValidateProgress(oldProgress);
    }

    public static void WholeIslandRestart()
    {
        var pool = Fixture();
        var progress = History();
        var firstKey = QuizExamEngine.SessionKey(Context, 0);
        var correctBefore = progress.QuizCorrectAnswers;
        var wrongBefore = progress.QuizWrongAnswers;
        var legacyCounters = Json(new { progress.CorrectAnswers, progress.WrongAnswers, progress.DailyActivity, progress.RecallRatings,
            progress.GameCorrectAnswers, progress.GameWrongAnswers, progress.CompletedGames });
        var first = QuizExamEngine.OpenExam(progress, pool, 0, new Random(808));
        for (var index = 0; index < 20; index++)
        {
            AnswerCurrent(progress, firstKey, index % 5 != 0);
            first.Index++;
        }
        Equal(20, first.Index, "The first pass completes all twenty questions.");
        Equal(correctBefore + 16, progress.QuizCorrectAnswers, "First-attempt correct responses count once.");
        Equal(wrongBefore + 4, progress.QuizWrongAnswers, "First-attempt wrong responses count once.");
        var completed = Json(first);
        var beforeCompletedResume = Json(progress);
        Check(ReferenceEquals(first, QuizExamEngine.OpenExam(progress, pool, 0, new Random(809))), "A completed exam remains completed until explicit restart.");
        Equal(beforeCompletedResume, Json(progress), "Opening a completed island must not silently restart it.");
        QuizExamEngine.OpenExam(progress, pool, 1, new Random(810));
        var otherIsland = SessionJson(progress, QuizExamEngine.SessionKey(Context, 1));

        var metrics = NonSessionJson(progress);
        var retry = QuizExamEngine.OpenExam(progress, pool, 0, new Random(811), restart: true);
        CheckFreshExam(progress, pool, 0, 20, isRetry: true);
        Check(!ReferenceEquals(first, retry), "Restart creates a separate retry session.");
        Equal(completed, Json(first), "Restart must not mutate the previous session object.");
        Sequence(first.WordKeys, retry.WordKeys, "Restart is the whole island, not an eight-word or missed-only subset");
        Equal(metrics, NonSessionJson(progress), "Restart itself must not score or review anything.");
        progress = Reload(progress);
        retry = QuizExamEngine.OpenExam(progress, pool, 0, new Random(812));
        Check(retry.IsRetry, "Retry attribution survives serialization and reopening.");
        for (var index = 0; index < 20; index++)
        {
            AnswerCurrent(progress, firstKey, true);
            if (index == 0)
            {
                var beforeDuplicate = Json(progress);
                Check(!LearningEngine.RecordQuizResponse(progress, retry, retry.WordKeys[index], false, Day), "Retry feedback must reject a second answer.");
                Equal(beforeDuplicate, Json(progress), "Duplicate retry responses do not rescore or re-credit a word.");
            }
            retry.Index++;
        }
        Equal(correctBefore + 16, progress.QuizCorrectAnswers, "Correct retries do not inflate first-attempt accuracy.");
        Equal(wrongBefore + 4, progress.QuizWrongAnswers, "Retries must not erase first-attempt mistakes.");
        Check(progress.DailyReviewedWords[Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)].SetEquals(first.WordKeys),
            "Repeating the whole island still credits exactly twenty unique words that day.");
        Equal(otherIsland, SessionJson(progress, QuizExamEngine.SessionKey(Context, 1)), "Restarting island zero leaves island one intact.");

        metrics = NonSessionJson(progress);
        retry = QuizExamEngine.OpenExam(progress, pool, 0, new Random(813), restart: true);
        Equal(metrics, NonSessionJson(progress), "A second explicit restart is also unscored.");
        Check(retry.IsRetry && retry.WordKeys.Count == 20, "Every whole-island restart retains retry attribution.");
        for (var index = 0; index < 20; index++)
        {
            AnswerCurrent(progress, firstKey, index != 0, Day.AddDays(1));
            retry.Index++;
        }
        Equal(correctBefore + 16, progress.QuizCorrectAnswers, "Next-day retry successes remain outside first-attempt accuracy.");
        Equal(wrongBefore + 4, progress.QuizWrongAnswers, "Next-day retry failures remain outside first-attempt accuracy.");
        Check(progress.DailyReviewedWords[Day.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)].SetEquals(first.WordKeys),
            "Retries earn unique-word review credit on a new day.");
        Equal(20, progress.DailyReviewedWords[Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)].Count, "Prior-day credit remains twenty, not forty.");
        Check(first.WordKeys.All(key => progress.Reviews[key].LastReviewed == Day.AddDays(1)), "Retries reschedule the actual island words.");
        Equal(3, progress.StudyStreak, "The historical day, first pass and next-day review form a three-day streak.");
        Equal(legacyCounters, Json(new { progress.CorrectAnswers, progress.WrongAnswers, progress.DailyActivity, progress.RecallRatings,
            progress.GameCorrectAnswers, progress.GameWrongAnswers, progress.CompletedGames }), "Quiz retries must not change legacy, card or game counters.");
        Equal(otherIsland, SessionJson(progress, QuizExamEngine.SessionKey(Context, 1)), "Repeated retries remain independent of other islands.");
        LearningEngine.ValidateProgress(progress);
    }

    public static void CorruptOptionsCannotResume()
    {
        var pool = Fixture();
        foreach (var legacy in new[] { false, true })
        {
            var baseline = History();
            var sessionKey = legacy ? LegacyKey : QuizExamEngine.SessionKey(Context, 0);
            var session = legacy ? SeedSaved(baseline, pool, sessionKey, pool.Take(20).Select(word => word.Key))
                : QuizExamEngine.OpenExam(baseline, pool, 0, new Random(901));
            AnswerCurrent(baseline, sessionKey, false);
            session.Index = 1;
            QuizExamEngine.OpenExam(baseline, pool, 1, new Random(902));
            Check(ReferenceEquals(session, QuizExamEngine.SavedExam(baseline, pool, 0)), "The undamaged options fixture must resume.");
            var answeredKey = OptionKey(sessionKey, 0);
            var currentKey = OptionKey(sessionKey, 1);
            var answer = session.WordKeys[1];

            var missing = Reload(baseline);
            missing.Sessions.Remove(currentKey);
            var missingBefore = Json(missing);
            Check(QuizExamEngine.SavedExam(missing, pool, 0) is null, "A missing option session cannot resume.");
            Equal(missingBefore, Json(missing), "Missing-option lookup is read-only.");
            var metrics = NonSessionJson(missing);
            var otherIsland = SessionJson(missing, QuizExamEngine.SessionKey(Context, 1));
            var incomplete = missing.Sessions[sessionKey];
            var incompleteSnapshot = SessionJson(missing, sessionKey);
            var replaced = QuizExamEngine.OpenExam(missing, pool, 0, new Random(903));
            Check(!ReferenceEquals(incomplete, replaced),
                "An incomplete options save must not be returned as the resumed session.");
            CheckFreshExam(missing, pool, 0, 20);
            if (legacy) Equal(incompleteSnapshot, SessionJson(missing, sessionKey), "Selecting a fresh island must preserve invalid legacy history.");
            Equal(metrics, NonSessionJson(missing), "Replacing missing options must not reset statistics.");
            Equal(otherIsland, SessionJson(missing, QuizExamEngine.SessionKey(Context, 1)), "Missing options in one island cannot replace another.");

            var faults = new (string Name, Action<ProgressState> Damage)[]
            {
                ("missing future question options", p => p.Sessions.Remove(OptionKey(sessionKey, 19))),
                ("empty choices", p => p.Sessions[currentKey].WordKeys.Clear()),
                ("only the answer", p => p.Sessions[currentKey].WordKeys = [answer]),
                ("correct answer missing", p => p.Sessions[currentKey].WordKeys.Remove(answer)),
                ("duplicate option key", p => p.Sessions[currentKey].WordKeys[0] = p.Sessions[currentKey].WordKeys[1]),
                ("choice missing from the dataset", p => p.Sessions[currentKey].WordKeys[0] = Word("not-in-the-fixture").Key),
                ("foreign-language choice", p => p.Sessions[currentKey].WordKeys[0] = Word("fixture-0000", "fr").Key),
                ("foreign-level choice", p => p.Sessions[currentKey].WordKeys[0] = Word("fixture-0000", level: "A2").Key),
                ("too many choices", p => p.Sessions[currentKey].WordKeys.Add(pool.First(word => !p.Sessions[currentKey].WordKeys.Contains(word.Key)).Key)),
                ("unanswered options with an advanced cursor", p => p.Sessions[currentKey].Index = 1),
                ("answered options with a reset cursor", p => p.Sessions[answeredKey].Index = 0),
                ("missing selected wrong choice", p => p.Sessions[answeredKey].Answers.Clear()),
                ("selected correctness disagrees with the exam", p => p.Sessions[answeredKey].Answers[p.Sessions[answeredKey].Answers.Single().Key] = true),
                ("answer recorded as a wrong distractor", p => p.Sessions[answeredKey].Answers = new() { [session.WordKeys[0]] = false }),
                ("multiple saved selections", p => p.Sessions[answeredKey].Answers[session.WordKeys[0]] = true),
                ("unanswered question with a saved selection", p => p.Sessions[currentKey].Answers[answer] = true),
                // With this full pool, removing only a distractor is corruption, not a small-pool exception.
                ("four choices truncated to three", p => p.Sessions[currentKey].WordKeys.Remove(p.Sessions[currentKey].WordKeys.First(key => key != answer))),
                ("four choices truncated to two", p => p.Sessions[currentKey].WordKeys = p.Sessions[currentKey].WordKeys.Where(key => key != answer).Take(1).Append(answer).ToList()),
            };
            foreach (var (name, damage) in faults)
            {
                var damaged = Reload(baseline);
                damage(damaged);
                var before = Json(damaged);
                Check(QuizExamEngine.SavedExam(damaged, pool, 0) is null, $"{sessionKey}: {name} must prevent resume.");
                Equal(before, Json(damaged), $"{sessionKey}: inspecting {name} must not repair or erase saved history.");
            }
        }
    }

    public static void InvalidSelectionIsAtomic()
    {
        var pool = Fixture();
        var progress = History();
        QuizExamEngine.OpenExam(progress, pool, 0, new Random(1001));
        AnswerCurrent(progress, QuizExamEngine.SessionKey(Context, 0), false);
        var english = SessionJson(progress, QuizExamEngine.SessionKey(Context, 0));
        var metrics = NonSessionJson(progress);
        foreach (var (language, level) in new[] { ("fr", "A1"), ("en", "A2") })
        {
            var otherPool = QuizExamEngine.CreatePool(Fixture(25, language, level).Reverse(), language, level);
            Check(QuizExamEngine.SavedExam(progress, otherPool, 0) is null, "Same island number in another context cannot resume English A1.");
            var opened = QuizExamEngine.OpenExam(progress, otherPool, 0, new Random(1002));
            CheckFreshExam(progress, otherPool, 0, 20);
            Check(ReferenceEquals(opened, QuizExamEngine.SavedExam(progress, otherPool, 0)), "Each context resumes its own named island.");
            Check(QuizExamEngine.SessionKey(Context, 0) != QuizExamEngine.SessionKey(language + ":" + level, 0), "Session keys must isolate language and level.");
            Check(QuizExamEngine.OptionsKey(Context, 0, 0) != QuizExamEngine.OptionsKey(language + ":" + level, 0, 0), "Option keys must isolate language and level.");
        }
        Equal(english, SessionJson(progress, QuizExamEngine.SessionKey(Context, 0)), "Other contexts must not change the selected English answer.");
        Equal(metrics, NonSessionJson(progress), "Opening other contexts must not change any learning statistics.");
        progress = Reload(progress);

        foreach (var index in new[] { -1, 73, int.MaxValue })
        {
            Throws<ArgumentOutOfRangeException>(() => QuizExamEngine.ExamWords(pool, index), $"Invalid slice index {index}");
            var before = Json(progress);
            Check(QuizExamEngine.SavedExam(progress, pool, index) is null, $"Invalid saved island index {index}");
            Equal(before, Json(progress), "Invalid lookup is read-only.");
            foreach (var restart in new[] { false, true })
                RejectWithoutMutation<ArgumentOutOfRangeException>(progress,
                    () => QuizExamEngine.OpenExam(progress, pool, index, new Random(1003), restart), $"Invalid index {index}, restart={restart}");
        }
        var mixedLanguage = pool.ToArray();
        mixedLanguage[^1] = mixedLanguage[^1] with { LanguageCode = "fr" };
        var mixedLevel = pool.ToArray();
        mixedLevel[^1] = mixedLevel[^1] with { Level = "A2" };
        var duplicate = pool.ToArray();
        duplicate[^1] = pool[0];
        foreach (var (name, invalidPool) in new[] { ("mixed language", mixedLanguage), ("mixed level", mixedLevel), ("duplicate key", duplicate) })
        foreach (var restart in new[] { false, true })
            RejectWithoutMutation<InvalidDataException>(progress,
                () => QuizExamEngine.OpenExam(progress, invalidPool, 0, new Random(1004), restart), name + " outside the selected island");
        RejectWithoutMutation<ArgumentOutOfRangeException>(progress,
            () => QuizExamEngine.OpenExam(progress, Array.Empty<VocabularyEntry>(), 0, new Random(1005)), "Empty pool");
        RejectWithoutMutation<InvalidDataException>(progress,
            () => QuizExamEngine.OpenExam(progress, [Word("only")], 0, new Random(1006)), "No possible distractor");
        RejectWithoutMutation<ArgumentNullException>(progress,
            () => QuizExamEngine.OpenExam(progress, pool, 0, null!), "Null random source");

        var crossContextSave = Reload(progress);
        crossContextSave.Sessions[QuizExamEngine.OptionsKey(Context, 0, 1)].WordKeys[0] = Word("fixture-0000", "fr").Key;
        RejectWithoutMutation<InvalidDataException>(crossContextSave,
            () => QuizExamEngine.OpenExam(crossContextSave, pool, 0, new Random(1007)), "Cross-context persisted option");
        Equal(english, SessionJson(progress, QuizExamEngine.SessionKey(Context, 0)), "Rejected operations preserve the original answer/options.");
        LearningEngine.ValidateProgress(progress);
    }

    private static VocabularyEntry Word(string word, string language = "en", string level = "A1") =>
        new(word, "noun", level, "fixture", "Meaning of " + word, "Example using " + word, language);

    private static VocabularyEntry[] Fixture(int count = FixtureCount, string language = "en", string level = "A1") =>
        Enumerable.Range(0, count).Select(index => Word("fixture-" + index.ToString("D4", CultureInfo.InvariantCulture), language, level)).ToArray();

    private static ProgressState History()
    {
        var historyKey = Word("historical-card").Key;
        var progress = new ProgressState
        {
            CorrectAnswers = 23, WrongAnswers = 7, QuizCorrectAnswers = 11, QuizWrongAnswers = 5,
            GameCorrectAnswers = 9, GameWrongAnswers = 2, CompletedGames = 3,
            GameBestScores = new() { ["bossrush"] = 900 }, FavoriteWords = [historyKey],
            DailyActivity = new() { [Day.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)] = 12 },
            CardPositions = new() { [Context] = historyKey },
        };
        LearningEngine.RecordCardReview(progress, historyKey, RecallRating.Good, Day.AddDays(-1));
        progress.Sessions[Context + ":cards"] = LearningEngine.CreateSession([historyKey]);
        progress.Sessions[Context + ":cards"].Answers[historyKey] = true;
        progress.Sessions[Context + ":cards"].Index = 1;
        return progress;
    }

    // Hand-authored persisted fixtures model the old schema; generation and resume are always production calls.
    private static StudySessionState SeedSaved(ProgressState progress, IReadOnlyList<VocabularyEntry> pool, string sessionKey, IEnumerable<string> keys)
    {
        var session = LearningEngine.CreateSession(keys);
        progress.Sessions[sessionKey] = session;
        for (var index = 0; index < session.WordKeys.Count; index++)
        {
            var answer = session.WordKeys[index];
            var distractors = pool.Reverse().Where(word => word.Key != answer).Take(3).Select(word => word.Key).ToArray();
            progress.Sessions[OptionKey(sessionKey, index)] = LearningEngine.CreateSession([distractors[1], answer, distractors[2], distractors[0]]);
        }
        return session;
    }

    // Like the caller, persist the selected option separately; only RecordQuizResponse awards review/score credit.
    // Leave the question cursor on feedback until a test explicitly continues it.
    private static string AnswerCurrent(ProgressState progress, string sessionKey, bool correct, DateOnly? day = null)
    {
        var session = progress.Sessions[sessionKey];
        var answer = session.WordKeys[session.Index];
        var options = progress.Sessions[OptionKey(sessionKey, session.Index)];
        var selected = correct ? answer : options.WordKeys.First(key => key != answer);
        Check(options.WordKeys.Contains(selected, StringComparer.Ordinal), "The selected answer must be one of the actual saved choices.");
        Check(LearningEngine.RecordQuizResponse(progress, session, answer, correct, day ?? Day), "The production response gate must accept the first current answer.");
        options.Answers[selected] = correct;
        options.Index = options.WordKeys.Count;
        return selected;
    }

    private static void CheckFreshExam(ProgressState progress, IReadOnlyList<VocabularyEntry> pool, int index, int count, bool isRetry = false)
    {
        var context = pool[0].LanguageCode + ":" + pool[0].Level;
        var sessionKey = QuizExamEngine.SessionKey(context, index);
        var session = progress.Sessions[sessionKey];
        Equal(count, session.WordKeys.Count, sessionKey + " question count");
        Equal(0, session.Index, sessionKey + " initial question cursor");
        Equal(0, session.Answers.Count, sessionKey + " starts unanswered");
        Equal(isRetry, session.IsRetry, sessionKey + " retry attribution");
        Sequence(pool.Skip(index * 20).Take(20).Select(word => word.Key), session.WordKeys, sessionKey + " exact word order");
        var words = pool.ToDictionary(word => word.Key, StringComparer.Ordinal);
        Equal(count, progress.Sessions.Keys.Count(key => key.StartsWith(sessionKey + "-options-", StringComparison.Ordinal)), sessionKey + " persists only its own question options");
        for (var question = 0; question < count; question++)
        {
            var optionsKey = QuizExamEngine.OptionsKey(context, index, question);
            var options = progress.Sessions[optionsKey];
            Equal(4, options.WordKeys.Count, optionsKey + " has four choices");
            Equal(4, options.WordKeys.Distinct(StringComparer.Ordinal).Count(), optionsKey + " has distinct keys");
            Check(options.WordKeys.All(words.ContainsKey), optionsKey + " uses only the selected language/level pool");
            Equal(4, options.WordKeys.Select(key => words[key].Word).Distinct(StringComparer.OrdinalIgnoreCase).Count(), optionsKey + " has distinct visible answers");
            Equal(1, options.WordKeys.Count(key => key == session.WordKeys[question]), optionsKey + " contains the correct answer exactly once");
            Equal(0, options.Index, optionsKey + " starts at its initial cursor");
            Equal(0, options.Answers.Count, optionsKey + " has no preselected answer");
        }
        Check(ReferenceEquals(session, QuizExamEngine.SavedExam(progress, pool, index)), sessionKey + " generated options must be resumable");
        LearningEngine.ValidateProgress(progress);
    }

    private static string OptionKey(string sessionKey, int question) => sessionKey + "-options-" + question.ToString(CultureInfo.InvariantCulture);
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static T Reload<T>(T value) => JsonSerializer.Deserialize<T>(Json(value))!;
    private static string SessionJson(ProgressState progress, string sessionKey) => Json(progress.Sessions
        .Where(item => item.Key == sessionKey || item.Key.StartsWith(sessionKey + "-options-", StringComparison.Ordinal))
        .OrderBy(item => item.Key, StringComparer.Ordinal).ToArray());

    private static string NonSessionJson(ProgressState progress)
    {
        var snapshot = JsonSerializer.SerializeToNode(progress)!.AsObject();
        snapshot.Remove(nameof(ProgressState.Sessions));
        return snapshot.ToJsonString();
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual, string message) =>
        Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{message}: expected {expected}, got {actual}.");

    private static void Sequence(IEnumerable<string> expected, IEnumerable<string> actual, string message) =>
        Check(expected.SequenceEqual(actual, StringComparer.Ordinal), message + ": sequences differ.");

    private static void Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException(message + ": expected " + typeof(T).Name + ".");
    }

    private static void RejectWithoutMutation<T>(ProgressState progress, Action action, string message) where T : Exception
    {
        var before = Json(progress);
        Throws<T>(action, message);
        Equal(before, Json(progress), message + " must be rejected before any mutation");
    }
}