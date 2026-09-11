using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YDKE_Windows;

internal static class Program
{
    private const string Journal = AppStorage.ImportJournalFileName;
    private static readonly string[] Checkpoints = ["after-journal", "after-progress", "after-settings", "before-delete"];
    private static readonly DateOnly Today = new(2026, 9, 8);

    public static async Task<int> Main()
    {
        var tests = new List<(string Name, Func<Task> Run)>
        {
            ("FolderPath and empty legacy loads do not create storage", EmptyStorage),
            ("Successful import commits the full new models without mutating callers", SuccessfulImport),
            ("Apply snapshots both targets before its first await/checkpoint", SnapshotIsolation),
            ("Invalid apply validates BOTH targets before ANY filesystem change", InvalidApply),
            ("Pre-commit I/O failure cannot touch state or masquerade as committed", PreCommitFailure),
            ("Import preview stays read-only even with a pending transaction", ReadOnlyPreview),
            ("Malformed journals block all state operations and preserve every byte", InvalidJournals),
            ("Oversized journal fails closed without reading state", OversizedJournal),
            ("Oversized input preview stays read-only", OversizedPreview),
            ("Journal directory is a blocker, not an absent file", DirectoryJournal),
            ("Unreadable journal blocks state and unrelated writes; retry succeeds", LockedJournal),
            ("Recovery must finish SETTINGS before returning a PROGRESS load", LockedSettingsRecovery),
            ("Journal deletion I/O failure retains commit and backups", LockedJournalDeletion),
            ("Deleting an observed pending journal does not unblock a live process", MissingObservedJournal),
            ("BOM-prefixed full envelope is a valid recovery record", BomJournal),
            ("Journal sidecars alone are uncommitted and never silently promoted", OrphanSidecars),
            ("Export protects journal, backups, temporary files and Windows aliases", ManagedPaths),
            ("Import preserves valid backups and quarantines corrupt evidence", CorruptSources),
            ("Successful import invalidates other live storage instances", OtherInstanceInvalidation),
            ("Queued pre-import saves remain stale after the applying instance commits", QueuedStaleSaves),
            ("Load acknowledgement cannot combine reads from two import generations", SplitGenerationLoads),
            ("Concurrent pair loads wait for complete roll-forward without deadlock", ConcurrentLoads),
            ("Legacy schemas, normal saves, backup fallback and quarantine remain compatible", LegacyCompatibility),
        };
        foreach (var checkpoint in Checkpoints)
        {
            foreach (var firstLoad in new[] { "settings", "progress", "pair" })
                tests.Add(($"Crash {checkpoint}: restart via {firstLoad} never returns a mixed pair",
                    () => CrashBoundary(checkpoint, firstLoad)));
            tests.Add(($"Repeated recovery interrupted at {checkpoint} preserves history", () => RepeatedRecovery(checkpoint)));
        }
        foreach (var write in new[] { "settings", "progress", "cloudprofile", "export", "apply" })
            tests.Add(($"Pending import recovers then rejects stale {write}, including retries", () => StaleWrite(write)));

        var failures = 0;
        foreach (var (name, run) in tests)
        {
            try
            {
                // This is a deadlock guard, not a timing assertion or a filesystem delay.
                await run().WaitAsync(TimeSpan.FromSeconds(30));
                Console.WriteLine($"PASS {name}");
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLine($"FAIL {name}: possible unreleased IoGate\n{ex}");
                return 2;
            }
            catch (Exception ex)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {name}\n{ex}");
            }
        }
        Console.WriteLine($"RESULT: {tests.Count - failures}/{tests.Count} passed");
        return failures == 0 ? 0 : 1;
    }

    private static UserSettings Settings(int marker = 77, bool imported = true) => new()
    {
        UiLanguage = imported ? "de" : "tr",
        StudyLanguage = imported ? "fr" : "en",
        Level = imported ? "B2" : "A1",
        LastStudyLevels = imported ? new() { ["en"] = "C1", ["fr"] = "B2" } : new() { ["en"] = "A1" },
        DailyGoal = marker,
        FontScale = imported ? 1.2 : 1,
        AppBackgroundColor = imported ? "#102030" : AppearancePalette.DefaultBackground,
        ButtonColor = imported ? "#345678" : AppearancePalette.DefaultButton,
        BoxColor = imported ? "#203040" : AppearancePalette.DefaultBox,
        ReduceMotion = imported,
        UntimedPractice = imported,
    };

    private static ProgressState Progress(int marker = 77)
    {
        var state = new ProgressState
        {
            CorrectAnswers = marker,
            WrongAnswers = 2,
            QuizCorrectAnswers = marker,
            QuizWrongAnswers = 3,
            GameCorrectAnswers = marker + 1,
            GameWrongAnswers = 4,
            CompletedGames = marker + 2,
            KnownWords = ["en:A1:apple", "fr:B2:l’école"],
            FavoriteWords = ["fr:B2:café"],
            GameBestScores = new() { ["hangman"] = marker },
            DailyActivity = new() { ["2024-02-29"] = 1 },
            CardPositions = new() { ["fr:B2"] = "fr:B2:l’école" },
            Sessions = new()
            {
                ["fr:B2:quiz"] = new()
                {
                    IsRetry = true, WordKeys = ["fr:B2:l’école", "fr:B2:café"], Index = 1,
                    Answers = new() { ["fr:B2:l’école"] = false },
                },
            },
        };
        LearningEngine.RecordCardReview(state, "fr:B2:l’école", RecallRating.Good, Today);
        LearningEngine.RecordScoredAnswer(state, ["en:A1:apple"], false, false, Today);
        return state;
    }

    private static async Task<AppStorage> Seed(TestFolder folder)
    {
        var storage = new AppStorage(folder.Path);
        for (var generation = 1; generation <= 4; generation++)
        {
            await storage.SaveSettingsAsync(Settings(100 + generation, imported: false));
            await storage.SaveProgressAsync(Progress(generation));
        }
        return storage;
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static string Envelope(UserSettings? settings = null, ProgressState? progress = null) =>
        Json(new { Format = "YDKE.Backup", Version = AppStorage.ExportVersion, Settings = settings ?? Settings(), Progress = progress ?? Progress() });
    private static UserSettings Normalized(UserSettings settings)
    {
        var copy = JsonSerializer.Deserialize<UserSettings>(Json(settings))!;
        copy.CloudConnected = false;
        return copy;
    }

    private static void Check(bool condition, string message = "Assertion failed")
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual) =>
        Check(EqualityComparer<T>.Default.Equals(expected, actual), $"Expected {expected}; got {actual}");

    private static async Task<T> Throws<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T ex) { return ex; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}");
    }

    private static T Read<T>(TestFolder folder, string name) => JsonSerializer.Deserialize<T>(File.ReadAllText(folder.File(name)))!;
    private static void AssertPair((UserSettings Settings, ProgressState Progress) pair, UserSettings? settings = null, ProgressState? progress = null)
    {
        Equal(Json(Normalized(settings ?? Settings())), Json(pair.Settings));
        Equal(Json(progress ?? Progress()), Json(pair.Progress));
    }
    private static void AssertDiskPair(TestFolder folder, UserSettings? settings = null, ProgressState? progress = null) =>
        AssertPair((Read<UserSettings>(folder, "settings.json"), Read<ProgressState>(folder, "progress.json")), settings, progress);

    private static void AssertBackups(TestFolder folder)
    {
        for (var index = 1; index <= 3; index++)
        {
            Equal(105 - index, Read<UserSettings>(folder, $"settings.json.bak{index}").DailyGoal);
            Equal(5 - index, Read<ProgressState>(folder, $"progress.json.bak{index}").CorrectAnswers);
        }
    }

    private static Dictionary<string, string> Snapshot(TestFolder folder)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var directory in Directory.EnumerateDirectories(folder.Path, "*", SearchOption.AllDirectories))
            result[System.IO.Path.GetRelativePath(folder.Path, directory) + "/"] = "directory";
        foreach (var file in Directory.EnumerateFiles(folder.Path, "*", SearchOption.AllDirectories))
        {
            using var stream = File.OpenRead(file);
            result[System.IO.Path.GetRelativePath(folder.Path, file)] =
                $"{stream.Length}:{Convert.ToHexString(SHA256.HashData(stream))}:{File.GetLastWriteTimeUtc(file).Ticks}";
        }
        return result;
    }

    private static void Unchanged(Dictionary<string, string> expected, TestFolder folder)
    {
        var actual = Snapshot(folder);
        Equal(expected.Count, actual.Count);
        foreach (var (path, value) in expected)
            Check(actual.TryGetValue(path, out var current) && value == current, $"Unexpected filesystem change: {path}");
    }

    private static async Task<AppStorage> Crash(TestFolder folder, string checkpoint, UserSettings? settings = null, ProgressState? progress = null)
    {
        var seen = new List<string>();
        var storage = new AppStorage(folder.Path, point =>
        {
            seen.Add(point);
            if (point == checkpoint) throw new SimulatedCrashException(point);
        });
        var failure = await Throws<PendingImportException>(() => storage.ApplyImportAsync(settings ?? Settings(), progress ?? Progress()));
        Check(failure.InnerException is SimulatedCrashException);
        Check(!failure.IsInvalidJournal);
        Equal(folder.File(Journal), failure.JournalPath);
        Equal(folder.Path, failure.FolderPath);
        Check(failure.Message.Contains("roll FORWARD", StringComparison.Ordinal));
        Equal(checkpoint, seen[^1]);
        Check(storage.HasPendingImport && storage.RequiresReload);
        Check(File.Exists(folder.File(Journal)));
        Check(!Directory.EnumerateFiles(folder.Path, "*.tmp-*").Any(), "Live fault injection must release temporary files.");
        return storage;
    }

    private static async Task EmptyStorage()
    {
        using var folder = new TestFolder();
        var path = folder.File("empty") + System.IO.Path.DirectorySeparatorChar;
        var storage = new AppStorage(path);
        Equal(System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path)), storage.FolderPath);
        Equal(20, (await storage.LoadSettingsAsync()).DailyGoal);
        Equal(0, (await storage.LoadProgressAsync()).CorrectAnswers);
        await storage.LoadStateAsync();
        Check(!storage.HasPendingImport && !storage.RequiresReload);
        Check(!Directory.Exists(path));
        Equal(0, Snapshot(folder).Count);
    }

    private static async Task SuccessfulImport()
    {
        using var folder = new TestFolder();
        var storage = await Seed(folder);
        var settings = Settings();
        settings.CloudConnected = true;
        settings.LastStudyLevels["fr"] = "A1"; // Current Level wins, without mutating the caller.
        var progress = Progress();
        var settingsBefore = Json(settings);
        var progressBefore = Json(progress);
        await storage.ApplyImportAsync(settings, progress);
        Equal(settingsBefore, Json(settings));
        Equal(progressBefore, Json(progress));
        AssertDiskPair(folder, settings, progress);
        AssertBackups(folder);
        Check(!storage.HasPendingImport && !storage.RequiresReload);
        Check(!File.Exists(folder.File(Journal)));
        Check(!File.Exists(folder.File("cloudprofile.json")));
        var before = Snapshot(folder);
        AssertPair(await new AppStorage(folder.Path).LoadStateAsync(), settings, progress);
        Unchanged(before, folder);
    }

    private static async Task SnapshotIsolation()
    {
        using var folder = new TestFolder();
        await Seed(folder);
        var settings = Settings();
        var progress = Progress();
        var originalSettings = Normalized(settings);
        var originalProgress = JsonSerializer.Deserialize<ProgressState>(Json(progress))!;
        var storage = new AppStorage(folder.Path, checkpoint =>
        {
            if (checkpoint != "after-journal") return;
            // Deliberately mutate deeply after the synchronous method-entry snapshot.
            settings.DailyGoal = 999;
            settings.LastStudyLevels["en"] = "A2";
            progress.CorrectAnswers = 999;
            progress.Sessions["fr:B2:quiz"].WordKeys.Clear();
            var persisted = JsonNode.Parse(File.ReadAllText(folder.File(Journal)))!;
            Equal(77, persisted["Settings"]!["DailyGoal"]!.GetValue<int>());
            Equal(77, persisted["Progress"]!["CorrectAnswers"]!.GetValue<int>());
        });
        await storage.ApplyImportAsync(settings, progress);
        AssertDiskPair(folder, originalSettings, originalProgress);
        AssertBackups(folder);
    }

    private static async Task InvalidApply()
    {
        using var folder = new TestFolder();
        var storage = await Seed(folder);
        var before = Snapshot(folder);
        var mutations = new Action<UserSettings, ProgressState>[]
        {
            (s, _) => s.DailyGoal = 0,
            (s, _) => s.LastStudyLevels = null!,
            (s, _) => s.LastStudyLevels["xx"] = "A1",
            (s, _) => s.SchemaVersion = 99,
            (s, _) => s.FontScale = double.NaN,
            (_, p) => p.CorrectAnswers = -1,
            (_, p) => p.Sessions["fr:B2:quiz"].Index = 99,
            (_, p) => p.RecallRatings["good"] = 1,
            (_, p) => p.DailyReviewedWords["2026-09-08"] = null!,
            (_, p) => p.SchemaVersion = 2,
        };
        foreach (var mutation in mutations)
        {
            var settings = Settings(); var progress = Progress();
            mutation(settings, progress);
            await Throws<InvalidDataException>(() => storage.ApplyImportAsync(settings, progress));
            Unchanged(before, folder);
        }
        await Throws<ArgumentNullException>(() => storage.ApplyImportAsync(null!, Progress()));
        await Throws<ArgumentNullException>(() => storage.ApplyImportAsync(Settings(), null!));
        Unchanged(before, folder);
        var missing = new AppStorage(folder.File("must-not-exist"));
        await Throws<InvalidDataException>(() => missing.ApplyImportAsync(Settings(), new ProgressState { CorrectAnswers = -1 }));
        Check(!Directory.Exists(missing.FolderPath));
        // Even with a pending import, invalid apply arguments are rejected before I/O.
        await Crash(folder, "after-journal");
        before = Snapshot(folder);
        await Throws<InvalidDataException>(() => storage.ApplyImportAsync(new UserSettings { DailyGoal = -1 }, Progress()));
        Unchanged(before, folder);
    }

    private static async Task PreCommitFailure()
    {
        using var folder = new TestFolder();
        File.WriteAllText(folder.File("not-a-directory"), "Preserve this unrelated file.");
        var before = Snapshot(folder);
        var storage = new AppStorage(folder.File("not-a-directory"));
        var error = await Throws<IOException>(() => storage.ApplyImportAsync(Settings(), Progress()));
        Check(error is not ImportReloadRequiredException);
        Check(!storage.HasPendingImport && !storage.RequiresReload);
        Unchanged(before, folder);
    }

    private static async Task ReadOnlyPreview()
    {
        using var folder = new TestFolder();
        var storage = await Seed(folder);
        var input = folder.File("input.json");
        await storage.ExportAsync(input, Settings(), Progress());
        File.WriteAllText(folder.File("invalid-input.json"), "{}");
        var imported = await storage.ImportAsync(input);
        await Crash(folder, "after-progress", imported.Settings, imported.Progress);
        var before = Snapshot(folder);
        AssertPair(await storage.ImportAsync(input));
        AssertPair(await storage.ImportAsync(folder.File(Journal)));
        await Throws<InvalidDataException>(() => storage.ImportAsync(folder.File("invalid-input.json")));
        Unchanged(before, folder);
        Check(storage.HasPendingImport);
        File.Delete(input); // Recovery must not depend on the original input's lifetime.
        AssertPair(await new AppStorage(folder.Path).LoadStateAsync());
    }

    private static async Task CrashBoundary(string checkpoint, string firstLoad)
    {
        using var folder = new TestFolder();
        await Seed(folder);
        var crashed = await Crash(folder, checkpoint);
        var rawJournal = File.ReadAllBytes(folder.File(Journal));
        using (var parsed = JsonDocument.Parse(rawJournal))
        {
            Equal("YDKE.Backup", parsed.RootElement.GetProperty("Format").GetString());
            Equal(AppStorage.ExportVersion, parsed.RootElement.GetProperty("Version").GetInt32());
        }
        AssertPair(await crashed.ImportAsync(folder.File(Journal)));
        // Prove the dangerous boundary really was exercised, bypassing storage recovery.
        Equal(checkpoint == "after-journal" ? 4 : 77, Read<ProgressState>(folder, "progress.json").CorrectAnswers);
        Equal(checkpoint is "after-journal" or "after-progress" ? 104 : 77, Read<UserSettings>(folder, "settings.json").DailyGoal);

        var restart = new AppStorage(folder.Path);
        if (firstLoad == "pair") AssertPair(await restart.LoadStateAsync());
        else
        {
            if (firstLoad == "settings") Equal(Json(Settings()), Json(await restart.LoadSettingsAsync()));
            else Equal(Json(Progress()), Json(await restart.LoadProgressAsync()));
            AssertDiskPair(folder); // BOTH must be new before the FIRST load returns.
            Check(restart.RequiresReload, "One individual load must not acknowledge a pair reload.");
            if (firstLoad == "settings")
            {
                await restart.LoadSettingsAsync();
                Check(restart.RequiresReload, "Repeated settings loads are not a progress load.");
                Equal(Json(Progress()), Json(await restart.LoadProgressAsync()));
            }
            else
            {
                await restart.LoadProgressAsync();
                Check(restart.RequiresReload);
                Equal(Json(Settings()), Json(await restart.LoadSettingsAsync()));
            }
        }
        AssertDiskPair(folder);
        AssertBackups(folder);
        Check(!restart.HasPendingImport && !restart.RequiresReload);
        Check(crashed.RequiresReload, "Recovery by another instance cannot authorize old UI memory.");
        Check(restart.RecoveryMessage?.Contains("Completed interrupted import", StringComparison.Ordinal) == true);
        Check(!File.Exists(folder.File(Journal)));
        var stable = Snapshot(folder);
        for (var iteration = 0; iteration < 3; iteration++) AssertPair(await new AppStorage(folder.Path).LoadStateAsync());
        Unchanged(stable, folder); // Includes backup bytes, names and modification times.
    }

    private static async Task RepeatedRecovery(string checkpoint)
    {
        using var folder = new TestFolder();
        await Seed(folder);
        await Crash(folder, "after-journal");
        var journal = File.ReadAllBytes(folder.File(Journal));
        Dictionary<string, string>? stable = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var retry = new AppStorage(folder.Path, point =>
            {
                if (point == checkpoint) throw new SimulatedCrashException(point);
            });
            await Throws<PendingImportException>(() => attempt % 2 == 0 ? retry.LoadSettingsAsync() : (Task)retry.LoadProgressAsync());
            Check(journal.AsSpan().SequenceEqual(File.ReadAllBytes(folder.File(Journal))));
            Check(retry.HasPendingImport && retry.RequiresReload);
            if (stable is not null) Unchanged(stable, folder);
            stable = Snapshot(folder);
        }
        AssertPair(await new AppStorage(folder.Path).LoadStateAsync());
        AssertBackups(folder);
    }

    private static async Task InvalidJournals()
    {
        var good = Envelope();
        var invalid = new List<string> { "{", "null", "[]", "{}", good + "{}" };
        void Change(Action<JsonNode> change)
        {
            var node = JsonNode.Parse(good)!;
            change(node);
            invalid.Add(node.ToJsonString());
        }
        Change(n => n["Format"] = "OtherApp");
        Change(n => n["Version"] = 2);
        Change(n => n.AsObject().Remove("Settings"));
        Change(n => n.AsObject().Remove("Progress"));
        Change(n => n["Settings"] = new JsonObject());
        Change(n => n["Progress"] = new JsonObject());
        Change(n => n["Settings"] = null);
        Change(n => n["Progress"] = null);
        Change(n => n["Extra"] = true);
        Change(n => n["Settings"]!["LastStudyLevels"] = null);
        Change(n => n["Settings"]!["Level"] = "A0");
        Change(n => n["Settings"]!["DailyGoal"] = -1);
        Change(n => n["Progress"]!["SchemaVersion"] = 2);
        Change(n => n["Progress"]!["CorrectAnswers"] = -1);
        Change(n => n["Progress"]!["DailyReviewedWords"]!["2026-09-08"] = new JsonArray("bad"));
        Change(n => n["Progress"]!["Sessions"]!["fr:B2:quiz"]!["Index"] = 100);
        invalid.Add(good.Replace("\"Version\":1", "\"Version\":1,\"Version\":1", StringComparison.Ordinal));
        invalid.Add(good.Replace("\"CorrectAnswers\":77", "\"CorrectAnswers\":77,\"CorrectAnswers\":78", StringComparison.Ordinal));
        foreach (var bad in invalid)
        {
            using var folder = new TestFolder();
            var storage = await Seed(folder);
            File.WriteAllText(folder.File("progress.json"), Json(Progress())); // Deliberately mixed on disk.
            File.WriteAllText(folder.File(Journal), bad);
            File.WriteAllText(folder.File(Journal + ".bak1"), good);
            File.WriteAllText(folder.File(Journal + ".tmp-evidence"), good);
            var before = Snapshot(folder);
            foreach (var operation in AllStateOperations(storage, folder))
            {
                var ex = await Throws<PendingImportException>(operation);
                Check(ex.IsInvalidJournal);
                Check(ex.InnerException is InvalidDataException);
                Check(ex.Message.Contains("Do not just delete", StringComparison.Ordinal));
                Equal(folder.File(Journal), ex.JournalPath);
                Unchanged(before, folder);
            }
            Check(storage.HasPendingImport && storage.RequiresReload);
            AssertPair(await storage.ImportAsync(folder.File(Journal + ".bak1")));
            Unchanged(before, folder); // Preview is allowed, but cannot silently repair.
        }
        Console.WriteLine($"  Rejected {invalid.Count} malformed/unsupported full journals, with valid sidecars present.");
    }

    private static IEnumerable<Func<Task>> AllStateOperations(AppStorage storage, TestFolder folder)
    {
        yield return () => storage.LoadSettingsAsync();
        yield return () => storage.LoadProgressAsync();
        yield return () => storage.LoadStateAsync();
        yield return () => storage.SaveSettingsAsync(Settings(15));
        yield return () => storage.SaveProgressAsync(Progress(15));
#pragma warning disable CS0618 // Explicit legacy compatibility/safety regression.
        yield return () => storage.SaveCloudProfileAsync(Progress(15));
#pragma warning restore CS0618
        yield return () => storage.ExportAsync(folder.File("unrelated-output.json"), Settings(15), Progress(15));
        yield return () => storage.ApplyImportAsync(Settings(15), Progress(15));
    }

    private static async Task OversizedJournal()
    {
        using var folder = new TestFolder();
        var storage = await Seed(folder);
        using (var stream = File.Create(folder.File(Journal))) stream.SetLength((long)AppStorage.MaxFileBytes + 1);
        var before = Snapshot(folder);
        foreach (var operation in AllStateOperations(storage, folder))
        {
            var error = await Throws<PendingImportException>(operation);
            Check(error.IsInvalidJournal);
            Check(error.InnerException?.Message.Contains("32 MiB", StringComparison.Ordinal) == true);
        }
        Unchanged(before, folder);
    }

    private static async Task OversizedPreview()
    {
        using var folder = new TestFolder();
        var storage = await Seed(folder);
        var input = folder.File("large-input.json");
        using (var stream = File.Create(input)) stream.SetLength((long)AppStorage.MaxFileBytes + 1);
        var before = Snapshot(folder);
        await Throws<InvalidDataException>(() => storage.ImportAsync(input));
        Unchanged(before, folder);
        Check(!storage.HasPendingImport);
    }

    private static async Task DirectoryJournal()
    {
        using var folder = new TestFolder();
        var storage = await Seed(folder);
        Directory.CreateDirectory(folder.File(Journal));
        File.WriteAllText(folder.File(Journal + "/evidence.txt"), "Do not discard");
        var before = Snapshot(folder);
        foreach (var operation in AllStateOperations(storage, folder)) await Throws<PendingImportException>(operation);
        Check(storage.HasPendingImport && storage.RequiresReload);
        Unchanged(before, folder);
    }

    private static async Task LockedJournal()
    {
        using var folder = new TestFolder();
        await Seed(folder);
        await Crash(folder, "after-journal");
        var before = Snapshot(folder);
        var storage = new AppStorage(folder.Path);
        using (var locked = new FileStream(folder.File(Journal), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            foreach (var operation in AllStateOperations(storage, folder))
            {
                var error = await Throws<PendingImportException>(operation);
                Check(!error.IsInvalidJournal);
                Check(error.InnerException is IOException);
            }
            Check(storage.HasPendingImport && storage.RequiresReload);
            Equal(104, Read<UserSettings>(folder, "settings.json").DailyGoal);
            Equal(4, Read<ProgressState>(folder, "progress.json").CorrectAnswers);
        }
        Unchanged(before, folder);
        AssertPair(await storage.LoadStateAsync());
        AssertBackups(folder);
    }

    private static async Task LockedSettingsRecovery()
    {
        if (!OperatingSystem.IsWindows()) { Console.WriteLine("  SKIP Windows deny-delete file sharing test."); return; }
        using var folder = new TestFolder();
        await Seed(folder);
        await Crash(folder, "after-journal");
        var journal = File.ReadAllBytes(folder.File(Journal));
        var storage = new AppStorage(folder.Path);
        using (var locked = new FileStream(folder.File("settings.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            // Reads succeed, so backups rotate, but Windows refuses the primary rename.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                await Throws<PendingImportException>(() => storage.LoadProgressAsync());
                await Throws<PendingImportException>(() => storage.SaveProgressAsync(Progress(555)));
                await Throws<PendingImportException>(() => storage.ExportAsync(folder.File("unrelated-output.json"), Settings(555), Progress(555)));
                Equal(77, Read<ProgressState>(folder, "progress.json").CorrectAnswers);
                Equal(104, Read<UserSettings>(folder, "settings.json").DailyGoal);
                AssertBackups(folder); // Repeated interrupted backup rotation cannot evict originals.
                Check(journal.AsSpan().SequenceEqual(File.ReadAllBytes(folder.File(Journal))));
                Check(!File.Exists(folder.File("unrelated-output.json")));
                Check(storage.HasPendingImport);
            }
        }
        AssertPair(await storage.LoadStateAsync());
        AssertBackups(folder);
        Check(!storage.HasPendingImport && !storage.RequiresReload);
    }

    private static async Task LockedJournalDeletion()
    {
        if (!OperatingSystem.IsWindows()) { Console.WriteLine("  SKIP Windows deny-delete file sharing test."); return; }
        using var folder = new TestFolder();
        await Seed(folder);
        FileStream? locked = null;
        try
        {
            var storage = new AppStorage(folder.Path, checkpoint =>
            {
                if (checkpoint == "before-delete")
                    locked = new FileStream(folder.File(Journal), FileMode.Open, FileAccess.Read, FileShare.Read);
            });
            var error = await Throws<PendingImportException>(() => storage.ApplyImportAsync(Settings(), Progress()));
            Check(error.InnerException is IOException && !error.IsInvalidJournal);
            AssertDiskPair(folder);
            AssertBackups(folder);
            var before = Snapshot(folder);
            var retry = new AppStorage(folder.Path);
            await Throws<PendingImportException>(() => retry.LoadStateAsync());
            Unchanged(before, folder); // Both target files already match; no backup rotation.
        }
        finally { locked?.Dispose(); }
        AssertPair(await new AppStorage(folder.Path).LoadStateAsync());
        AssertBackups(folder);
    }

    private static async Task MissingObservedJournal()
    {
        using var folder = new TestFolder();
        await Seed(folder);
        var storage = await Crash(folder, "after-progress");
        var bytes = File.ReadAllBytes(folder.File(Journal));
        File.Delete(folder.File(Journal)); // Simulate an unsafe manual attempt to bypass recovery.
        var before = Snapshot(folder);
        await Throws<PendingImportException>(() => storage.LoadStateAsync());
        await Throws<PendingImportException>(() => new AppStorage(folder.Path).SaveSettingsAsync(Settings(15)));
        Check(storage.HasPendingImport && storage.RequiresReload);
        Unchanged(before, folder);
        File.WriteAllBytes(folder.File(Journal), bytes); // Restore the exact saved evidence.
        // The injected callback still represents a crashed process; restart without it.
        AssertPair(await new AppStorage(folder.Path).LoadStateAsync());
    }

    private static async Task BomJournal()
    {
        using var folder = new TestFolder();
        await Seed(folder);
        File.WriteAllBytes(folder.File(Journal), [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Envelope())]);
        AssertPair(await new AppStorage(folder.Path).LoadStateAsync());
        AssertBackups(folder);
    }

    private static async Task OrphanSidecars()
    {
        using var folder = new TestFolder();
        var storage = await Seed(folder);
        File.WriteAllText(folder.File(Journal + ".bak1"), Envelope());
        File.WriteAllText(folder.File(Journal + ".tmp-uncommitted"), Envelope());
        File.WriteAllText(folder.File(Journal + ".tmp-truncated"), "{");
        var before = Snapshot(folder);
        AssertPair(await storage.LoadStateAsync(), Settings(104, imported: false), Progress(4));
        Check(!storage.HasPendingImport);
        Unchanged(before, folder);
        await storage.ApplyImportAsync(Settings(), Progress());
        Equal(Envelope(), File.ReadAllText(folder.File(Journal + ".bak1")));
        Equal(Envelope(), File.ReadAllText(folder.File(Journal + ".tmp-uncommitted")));
        Equal("{", File.ReadAllText(folder.File(Journal + ".tmp-truncated")));
        AssertDiskPair(folder);
    }

    private static async Task ManagedPaths()
    {
        using var folder = new TestFolder();
        var storage = await Seed(folder);
        File.WriteAllText(folder.File(Journal + ".tmp-evidence"), "preserve evidence");
        var before = Snapshot(folder);
        foreach (var reserved in new[] { "settings.json", "progress.json", "cloudprofile.json", Journal })
        {
            foreach (var suffix in new[] { "", ".bak1", ".bak3", ".tmp-evidence", ".corrupt-evidence", ".bak1.tmp-evidence", ":stream", ".", " " })
            {
                await Throws<ArgumentException>(() => storage.ExportAsync(folder.File(reserved + suffix), Settings(), Progress()));
                await Throws<ArgumentException>(() => storage.ExportAsync(folder.File("./" + reserved.ToUpperInvariant() + suffix), Settings(), Progress()));
            }
        }
        Unchanged(before, folder);
        // Non-managed, separate safety exports in this folder remain supported.
        await storage.ExportAsync(folder.File("before-import-test.json"), Settings(), Progress());
        AssertPair(await storage.ImportAsync(folder.File("before-import-test.json")));
    }

    private static async Task CorruptSources()
    {
        using var folder = new TestFolder();
        await Seed(folder);
        var progressBackups = Enumerable.Range(1, 3).Select(index => File.ReadAllBytes(folder.File($"progress.json.bak{index}"))).ToArray();
        byte[] badPrimary = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("{irreplaceable truncated progress")];
        var badBackup = Encoding.UTF8.GetBytes("{irreplaceable malformed settings backup");
        File.WriteAllBytes(folder.File("progress.json"), badPrimary);
        File.WriteAllBytes(folder.File("settings.json.bak1"), badBackup);
        await Crash(folder, "after-settings");
        Check(badPrimary.AsSpan().SequenceEqual(File.ReadAllBytes(Directory.GetFiles(folder.Path, "progress.json.corrupt-*").Single())));
        Check(badBackup.AsSpan().SequenceEqual(File.ReadAllBytes(Directory.GetFiles(folder.Path, "settings.json.bak1.corrupt-*").Single())));
        for (var index = 1; index <= 3; index++)
            Check(progressBackups[index - 1].AsSpan().SequenceEqual(File.ReadAllBytes(folder.File($"progress.json.bak{index}"))));
        Equal(104, Read<UserSettings>(folder, "settings.json.bak1").DailyGoal);
        Equal(102, Read<UserSettings>(folder, "settings.json.bak2").DailyGoal);
        Equal(101, Read<UserSettings>(folder, "settings.json.bak3").DailyGoal);
        var before = Snapshot(folder);
        before.Remove(Journal);
        AssertPair(await new AppStorage(folder.Path).LoadStateAsync());
        Unchanged(before, folder); // Retry must not duplicate quarantine or rotate targets.
    }

    private static async Task StaleWrite(string kind)
    {
        using var folder = new TestFolder();
        await Seed(folder);
        await Crash(folder, "after-progress");
        var storage = new AppStorage(folder.Path);
        Task Write() => kind switch
        {
            "settings" => storage.SaveSettingsAsync(Settings(555)),
            "progress" => storage.SaveProgressAsync(Progress(555)),
#pragma warning disable CS0618
            "cloudprofile" => storage.SaveCloudProfileAsync(Progress(555)),
#pragma warning restore CS0618
            "export" => storage.ExportAsync(folder.File("stale-output.json"), Settings(555), Progress(555)),
            "apply" => storage.ApplyImportAsync(Settings(555), Progress(555)),
            _ => throw new InvalidOperationException(kind),
        };
        var error = await Throws<ImportReloadRequiredException>(Write);
        Check(error is not PendingImportException, "Successful recovery should signal stale bytes, not still-pending I/O.");
        Check(error.Message.Contains("NOT performed", StringComparison.Ordinal));
        AssertDiskPair(folder);
        AssertBackups(folder);
        Check(!File.Exists(folder.File("stale-output.json")) && !File.Exists(folder.File("cloudprofile.json")));
        Check(!storage.HasPendingImport && storage.RequiresReload);
        var before = Snapshot(folder);
        await Throws<ImportReloadRequiredException>(Write);
        await storage.LoadSettingsAsync();
        await storage.LoadSettingsAsync();
        Check(storage.RequiresReload);
        await Throws<ImportReloadRequiredException>(Write);
        Unchanged(before, folder);
        await storage.LoadProgressAsync();
        Check(!storage.RequiresReload);
        var loaded = await storage.LoadStateAsync();
        loaded.Progress.CorrectAnswers = 78;
        await storage.SaveProgressAsync(loaded.Progress);
        AssertDiskPair(folder, loaded.Settings, loaded.Progress);
    }

    private static async Task OtherInstanceInvalidation()
    {
        using var folder = new TestFolder();
        var old = await Seed(folder);
        var oldPair = await old.LoadStateAsync();
        // Canonical trailing separator / dot aliases must use the SAME import generation.
        var other = new AppStorage(folder.File("./"));
        await other.ApplyImportAsync(Settings(), Progress());
        Check(!other.RequiresReload && old.RequiresReload && !old.HasPendingImport);
        var before = Snapshot(folder);
        await Throws<ImportReloadRequiredException>(() => old.SaveSettingsAsync(oldPair.Settings));
        await Throws<ImportReloadRequiredException>(() => old.SaveProgressAsync(oldPair.Progress));
        await Throws<ImportReloadRequiredException>(() => old.ApplyImportAsync(oldPair.Settings, oldPair.Progress));
        Unchanged(before, folder);
        AssertPair(await old.LoadStateAsync());
        Check(!old.RequiresReload);
    }

    private static async Task QueuedStaleSaves()
    {
        using var folder = new TestFolder();
        var other = await Seed(folder);
        AppStorage? storage = null;
        var queued = new List<Task>();
        storage = new AppStorage(folder.Path, checkpoint =>
        {
            if (checkpoint != "after-journal") return;
            // Start, but never synchronously await, storage calls inside a held gate.
            queued.Add(storage!.SaveSettingsAsync(Settings(555)));
            queued.Add(storage.SaveProgressAsync(Progress(555)));
            queued.Add(other.SaveProgressAsync(Progress(555)));
            Check(queued.All(task => !task.IsCompleted), "IoGate must exclude every queued write until the pair is complete.");
        });
        await storage.ApplyImportAsync(Settings(), Progress());
        foreach (var task in queued) await Throws<ImportReloadRequiredException>(() => task);
        AssertDiskPair(folder);
        AssertBackups(folder);
    }

    private static async Task SplitGenerationLoads()
    {
        using var folder = new TestFolder();
        var observer = await Seed(folder);
        var importer = new AppStorage(folder.Path);
        await importer.ApplyImportAsync(Settings(), Progress());
        Equal(77, (await observer.LoadSettingsAsync()).DailyGoal);
        Check(observer.RequiresReload);
        await importer.ApplyImportAsync(Settings(88), Progress(88));
        Equal(88, (await observer.LoadProgressAsync()).CorrectAnswers);
        Check(observer.RequiresReload, "Settings from the first import cannot acknowledge the second import's progress.");
        await Throws<ImportReloadRequiredException>(() => observer.SaveProgressAsync(Progress(555)));
        Equal(88, (await observer.LoadSettingsAsync()).DailyGoal);
        Check(!observer.RequiresReload);
        AssertPair(await observer.LoadStateAsync(), Settings(88), Progress(88));
    }

    private static async Task ConcurrentLoads()
    {
        using var folder = new TestFolder();
        await Seed(folder);
        var reads = new List<Task<(UserSettings Settings, ProgressState Progress)>>();
        var storage = new AppStorage(folder.Path, checkpoint =>
        {
            if (checkpoint != "after-progress") return;
            for (var index = 0; index < 8; index++) reads.Add(new AppStorage(folder.Path).LoadStateAsync());
            Check(reads.All(read => !read.IsCompleted));
            throw new SimulatedCrashException(checkpoint);
        });
        await Throws<PendingImportException>(() => storage.ApplyImportAsync(Settings(), Progress()));
        foreach (var pair in await Task.WhenAll(reads)) AssertPair(pair);
        AssertDiskPair(folder);
        AssertBackups(folder);
    }

    private static async Task LegacyCompatibility()
    {
        using var folder = new TestFolder();
        var storage = new AppStorage(folder.Path);
        const string settingsJson = """{"UiLanguage":"tr","StudyLanguage":"en","Level":"B1","CloudConnected":true}""";
        const string progressJson = """{"KnownWords":["en:A1:apple"],"FavoriteWords":[],"GameBestScores":{},"CorrectAnswers":2,"WrongAnswers":1,"StudyStreak":1,"LastStudyDate":"2024-02-29"}""";
        File.WriteAllText(folder.File("settings.json"), settingsJson);
        File.WriteAllText(folder.File("progress.json"), progressJson);
        var before = Snapshot(folder);
        var pair = await storage.LoadStateAsync();
        Check(!pair.Settings.CloudConnected);
        Equal("B1", pair.Settings.LastStudyLevels["en"]);
        Equal(20, pair.Settings.DailyGoal);
        Equal(0, pair.Progress.RecallRatings.Count);
        Equal(0, pair.Progress.DailyReviewedWords.Count);
        Equal(0, pair.Progress.QuizCorrectAnswers);
        Equal(2, pair.Progress.CorrectAnswers);
        Unchanged(before, folder);
        await storage.SaveSettingsAsync(pair.Settings);
        await storage.SaveProgressAsync(pair.Progress);
        Equal(settingsJson, File.ReadAllText(folder.File("settings.json.bak1")));
        Equal(progressJson, File.ReadAllText(folder.File("progress.json.bak1")));
        for (var generation = 2; generation <= 5; generation++) await storage.SaveProgressAsync(Progress(generation));
        for (var index = 1; index <= 3; index++) Equal(5 - index, Read<ProgressState>(folder, $"progress.json.bak{index}").CorrectAnswers);
        const string corrupt = "{legacy-corrupt-evidence";
        File.WriteAllText(folder.File("progress.json"), corrupt);
        var recovered = await storage.LoadProgressAsync();
        Equal(4, recovered.CorrectAnswers);
        Equal(corrupt, File.ReadAllText(folder.File("progress.json")));
        await storage.SaveProgressAsync(recovered);
        Equal(corrupt, File.ReadAllText(Directory.GetFiles(folder.Path, "progress.json.corrupt-*").Single()));
        File.Delete(folder.File("progress.json"));
        Equal(4, (await storage.LoadProgressAsync()).CorrectAnswers);
        Check(!File.Exists(folder.File("progress.json")));
        Check(!File.Exists(folder.File(Journal)) && !storage.HasPendingImport);
        Check(storage.RecoveryMessage is not null);
        storage.ClearRecoveryMessage();
        Check(storage.RecoveryMessage is null);
    }
}

internal sealed class SimulatedCrashException(string checkpoint) : Exception(checkpoint);

internal sealed class TestFolder : IDisposable
{
    // Never use the default AppStorage constructor or the OS/user-profile temp directory.
    public string Path { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "scratch", Guid.NewGuid().ToString("N"));
    public TestFolder() => Directory.CreateDirectory(Path);
    public string File(string name) => System.IO.Path.Combine(Path, name);
    public void Dispose() => Directory.Delete(Path, recursive: true);
}