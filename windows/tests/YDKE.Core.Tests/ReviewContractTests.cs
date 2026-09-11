using System.Text.Json;
using YDKE_Windows;

// Tests invoke the SAME pure queue/response/accounting helpers used by native study UI.
internal static class ReviewContractTests
{
    private static readonly DateOnly Day = new(2026, 9, 8);
    private const string Apple = "en:A1:apple";
    private const string Pear = "en:A1:pear";
    private const string French = "fr:A1:pomme";
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static T Reload<T>(T value) => JsonSerializer.Deserialize<T>(Json(value))!;
    private static void Check(bool value, string message = "Assertion failed")
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Equal<T>(T expected, T actual) => Check(EqualityComparer<T>.Default.Equals(expected, actual), $"Expected {expected}, got {actual}");
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { return; }
        throw new InvalidOperationException("Expected rejection");
    }
    private static VocabularyEntry Word(string word) => new(word, "noun", "A1", "test", "meaning", "example", "en");

    public static void KnownSelection()
    {
        var progress = new ProgressState();
        var words = new[] { Word("manual"), Word("placeholder"), Word("due"), Word("new"), Word("future"), Word("new") };
        progress.KnownWords.UnionWith(words.Take(3).Select(word => word.Key));
        progress.Reviews[Word("placeholder").Key] = new WordReview();
        LearningEngine.Rate(progress, Word("due").Key, RecallRating.Again, Day.AddDays(-1));
        LearningEngine.Rate(progress, Word("future").Key, RecallRating.Easy, Day);
        var before = Json(progress);
        Equal("due,new", string.Join(',', LearningEngine.DueAndNew(words, progress, Day).Select(word => word.Word)));
        Equal(before, Json(progress)); // Pure selection, not automatic mastery.
        LearningEngine.RecordCardReview(progress, Word("new").Key, RecallRating.Easy, Day);
        Check(!progress.KnownWords.Contains(Word("new").Key));
        Check(progress.KnownWords.Contains(Word("due").Key));
    }

    public static void FiniteRetries()
    {
        var progress = new ProgressState();
        var session = LearningEngine.CreateSession([Apple, Pear, Apple]);
        progress.Sessions["en:A1:cards"] = session;
        var beforeRating = Json(progress); // The native undo snapshot contract.
        LearningEngine.RecordCardReview(progress, Apple, RecallRating.Again, Day);
        session.Answers[Apple] = false;
        session.Index = LearningEngine.NextUnansweredIndex(session);
        Equal(1, session.Index); Equal(2, session.WordKeys.Count);
        Equal(Day.AddDays(1), progress.Reviews[Apple].DueDate);
        var undone = JsonSerializer.Deserialize<ProgressState>(beforeRating)!;
        Equal(0, undone.Reviews.Count); Equal(0, undone.RecallRatings.Count);
        Equal(0, undone.DailyReviewedWords.Count); Equal(0, undone.Sessions["en:A1:cards"].Index);
        Equal(0, undone.StudyStreak);
        LearningEngine.RecordCardReview(progress, Pear, RecallRating.Good, Day);
        session.Answers[Pear] = true;
        session.Index = LearningEngine.NextUnansweredIndex(session);
        Equal(2, session.Index); // No automatic requeue of Apple.
        progress = Reload(progress);
        session = progress.Sessions["en:A1:cards"];
        Equal(Apple, LearningEngine.MissedKeys(session).Single());
        var retry = LearningEngine.CreateSession(LearningEngine.MissedKeys(session), isRetry: true);
        progress.Sessions["en:A1:cards"] = retry;
        progress = Reload(progress);
        retry = progress.Sessions["en:A1:cards"];
        Check(retry.IsRetry); Equal(1, retry.WordKeys.Count); Equal(0, retry.Index);
        LearningEngine.RecordCardReview(progress, Apple, RecallRating.Again, Day);
        retry.Answers[Apple] = false;
        retry.Index = LearningEngine.NextUnansweredIndex(retry);
        Equal(1, retry.Index); Equal(1, retry.WordKeys.Count); // Even another miss ends this pass.
        Equal(2, progress.DailyReviewedWords["2026-09-08"].Count);
        Equal(2, progress.RecallRatings["Again"]);
        Equal(0, progress.QuizCorrectAnswers); Equal(0, progress.QuizWrongAnswers);
        Equal(Day.AddDays(1), progress.Reviews[Apple].DueDate);
        Equal(0, LearningEngine.MissedKeys(LearningEngine.CreateSession([])).Count);
        var partial = LearningEngine.CreateSession([Apple, Pear]); partial.Answers[Pear] = false;
        Equal(Pear, LearningEngine.MissedKeys(partial).Single()); // Unanswered is not a miss.
        Equal(0, LearningEngine.NextUnansweredIndex(partial));
        Reject(() => LearningEngine.CreateSession([Apple, French]));
    }

    public static void QuizRetries()
    {
        var progress = new ProgressState();
        var quiz = LearningEngine.CreateSession([Apple, Pear]);
        progress.Sessions["en:A1:quiz"] = quiz;
        Check(LearningEngine.RecordQuizResponse(progress, quiz, Apple, false, Day));
        var before = Json(progress);
        Check(!LearningEngine.RecordQuizResponse(progress, quiz, Apple, true, Day));
        Equal(before, Json(progress));
        quiz.Index++;
        Check(LearningEngine.RecordQuizResponse(progress, quiz, Pear, true, Day)); quiz.Index++;
        Equal(1, progress.QuizCorrectAnswers); Equal(1, progress.QuizWrongAnswers);
        progress = Reload(progress); quiz = progress.Sessions["en:A1:quiz"];
        var retry = LearningEngine.CreateSession(LearningEngine.MissedKeys(quiz), true);
        progress.Sessions["en:A1:quiz"] = retry;
        progress = Reload(progress); retry = progress.Sessions["en:A1:quiz"];
        Check(LearningEngine.RecordQuizResponse(progress, retry, Apple, true, Day)); retry.Index++;
        Equal(1, progress.QuizCorrectAnswers); Equal(1, progress.QuizWrongAnswers);
        Equal(2, progress.DailyReviewedWords["2026-09-08"].Count);
        Equal(0, progress.RecallRatings.Count); Equal(0, LearningEngine.MissedKeys(retry).Count);
        Check(!LearningEngine.RecordQuizResponse(progress, retry, Apple, false, Day));
    }

    public static void Metrics()
    {
        var progress = new ProgressState { CorrectAnswers = 12, WrongAnswers = 8, DailyActivity = new() { ["2026-09-07"] = 10 } };
        LearningEngine.RecordCardReview(progress, Apple, RecallRating.Easy, Day);
        LearningEngine.RecordCardReview(progress, Apple, RecallRating.Again, Day);
        LearningEngine.RecordScoredAnswer(progress, [Apple, Pear, Apple, French], true, true, Day);
        LearningEngine.RecordScoredAnswer(progress, [Apple], false, false, Day);
        LearningEngine.RecordScoredAnswer(progress, [], false, true, Day);
        LearningEngine.RegisterGameCompleted(progress);
        Equal(1, progress.RecallRatings["Easy"]); Equal(1, progress.RecallRatings["Again"]);
        Equal(1, progress.QuizWrongAnswers); Equal(0, progress.QuizCorrectAnswers);
        Equal(1, progress.GameCorrectAnswers); Equal(1, progress.GameWrongAnswers); Equal(1, progress.CompletedGames);
        Equal(3, progress.DailyReviewedWords["2026-09-08"].Count); Equal(3, progress.Reviews.Count);
        Equal(1, progress.StudyStreak); Equal(12, progress.CorrectAnswers); Equal(8, progress.WrongAnswers);
        Equal(1, progress.DailyActivity.Count); Equal(10, progress.DailyActivity["2026-09-07"]);
        LearningEngine.RecordScoredAnswer(progress, [], true, true, Day.AddDays(1));
        Equal(2, progress.StudyStreak); Equal(1, progress.DailyReviewedWords.Count);
        LearningEngine.RecordScoredAnswer(progress, [], true, false, Day.AddDays(-1));
        Equal(Day.AddDays(1), progress.LastStudyDate!.Value); Equal(2, progress.StudyStreak);
        LearningEngine.RecordCardReview(progress, Apple, RecallRating.Good, Day.AddDays(1));
        Equal(1, progress.DailyReviewedWords["2026-09-09"].Count); Equal(2, progress.StudyStreak);
        var readingOnly = new ProgressState();
        LearningEngine.RecordScoredAnswer(readingOnly, [], true, true, Day);
        Equal(1, readingOnly.StudyStreak); Equal(0, readingOnly.Reviews.Count);
        Equal(0, readingOnly.DailyReviewedWords.Count); Equal(0, readingOnly.DailyActivity.Count);
        progress.GameCorrectAnswers = LearningEngine.MaxCounter;
        progress.CompletedGames = LearningEngine.MaxCounter;
        progress.RecallRatings["Easy"] = LearningEngine.MaxCounter;
        LearningEngine.RecordScoredAnswer(progress, [], true, true, Day.AddDays(1));
        LearningEngine.RegisterGameCompleted(progress);
        LearningEngine.RecordCardReview(progress, Pear, RecallRating.Easy, Day.AddDays(1));
        Equal(LearningEngine.MaxCounter, progress.GameCorrectAnswers);
        Equal(LearningEngine.MaxCounter, progress.CompletedGames); Equal(LearningEngine.MaxCounter, progress.RecallRatings["Easy"]);
    }

    public static void BatchAtomicity()
    {
        var progress = new ProgressState();
        LearningEngine.RecordCardReview(progress, Pear, RecallRating.Good, Day.AddDays(1));
        var before = Json(progress);
        Reject(() => LearningEngine.RecordScoredAnswer(progress, [Apple, "bad"], true, true, Day));
        Equal(before, Json(progress));
        Reject(() => LearningEngine.RecordScoredAnswer(progress, [Apple, Pear], true, false, Day));
        Equal(before, Json(progress)); // Later key's future review must not partially schedule Apple.
        Reject(() => LearningEngine.RecordScoredAnswer(progress, [Apple], false, true, DateOnly.MaxValue));
        Reject(() => LearningEngine.RecordCardReview(progress, Apple, (RecallRating)99, Day));
        Reject(() => LearningEngine.RecordScoredAnswer(progress, null!, true, true, Day));
        Equal(before, Json(progress));
        static IEnumerable<string> BrokenInput() { yield return Apple; throw new ArgumentException("lazy input failed"); }
        Reject(() => LearningEngine.RecordScoredAnswer(progress, BrokenInput(), true, true, Day));
        Equal(before, Json(progress));
        var full = new ProgressState();
        full.DailyReviewedWords = Enumerable.Range(0, LearningEngine.MaxCollectionCount)
            .ToDictionary(index => DateOnly.MinValue.AddDays(index).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), _ => new HashSet<string>());
        Reject(() => LearningEngine.RecordCardReview(full, Apple, RecallRating.Good, Day));
        Equal(0, full.Reviews.Count); Equal(0, full.RecallRatings.Count); Equal(0, full.StudyStreak);
    }

    public static void Validation()
    {
        foreach (var name in new[] { "QuizCorrectAnswers", "QuizWrongAnswers", "GameCorrectAnswers", "GameWrongAnswers", "CompletedGames" })
        foreach (var value in new[] { -1, LearningEngine.MaxCounter + 1 })
        {
            var progress = new ProgressState(); typeof(ProgressState).GetProperty(name)!.SetValue(progress, value);
            Reject(() => LearningEngine.ValidateProgress(progress));
        }
        Reject(() => LearningEngine.ValidateProgress(new ProgressState { RecallRatings = null! }));
        Reject(() => LearningEngine.ValidateProgress(new ProgressState { DailyReviewedWords = null! }));
        foreach (var name in new[] { "good", "0", "Unknown" })
            Reject(() => LearningEngine.ValidateProgress(new ProgressState { RecallRatings = new() { [name] = 1 } }));
        foreach (var value in new[] { -1, LearningEngine.MaxCounter + 1 })
            Reject(() => LearningEngine.ValidateProgress(new ProgressState { RecallRatings = new() { ["Good"] = value } }));
        Reject(() => LearningEngine.ValidateProgress(new ProgressState { DailyReviewedWords = new() { ["2026-9-08"] = [] } }));
        Reject(() => LearningEngine.ValidateProgress(new ProgressState { DailyReviewedWords = new() { ["2026-09-08"] = null! } }));
        Reject(() => LearningEngine.ValidateProgress(new ProgressState { DailyReviewedWords = new() { ["2026-09-08"] = ["bad"] } }));
        Reject(() => LearningEngine.ValidateSettings(new UserSettings { LastStudyLevels = null! }));
        Reject(() => LearningEngine.ValidateSettings(new UserSettings { LastStudyLevels = new() { ["tr"] = "A1" } }));
        Reject(() => LearningEngine.ValidateSettings(new UserSettings { LastStudyLevels = new() { ["en"] = "B9" } }));
        Reject(() => JsonSerializer.Deserialize<UserSettings>("""{"LastStudyLevels":{"en":"B9"}}"""));
        Reject(() => JsonSerializer.Deserialize<UserSettings>("""{"LastStudyLevels":null}"""));
        var legacy = JsonSerializer.Deserialize<ProgressState>("""{"CorrectAnswers":12,"WrongAnswers":7}""")!;
        LearningEngine.ValidateProgress(legacy);
        Equal(12, legacy.CorrectAnswers); Equal(0, legacy.QuizCorrectAnswers); Equal(0, legacy.GameCorrectAnswers);
        Equal(0, legacy.CompletedGames); Equal(0, legacy.RecallRatings.Count); Equal(0, legacy.DailyReviewedWords.Count);
    }

    public static void LanguageLevels()
    {
        var settings = JsonSerializer.Deserialize<UserSettings>("""{"StudyLanguage":"en","Level":"C1","LastStudyLevels":{"en":"A1","fr":"B2"}}""")!;
        Equal("C1", settings.LastStudyLevels["en"]); // Current Level overrides stale remembered current level.
        LearningEngine.ChangeStudySelection(settings, "fr"); Equal("B2", settings.Level);
        settings = Reload(settings);
        LearningEngine.ChangeStudySelection(settings, "en"); Equal("C1", settings.Level);
        LearningEngine.ChangeStudySelection(settings, "en", "C2"); Equal("C2", settings.LastStudyLevels["en"]);
        LearningEngine.ChangeStudySelection(settings, "nl"); Equal("A1", settings.Level);
        settings = Reload(settings); LearningEngine.ChangeStudySelection(settings, "en"); Equal("C2", settings.Level);
        var before = Json(settings);
        Reject(() => LearningEngine.ChangeStudySelection(settings, "zz")); Equal(before, Json(settings));
        Reject(() => LearningEngine.ChangeStudySelection(settings, "fr", "A0")); Equal(before, Json(settings));
        LearningEngine.ChangeStudySelection(settings, "fr", "C2");
        settings = JsonSerializer.Deserialize<UserSettings>(before)!; // UI failed-save rollback snapshot.
        Equal("en", settings.StudyLanguage); Equal("C2", settings.Level); Equal("B2", settings.LastStudyLevels["fr"]);
        var legacy = JsonSerializer.Deserialize<UserSettings>("""{"StudyLanguage":"de","Level":"B1"}""")!;
        Equal("B1", legacy.LastStudyLevels["de"]); LearningEngine.ChangeStudySelection(legacy, "it"); Equal("A1", legacy.Level);
    }

    public static async Task StorageRoundtrip()
    {
        using var folder = new TestFolder();
        var storage = new AppStorage(folder.Path);
        var progress = new ProgressState();
        LearningEngine.RecordCardReview(progress, Apple, RecallRating.Again, Day);
        LearningEngine.RecordScoredAnswer(progress, [Apple, French], true, true, Day);
        LearningEngine.RecordScoredAnswer(progress, [Pear], false, false, Day);
        LearningEngine.RegisterGameCompleted(progress);
        progress.Sessions["en:A1:cards"] = LearningEngine.CreateSession([Apple], true);
        progress.KnownWords.Add(Pear);
        var settings = new UserSettings();
        LearningEngine.ChangeStudySelection(settings, "en", "C2"); LearningEngine.ChangeStudySelection(settings, "de", "B2");
        await storage.SaveSettingsAsync(settings); await storage.SaveProgressAsync(progress);
        var restarted = new AppStorage(folder.Path);
        Equal(Json(progress), Json(await restarted.LoadProgressAsync()));
        var loaded = await restarted.LoadSettingsAsync(); Equal(Json(settings), Json(loaded));
        LearningEngine.ChangeStudySelection(loaded, "en"); Equal("C2", loaded.Level);
        var export = folder.File("metrics-backup.json"); await storage.ExportAsync(export, settings, progress);
        var imported = await storage.ImportAsync(export);
        Equal(Json(progress), Json(imported.Progress)); Equal(Json(settings), Json(imported.Settings));
    }
}