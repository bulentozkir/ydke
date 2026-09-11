using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using YDKE_Windows;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Review intervals, repetitions, mistakes and leap dates", Sync(ReviewDates)),
    ("Same-day ratings, date limits and rejected mutation", Sync(ReviewBounds)),
    ("Due reviewed vocabulary only, ordered and deduplicated", Sync(DueWords)),
    ("Daily counts, consecutive-day streak and backwards clock", Sync(Activity)),
    ("Unicode normalization and language-scoped article stripping", Sync(Normalization)),
    ("Rack multiplicity, accents and Unicode text elements", Sync(Racks)),
    ("Strict progress/settings validation", Sync(Validation)),
    ("Legacy migration and new fields serialization", Serialization),
    ("Three backup generations and malformed-primary recovery", Recovery),
    ("Missing primary and invalid backup handling", MissingPrimary),
    ("Settings recovery and corrupt-source preservation", SettingsRecovery),
    ("Versioned export/import roundtrip is read-only", Roundtrip),
    ("Import validation rejects hostile, null and out-of-bounds payloads", InvalidImports),
    ("Oversized import is rejected without modifying storage", OversizedImport),
    ("Rejected saves/exports protect existing files", RejectedWrites),
    ("Concurrent saves/loads/exports across storage instances", Concurrency),
    ("Manual known excludes only unreviewed new vocabulary", Sync(ReviewContractTests.KnownSelection)),
    ("Finite missed-card queues survive restart and undo", Sync(ReviewContractTests.FiniteRetries)),
    ("Quiz first responses and explicit retries keep objective accuracy stable", Sync(ReviewContractTests.QuizRetries)),
    ("Recall, quiz, game and unique-word metrics stay separate", Sync(ReviewContractTests.Metrics)),
    ("Scored batch validation rejects without partial mutation", Sync(ReviewContractTests.BatchAtomicity)),
    ("New metric and settings shapes reject invalid bounds", Sync(ReviewContractTests.Validation)),
    ("Persisted language levels migrate and switch transactionally", Sync(ReviewContractTests.LanguageLevels)),
    ("New fields serialize and survive storage restart", ReviewContractTests.StorageRoundtrip),
};
var failures = 0;
foreach (var (name, run) in tests)
{
    try { await run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL {name}\n{ex}"); }
}
Console.WriteLine($"RESULT: {tests.Length - failures}/{tests.Length} passed");
return failures == 0 ? 0 : 1;

static Func<Task> Sync(Action test) => () => { test(); return Task.CompletedTask; };
static void Check(bool condition, string message = "Assertion failed")
{
    if (!condition) throw new InvalidOperationException(message);
}
static void Equal<T>(T expected, T actual) => Check(EqualityComparer<T>.Default.Equals(expected, actual), $"Expected {expected}; got {actual}");
static void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}");
}
static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); } catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}");
}
static VocabularyEntry Word(string word) => new(word, "noun", "A1", "test", "definition", "example", "en");
static string Json<T>(T value) => JsonSerializer.Serialize(value);
static ProgressState Sample(int correct = 12)
{
    var progress = new ProgressState { CorrectAnswers = correct, WrongAnswers = 3 };
    var today = new DateOnly(2024, 2, 28);
    LearningEngine.Rate(progress, "en:A1:apple", RecallRating.Good, today);
    LearningEngine.RegisterActivity(progress, today);
    progress.KnownWords.Add("en:A1:apple");
    progress.FavoriteWords.Add("fr:A1:l’école");
    progress.GameBestScores["bossrush"] = 1200;
    progress.CardPositions["en:A1"] = "en:A1:apple";
    progress.Sessions["en:A1:quiz"] = new StudySessionState
    {
        WordKeys = ["en:A1:apple", "en:A1:pear"], Index = 1,
        Answers = new() { ["en:A1:apple"] = true },
    };
    return progress;
}

static void ReviewDates()
{
    var today = new DateOnly(2024, 2, 28);
    foreach (var (rating, days) in new[] { (RecallRating.Again, 1), (RecallRating.Hard, 2), (RecallRating.Good, 3), (RecallRating.Easy, 7) })
    {
        var state = new ProgressState();
        var review = LearningEngine.Rate(state, "en:A1:apple", rating, today);
        Equal(today.AddDays(days), review.DueDate);
        Equal(days, review.IntervalDays);
        Equal(today, review.LastReviewed!.Value);
        Equal(rating == RecallRating.Again ? 0 : 1, review.Repetitions);
        Equal(rating == RecallRating.Again ? 1 : 0, review.Mistakes);
        Equal(0, state.DailyActivity.Count); // Scheduling does not double-count UI activity.
        Equal(0, state.CorrectAnswers);
    }
    var progress = new ProgressState();
    var first = LearningEngine.Rate(progress, "en:A1:apple", RecallRating.Good, today);
    var second = LearningEngine.Rate(progress, "en:A1:apple", RecallRating.Good, first.DueDate);
    Equal(6, second.IntervalDays); Equal(2, second.Repetitions);
    var third = LearningEngine.Rate(progress, "en:A1:apple", RecallRating.Hard, second.DueDate);
    Equal(8, third.IntervalDays);
    var easy = LearningEngine.Rate(progress, "en:A1:apple", RecallRating.Easy, third.DueDate);
    Equal(24, easy.IntervalDays);
    var again = LearningEngine.Rate(progress, "en:A1:apple", RecallRating.Again, easy.DueDate);
    Equal(1, again.IntervalDays); Equal(0, again.Repetitions); Equal(1, again.Mistakes);
}

static void ReviewBounds()
{
    var state = new ProgressState();
    var today = new DateOnly(2026, 9, 8);
    LearningEngine.Rate(state, "en:A1:apple", RecallRating.Good, today);
    var sameDay = LearningEngine.Rate(state, "en:A1:apple", RecallRating.Easy, today);
    Equal(3, sameDay.IntervalDays); Equal(1, sameDay.Repetitions);
    var before = Json(state);
    Throws<ArgumentOutOfRangeException>(() => LearningEngine.Rate(state, "en:A1:apple", RecallRating.Good, today.AddDays(-1)));
    Throws<ArgumentOutOfRangeException>(() => LearningEngine.Rate(state, "en:A1:apple", (RecallRating)90, today));
    Throws<InvalidDataException>(() => LearningEngine.Rate(state, "bad", RecallRating.Good, today));
    Throws<ArgumentOutOfRangeException>(() => LearningEngine.Rate(state, "en:A1:apple", RecallRating.Good, DateOnly.MaxValue));
    Equal(before, Json(state));
    var end = LearningEngine.Rate(new ProgressState(), "en:A1:apple", RecallRating.Easy, DateOnly.MaxValue.AddDays(-1));
    Equal(1, end.IntervalDays); Equal(DateOnly.MaxValue, end.DueDate);
    state.Reviews["en:A1:apple"] = new WordReview
    {
        LastReviewed = today, DueDate = today.AddDays(LearningEngine.MaxIntervalDays),
        IntervalDays = LearningEngine.MaxIntervalDays, Repetitions = LearningEngine.MaxCounter, Mistakes = LearningEngine.MaxCounter,
    };
    var capped = LearningEngine.Rate(state, "en:A1:apple", RecallRating.Easy, today.AddDays(1));
    Equal(LearningEngine.MaxIntervalDays, capped.IntervalDays); Equal(LearningEngine.MaxCounter, capped.Repetitions);
}

static void DueWords()
{
    var progress = new ProgressState();
    var today = new DateOnly(2026, 9, 8);
    LearningEngine.Rate(progress, Word("pear").Key, RecallRating.Good, today.AddDays(-3));
    LearningEngine.Rate(progress, Word("apple").Key, RecallRating.Good, today.AddDays(-4));
    LearningEngine.Rate(progress, Word("banana").Key, RecallRating.Good, today.AddDays(-3));
    LearningEngine.Rate(progress, Word("future").Key, RecallRating.Easy, today);
    progress.Reviews[Word("placeholder").Key] = new WordReview();
    var due = LearningEngine.DueWords(new[] { Word("pear"), Word("new"), Word("apple"), Word("apple"), Word("future"), Word("placeholder"), Word("banana") }, progress, today);
    Equal("apple,banana,pear", string.Join(',', due.Select(word => word.Word)));
    Equal(0, LearningEngine.DueWords(Array.Empty<VocabularyEntry>(), progress, today).Count);
}

static void Activity()
{
    var state = new ProgressState();
    var today = new DateOnly(2024, 2, 28);
    LearningEngine.RegisterActivity(state, today); LearningEngine.RegisterActivity(state, today);
    Equal(1, state.StudyStreak); Equal(2, state.DailyActivity["2024-02-28"]);
    LearningEngine.RegisterActivity(state, today.AddDays(1)); Equal(2, state.StudyStreak);
    LearningEngine.RegisterActivity(state, today.AddDays(2)); Equal(3, state.StudyStreak);
    LearningEngine.RegisterActivity(state, today.AddDays(4)); Equal(1, state.StudyStreak);
    LearningEngine.RegisterActivity(state, today); Equal(1, state.StudyStreak);
    Equal(today.AddDays(4), state.LastStudyDate!.Value); Equal(3, state.DailyActivity["2024-02-28"]);
    var minimum = new ProgressState(); LearningEngine.RegisterActivity(minimum, DateOnly.MinValue);
    Equal(1, minimum.StudyStreak); Equal(1, minimum.DailyActivity["0001-01-01"]);
    state.DailyActivity["2024-03-03"] = LearningEngine.MaxCounter;
    LearningEngine.RegisterActivity(state, today.AddDays(4)); Equal(LearningEngine.MaxCounter, state.DailyActivity["2024-03-03"]);
}

static void Normalization()
{
    Equal("", LearningEngine.NormalizeAnswer(null));
    Equal("café", LearningEngine.NormalizeAnswer("  THE\u00a0CAFE\u0301! "));
    Equal("école", LearningEngine.NormalizeAnswer("L’ÉCOLE", "fr"));
    Equal("amica", LearningEngine.NormalizeAnswer("un’amica", "it"));
    Equal("haus", LearningEngine.NormalizeAnswer("DAS\t HAUS", "de"));
    Equal("apple", LearningEngine.NormalizeAnswer("ＴＨＥ ＡＰＰＬＥ"));
    Equal("ice cream", LearningEngine.NormalizeAnswer("Ice \n\t cream"));
    Equal("co-operate", LearningEngine.NormalizeAnswer("CO–OPERATE"));
    Equal("theory", LearningEngine.NormalizeAnswer("theory"));
    Equal("the", LearningEngine.NormalizeAnswer("the"));
    Equal("die katze", LearningEngine.NormalizeAnswer("die Katze", "en"));
    Equal("café", LearningEngine.NormalizeAnswer("ca\u200bfé"));
    Check(LearningEngine.NormalizeAnswer("resume") != LearningEngine.NormalizeAnswer("résumé"));
    var culture = CultureInfo.CurrentCulture;
    try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR"); Equal("ice", LearningEngine.NormalizeAnswer("ICE")); }
    finally { CultureInfo.CurrentCulture = culture; }
}

static void Racks()
{
    Check(LearningEngine.CanBuildFromRack("apple", "p a l p e"));
    Check(!LearningEngine.CanBuildFromRack("apple", "aple"));
    Check(!LearningEngine.CanBuildFromRack("aaa", "aa"));
    Check(LearningEngine.CanBuildFromRack("café", "E\u0301FCA"));
    Check(!LearningEngine.CanBuildFromRack("café", "EF\u0301CAé")); // An accented f is not an f tile.
    Check(LearningEngine.CanBuildFromRack("éé", "e\u0301e\u0301"));
    Check(!LearningEngine.CanBuildFromRack("éé", "éee"));
    Check(!LearningEngine.CanBuildFromRack("café", "cafe"));
    Check(LearningEngine.CanBuildFromRack("𐐀𐐀", "𐐨𐐨"));
    Check(!LearningEngine.CanBuildFromRack("𐐀𐐀", "𐐨"));
    Check(LearningEngine.CanBuildFromRack("a cat", "aact")); // Do not strip the article.
    Check(!LearningEngine.CanBuildFromRack("a cat", "act"));
    Check(LearningEngine.CanBuildFromRack("re-enter", "reenter"));
    Check(!LearningEngine.CanBuildFromRack("", "abc"));
    Check(!LearningEngine.CanBuildFromRack("!?", "abc"));
    Check(!LearningEngine.CanBuildFromRack("abc", null));
}

static void Validation()
{
    LearningEngine.ValidateProgress(Sample()); LearningEngine.ValidateSettings(new UserSettings());
    Throws<InvalidDataException>(() => LearningEngine.ValidateProgress(new ProgressState { Reviews = null! }));
    Throws<InvalidDataException>(() => LearningEngine.ValidateProgress(new ProgressState { CorrectAnswers = -1 }));
    Throws<InvalidDataException>(() => LearningEngine.ValidateProgress(new ProgressState { KnownWords = ["en:A1:\u0000bad"] }));
    Throws<InvalidDataException>(() => LearningEngine.ValidateProgress(new ProgressState { KnownWords = ["zz:A1:word"] }));
    Throws<InvalidDataException>(() => LearningEngine.ValidateProgress(new ProgressState { KnownWords = ["en:A0:word"] }));
    Throws<InvalidDataException>(() => LearningEngine.ValidateProgress(new ProgressState { KnownWords = ["en:A1:\uD800"] }));
    Throws<InvalidDataException>(() => LearningEngine.ValidateProgress(new ProgressState { StudyStreak = 1 }));
    Throws<InvalidDataException>(() => LearningEngine.ValidateSettings(new UserSettings { DailyGoal = 0 }));
    Throws<InvalidDataException>(() => LearningEngine.ValidateSettings(new UserSettings { FontScale = double.NaN }));
    Throws<InvalidDataException>(() => LearningEngine.ValidateSettings(new UserSettings { BoxColor = "red" }));
    var state = Sample(); state.Sessions["bad"] = new StudySessionState();
    Throws<InvalidDataException>(() => LearningEngine.ValidateProgress(state));
}

static async Task Serialization()
{
    using var folder = new TestFolder();
    var storage = new AppStorage(folder.Path);
    Equal(20, (await storage.LoadSettingsAsync()).DailyGoal);
    Check(!Directory.EnumerateFiles(folder.Path).Any());
    await File.WriteAllTextAsync(folder.File("settings.json"), """{"UiLanguage":"tr","StudyLanguage":"en","Level":"A1","CloudConnected":true}""");
    await File.WriteAllTextAsync(folder.File("progress.json"), """{"KnownWords":["en:A1:apple"],"FavoriteWords":[],"GameBestScores":{},"CorrectAnswers":2,"WrongAnswers":1,"StudyStreak":1,"LastStudyDate":"2024-02-28"}""");
    var legacySettings = await storage.LoadSettingsAsync();
    Check(!legacySettings.CloudConnected); Equal(20, legacySettings.DailyGoal); Check(!legacySettings.ReduceMotion);
    var legacy = await storage.LoadProgressAsync();
    Equal(1, legacy.KnownWords.Count); Equal(2, legacy.CorrectAnswers); Equal(0, legacy.Reviews.Count); Equal(0, legacy.Sessions.Count);
    Check(storage.RecoveryMessage is null);
    var progress = Sample(); await storage.SaveProgressAsync(progress);
    Equal(Json(progress), Json(await storage.LoadProgressAsync()));
    var settings = new UserSettings { DailyGoal = 37, ReduceMotion = true, UntimedPractice = true, CloudConnected = true };
    await storage.SaveSettingsAsync(settings);
    Check(settings.CloudConnected); // Storage does not mutate UI-owned objects.
    var loaded = await storage.LoadSettingsAsync();
    Equal(37, loaded.DailyGoal); Check(loaded.ReduceMotion && loaded.UntimedPractice && !loaded.CloudConnected);
    Check(!File.Exists(folder.File("cloudprofile.json")));
}

static async Task Recovery()
{
    using var folder = new TestFolder();
    var storage = new AppStorage(folder.Path);
    for (var count = 1; count <= 5; count++) await storage.SaveProgressAsync(Sample(count));
    for (var index = 1; index <= 3; index++)
        Equal(5 - index, JsonSerializer.Deserialize<ProgressState>(await File.ReadAllTextAsync(folder.File($"progress.json.bak{index}")))!.CorrectAnswers);
    Equal(3, Directory.GetFiles(folder.Path, "*.bak*").Length);
    const string corrupt = "{ broken primary with irreplaceable data";
    await File.WriteAllTextAsync(folder.File("progress.json"), corrupt);
    var badBackup = JsonNode.Parse(await File.ReadAllTextAsync(folder.File("progress.json.bak1")))!;
    badBackup["CorrectAnswers"] = -4;
    await File.WriteAllTextAsync(folder.File("progress.json.bak1"), badBackup.ToJsonString());
    var recovered = await storage.LoadProgressAsync();
    Equal(3, recovered.CorrectAnswers); Check(storage.RecoveryMessage!.Contains("bak2", StringComparison.Ordinal));
    Equal(corrupt, await File.ReadAllTextAsync(folder.File("progress.json")));
    await storage.SaveProgressAsync(recovered);
    Equal(corrupt, await File.ReadAllTextAsync(Directory.GetFiles(folder.Path, "progress.json.corrupt-*").Single()));
    Equal(3, (await storage.LoadProgressAsync()).CorrectAnswers);
    await storage.SaveProgressAsync(Sample(6)); // Invalid backup is skipped and preserved, not rotated in.
    Check(Directory.GetFiles(folder.Path, "progress.json.bak1.corrupt-*").Length == 1);
    for (var index = 1; index <= 3; index++)
        LearningEngine.ValidateProgress(JsonSerializer.Deserialize<ProgressState>(await File.ReadAllTextAsync(folder.File($"progress.json.bak{index}")))!);
    Check(!Directory.GetFiles(folder.Path, "*.tmp-*").Any());
    storage.ClearRecoveryMessage(); Check(storage.RecoveryMessage is null);
}

static async Task MissingPrimary()
{
    using var folder = new TestFolder();
    var storage = new AppStorage(folder.Path);
    await storage.SaveProgressAsync(Sample(5)); await storage.SaveProgressAsync(Sample(6));
    File.Delete(folder.File("progress.json"));
    Equal(5, (await storage.LoadProgressAsync()).CorrectAnswers);
    Check(!File.Exists(folder.File("progress.json")));
    await File.WriteAllTextAsync(folder.File("progress.json.bak1"), "{}");
    Equal(0, (await storage.LoadProgressAsync()).CorrectAnswers);
    Equal("{}", await File.ReadAllTextAsync(folder.File("progress.json.bak1")));
    Check(storage.RecoveryMessage!.Contains("defaults", StringComparison.Ordinal));
}

static async Task SettingsRecovery()
{
    using var folder = new TestFolder();
    var storage = new AppStorage(folder.Path);
    await storage.SaveSettingsAsync(new UserSettings { DailyGoal = 42 });
    await storage.SaveSettingsAsync(new UserSettings { DailyGoal = 43 });
    await File.WriteAllTextAsync(folder.File("settings.json"), "null");
    Equal(42, (await storage.LoadSettingsAsync()).DailyGoal);
    Equal("null", await File.ReadAllTextAsync(folder.File("settings.json")));
    await storage.SaveSettingsAsync(new UserSettings());
    Equal("null", await File.ReadAllTextAsync(Directory.GetFiles(folder.Path, "settings.json.corrupt-*").Single()));
    // With no backup, default loading still must leave the invalid source intact.
    File.Delete(folder.File("settings.json.bak1"));
    await File.WriteAllTextAsync(folder.File("settings.json"), "{}");
    Equal(20, (await storage.LoadSettingsAsync()).DailyGoal);
    Equal("{}", await File.ReadAllTextAsync(folder.File("settings.json")));
}

static async Task Roundtrip()
{
    using var folder = new TestFolder();
    var storage = new AppStorage(folder.Path);
    var state = Sample(); var settings = new UserSettings { ReduceMotion = true, UntimedPractice = true, DailyGoal = 51 };
    LearningEngine.ChangeStudySelection(settings, settings.StudyLanguage);
    await storage.SaveProgressAsync(Sample(8)); await storage.SaveSettingsAsync(new UserSettings());
    var before = await Snapshot(folder.Path);
    var export = folder.File("backup.json"); await storage.ExportAsync(export, settings, state);
    var bytes = await File.ReadAllTextAsync(export);
    var (importedSettings, importedProgress) = await storage.ImportAsync(export);
    Equal(Json(settings), Json(importedSettings)); Equal(Json(state), Json(importedProgress));
    Equal(bytes, await File.ReadAllTextAsync(export));
    foreach (var (name, contents) in before) Equal(contents, await File.ReadAllTextAsync(folder.File(name)));
    importedProgress.CorrectAnswers = 99; Equal(12, state.CorrectAnswers);
    var legacyFlag = JsonNode.Parse(bytes)!; legacyFlag["Settings"]!["CloudConnected"] = true;
    await File.WriteAllTextAsync(export, legacyFlag.ToJsonString());
    Check(!(await storage.ImportAsync(export)).Settings.CloudConnected);
}

static async Task InvalidImports()
{
    using var folder = new TestFolder();
    var storage = new AppStorage(folder.Path);
    await storage.SaveSettingsAsync(new UserSettings()); await storage.SaveProgressAsync(Sample());
    var before = await Snapshot(folder.Path);
    var path = folder.File("input.json");
    await storage.ExportAsync(path, new UserSettings(), Sample());
    var original = await File.ReadAllTextAsync(path);
    var invalid = new List<string> { "null", "[]", "{}", "{", original + "{}" };
    void Change(Action<JsonNode> mutate)
    {
        var node = JsonNode.Parse(original)!; mutate(node); invalid.Add(node.ToJsonString());
    }
    Change(n => n["Version"] = 99); Change(n => n["Version"] = 0); Change(n => n.AsObject().Remove("Version"));
    Change(n => n["Format"] = "OtherProduct"); Change(n => n["Unexpected"] = true);
    Change(n => n["Settings"] = null); Change(n => n["Progress"] = new JsonObject());
    Change(n => n["Settings"]!["SchemaVersion"] = 99); Change(n => n["Progress"]!["SchemaVersion"] = 2);
    Change(n => n["Settings"]!["DailyGoal"] = -1); Change(n => n["Settings"]!["DailyGoal"] = 10001);
    Change(n => n["Settings"]!["FontScale"] = 100); Change(n => n["Settings"]!["BoxColor"] = "#GGGGGG");
    Change(n => n["Settings"]!["UiLanguage"] = "xx"); Change(n => n["Settings"]!["Level"] = "Z9");
    Change(n => n["Progress"]!["CorrectAnswers"] = -1); Change(n => n["Progress"]!["WrongAnswers"] = long.MaxValue);
    Change(n => n["Progress"]!["LastStudyDate"] = "2024-02-30");
    foreach (var name in new[] { "KnownWords", "FavoriteWords", "GameBestScores", "Reviews", "DailyActivity", "CardPositions", "Sessions", "RecallRatings", "DailyReviewedWords" })
        Change(n => n["Progress"]![name] = null);
    foreach (var name in new[] { "QuizCorrectAnswers", "QuizWrongAnswers", "GameCorrectAnswers", "GameWrongAnswers", "CompletedGames" })
    {
        Change(n => n["Progress"]![name] = -1);
        Change(n => n["Progress"]![name] = LearningEngine.MaxCounter + 1);
    }
    Change(n => n["Progress"]!["RecallRatings"]!["good"] = 1);
    Change(n => n["Progress"]!["RecallRatings"]!["Again"] = -1);
    Change(n => n["Progress"]!["DailyReviewedWords"]!["2026-9-08"] = new JsonArray("en:A1:apple"));
    Change(n => n["Progress"]!["DailyReviewedWords"]!["2026-09-08"] = new JsonArray("bad"));
    Change(n => n["Progress"]!["DailyReviewedWords"]!["2026-09-08"] = null);
    Change(n => n["Settings"]!["LastStudyLevels"] = null);
    Change(n => n["Settings"]!["LastStudyLevels"]!["en"] = "A0");
    Change(n => n["Settings"]!["LastStudyLevels"]!["xx"] = "A1");
    Change(n => n["Progress"]!["KnownWords"] = new JsonArray("bad"));
    Change(n => n["Progress"]!["Reviews"]!["en:A1:apple"] = null);
    Change(n => n["Progress"]!["Reviews"]!["en:A1:apple"]!["IntervalDays"] = -1);
    Change(n => n["Progress"]!["Reviews"]!["en:A1:apple"]!["DueDate"] = "2020-01-01");
    Change(n => n["Progress"]!["DailyActivity"]!["2024-2-28"] = 1);
    Change(n => n["Progress"]!["DailyActivity"]!["2024-02-28"] = 0);
    Change(n => n["Progress"]!["CardPositions"]!["en:A1"] = "fr:A1:apple");
    Change(n => n["Progress"]!["Sessions"]!["en:A1:quiz"] = null);
    Change(n => n["Progress"]!["Sessions"]!["en:A1:quiz"]!["Index"] = 3);
    Change(n => n["Progress"]!["Sessions"]!["en:A1:quiz"]!["WordKeys"] = null);
    Change(n => n["Progress"]!["Sessions"]!["en:A1:quiz"]!["Answers"] = null);
    Change(n => n["Progress"]!["Sessions"]!["en:A1:quiz"]!["WordKeys"] = new JsonArray("en:A1:apple", "en:A1:apple"));
    Change(n => n["Progress"]!["Sessions"]!["en:A1:quiz"]!["Answers"]!["en:A1:missing"] = true);
    invalid.Add(original.Replace("\"Version\": 1", "\"Version\": 1, \"Version\": 2", StringComparison.Ordinal));
    invalid.Add(original.Replace("\"CorrectAnswers\": 12", "\"CorrectAnswers\": 12, \"CorrectAnswers\": 8", StringComparison.Ordinal));
    foreach (var input in invalid)
    {
        await File.WriteAllTextAsync(path, input);
        await ThrowsAsync<InvalidDataException>(async () => { await storage.ImportAsync(path); });
        Equal(input, await File.ReadAllTextAsync(path));
    }
    foreach (var (name, contents) in before) Equal(contents, await File.ReadAllTextAsync(folder.File(name)));
    Console.WriteLine($"  Rejected {invalid.Count} invalid import payloads without changing local storage.");
}

static async Task OversizedImport()
{
    using var folder = new TestFolder();
    var storage = new AppStorage(folder.Path);
    await storage.SaveProgressAsync(Sample()); var before = await File.ReadAllTextAsync(folder.File("progress.json"));
    var path = folder.File("large.json");
    using (var file = File.Create(path)) file.SetLength((long)AppStorage.MaxFileBytes + 1);
    await ThrowsAsync<InvalidDataException>(async () => { await storage.ImportAsync(path); });
    Equal((long)AppStorage.MaxFileBytes + 1, new FileInfo(path).Length);
    Equal(before, await File.ReadAllTextAsync(folder.File("progress.json")));
}

static async Task RejectedWrites()
{
    using var folder = new TestFolder();
    var storage = new AppStorage(folder.Path);
    await storage.SaveProgressAsync(Sample()); await storage.SaveSettingsAsync(new UserSettings());
    var before = await Snapshot(folder.Path);
    await ThrowsAsync<InvalidDataException>(() => storage.SaveProgressAsync(new ProgressState { Sessions = null! }));
    await ThrowsAsync<InvalidDataException>(() => storage.SaveSettingsAsync(new UserSettings { DailyGoal = -1 }));
    await ThrowsAsync<ArgumentException>(() => storage.ExportAsync(folder.File("progress.json"), new UserSettings(), Sample()));
    await ThrowsAsync<ArgumentException>(() => storage.ExportAsync(folder.File("settings.json.bak1"), new UserSettings(), Sample()));
    foreach (var (name, contents) in before) Equal(contents, await File.ReadAllTextAsync(folder.File(name)));
    var destination = folder.File("important.json"); await File.WriteAllTextAsync(destination, "not an export; do not replace");
    await ThrowsAsync<InvalidDataException>(() => storage.ExportAsync(destination, new UserSettings(), Sample()));
    Equal("not an export; do not replace", await File.ReadAllTextAsync(destination));
    Check(!Directory.GetFiles(folder.Path, "*.tmp-*").Any());
}

static async Task Concurrency()
{
    using var folder = new TestFolder();
    var storage = new AppStorage(folder.Path); var other = new AppStorage(folder.Path);
    var shared = Sample(10);
    var pending = storage.SaveProgressAsync(shared); shared.CorrectAnswers = 999;
    await pending; Equal(10, (await storage.LoadProgressAsync()).CorrectAnswers);
    var export = folder.File("concurrent.json");
    var tasks = Enumerable.Range(1, 24).Select(async count =>
    {
        var instance = count % 2 == 0 ? storage : other;
        var state = Sample(count);
        await Task.WhenAll(instance.SaveProgressAsync(state), instance.SaveSettingsAsync(new UserSettings { DailyGoal = count }),
            instance.ExportAsync(export, new UserSettings { DailyGoal = count }, state));
        LearningEngine.ValidateProgress(await instance.LoadProgressAsync());
        LearningEngine.ValidateSettings(await instance.LoadSettingsAsync());
        var imported = await instance.ImportAsync(export);
        Equal(imported.Settings.DailyGoal, imported.Progress.CorrectAnswers); // No torn export pairs.
    });
    await Task.WhenAll(tasks);
    for (var index = 1; index <= 3; index++)
    {
        LearningEngine.ValidateProgress(JsonSerializer.Deserialize<ProgressState>(await File.ReadAllTextAsync(folder.File($"progress.json.bak{index}")))!);
        LearningEngine.ValidateSettings(JsonSerializer.Deserialize<UserSettings>(await File.ReadAllTextAsync(folder.File($"settings.json.bak{index}")))!);
        await storage.ImportAsync(folder.File($"concurrent.json.bak{index}"));
    }
    Equal(9, Directory.GetFiles(folder.Path, "*.bak*").Length);
    Check(!Directory.GetFiles(folder.Path, "*.tmp-*").Any());
    Check(!Directory.GetFiles(folder.Path, "*.corrupt-*").Any());
}

static async Task<Dictionary<string, string>> Snapshot(string folder)
{
    var result = new Dictionary<string, string>();
    foreach (var path in Directory.GetFiles(folder)) result.Add(System.IO.Path.GetFileName(path), await File.ReadAllTextAsync(path));
    return result;
}

internal sealed class TestFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YDKE.Core.Tests-" + Guid.NewGuid().ToString("N"));
    public TestFolder() => Directory.CreateDirectory(Path);
    public string File(string name) => System.IO.Path.Combine(Path, name);
    public void Dispose() => Directory.Delete(Path, recursive: true);
}