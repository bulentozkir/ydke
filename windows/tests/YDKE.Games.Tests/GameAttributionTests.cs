using System.Text.Json;

namespace YDKE_Windows;

internal static class GameAttributionTests
{
    private const string Apple = "en:A1:apple";
    private const string Pear = "en:A1:pear";
    private const string Day = "2026-09-08";
    private static void Assert(bool value, string message = "Assertion failed") => GameSourceContracts.Require(value, message);
    private static string Json(ProgressState progress) => JsonSerializer.Serialize(progress);
    private static VocabularyEntry Word(string word, string language = "en", string level = "A1") => new(word, "noun", level, "Test", "definition", "example", language);
    private static void Credit(ProgressState progress, int correct, int wrong, params string[] words)
    {
        Assert(progress.GameCorrectAnswers == correct && progress.GameWrongAnswers == wrong, $"Expected {correct}/{wrong} objective answers, got {progress.GameCorrectAnswers}/{progress.GameWrongAnswers}.");
        Assert(progress.Reviews.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(words), "Reviewed a missing or untested word.");
        Assert((progress.DailyReviewedWords.GetValueOrDefault(Day) ?? []).SetEquals(words), "Daily credit must be distinct per vocabulary key.");
        Assert(progress.QuizCorrectAnswers == 0 && progress.QuizWrongAnswers == 0 && progress.RecallRatings.Count == 0, "Game actions contaminated quiz/card metrics.");
        LearningEngine.ValidateProgress(progress);
    }

    public static void Register(Action<string, Action> check, string dataRoot)
    {
        var source = new Lazy<GameSourceContracts>(() => new(dataRoot));
        var factory = new Lazy<Func<IGameSourceProbe>>(() => GameSourceProbe.Compile(source.Value));
        IGameSourceProbe New(string id = "speedround", string mode = "timed")
        {
            var probe = factory.Value(); probe.Start(id, mode); return probe;
        }
        void Async(string name, Func<Task> test) => check(name, () => test().GetAwaiter().GetResult());

        check("Actual source: every Resolve caller supplies explicit tested keys", () => source.Value.ExplicitKeys());
        check("Actual source: no feedback lookup or legacy learning writers", () => source.Value.NoDisplayOrLegacyAttribution());
        check("Actual source: atomic board saves precede state changes; final feedback never rescores", () => source.Value.BoardAtomicity());
        check("Actual source: sentence, semantic, reading and open-input key origins", () => source.Value.KeyOrigins());
        check("Actual source: completion shares the best-score transaction; stats/profile handoff", () => source.Value.CompletionAndStats());
        check("Actual source: primary timer progress and mode-aware practice UI", () => source.Value.ProgressAndPractice());

        Async("Actual resolver: translated, composite and key-like feedback cannot change attribution", async () =>
        {
            string[] feedbacks = ["apple", "apple — fruit; elma", "Across: apple · Down: pear", "Completed a line", "6 / 6",
                "a whole sentence with apple and pear.", "café ↔ chaud — Synonyms", "en:A1:unrelated", "\"word\" + [fake.Key] — ✓", ""];
            foreach (var correct in new[] { true, false })
            {
                string? expected = null;
                foreach (var feedback in feedbacks)
                {
                    var probe = New();
                    probe.Progress.CorrectAnswers = 12; probe.Progress.WrongAnswers = 8;
                    probe.Progress.DailyActivity["2026-09-07"] = 9;
                    probe.Progress.KnownWords.Add(Pear);
                    await probe.Resolve(correct, feedback, [Apple, Pear, Apple]);
                    Credit(probe.Progress, correct ? 1 : 0, correct ? 0 : 1, Apple, Pear);
                    Assert(probe.Progress.CorrectAnswers == 12 && probe.Progress.WrongAnswers == 8 && probe.Progress.DailyActivity["2026-09-07"] == 9);
                    Assert(probe.Progress.DailyActivity.Count == 1 && probe.Progress.KnownWords.SetEquals([Pear]));
                    Assert(probe.Progress.CompletedGames == 0 && probe.SaveAttempts == 1);
                    expected ??= Json(probe.Progress);
                    Assert(Json(probe.Progress) == expected, "Only feedback changed, but persisted attribution changed too.");
                }
            }
        });

        Async("Actual resolver: reading with vocabulary-like feedback has objective credit only", async () =>
        {
            foreach (var correct in new[] { true, false })
            {
                var probe = New("readingcomprehension", "standard");
                await probe.Resolve(correct, "apple\nExplanation: en:A1:apple; pear — armut", []);
                Credit(probe.Progress, correct ? 1 : 0, correct ? 0 : 1);
                Assert(probe.Progress.DailyReviewedWords.Count == 0 && probe.Progress.StudyStreak == 1 && probe.Progress.CompletedGames == 0);
            }
        });

        Async("Actual source methods: failed answer save rolls back, retries once per accepted attempt", async () =>
        {
            var probe = New(); probe.FailuresRemaining = 2;
            var before = Json(probe.Progress);
            await probe.Resolve(false, "Wrong — not a key", [Apple, Pear, Apple]);
            Assert(probe.SaveAttempts == 3 && probe.Rollbacks.Count == 2 && probe.Rollbacks.All(p => p == before));
            Credit(probe.Progress, 0, 1, Apple, Pear);
            Assert(probe.Progress.Reviews[Apple].Mistakes == 1 && probe.Progress.Reviews[Pear].Mistakes == 1);
            Assert(probe.Session.Round == 2 && probe.Session.Lives == 2 && probe.Progress.CompletedGames == 0);
        });

        Async("Actual source methods: abandon failed answer leaves no review or completion", async () =>
        {
            var probe = New(); probe.FailuresRemaining = 1; probe.LeaveOnFeedback = true;
            var before = Json(probe.Progress);
            await probe.Resolve(false, "apple", [Apple]);
            Assert(Json(probe.Progress) == before && probe.Session.Round == 1 && probe.Session.Lives == 3 && !probe.Active);
        });

        Async("Actual source methods: duplicate submit and detached controls cannot rescore", async () =>
        {
            var probe = New(); probe.HoldNextSave();
            var pending = probe.Resolve(true, "apple", [Apple]);
            Assert(!pending.IsCompleted && probe.SaveAttempts == 1);
            await probe.Resolve(false, "pear", [Pear]);
            Assert(probe.SaveAttempts == 1);
            probe.ReleaseSave(); await pending;
            Credit(probe.Progress, 1, 0, Apple);
            probe.DetachSource();
            await probe.Resolve(false, "pear", [Pear]);
            Assert(!await probe.SubAnswer(false, [Pear]) && probe.SaveAttempts == 1);
        });

        Async("Actual source methods: key snapshot survives editing during pending save", async () =>
        {
            var probe = New(); probe.HoldNextSave();
            var keys = new List<string> { Apple, Apple };
            var pending = probe.SubAnswer(true, keys);
            Assert(!pending.IsCompleted);
            keys.Clear(); keys.Add(Pear);
            probe.ReleaseSave(); Assert(await pending);
            Credit(probe.Progress, 1, 0, Apple);
        });

        Async("Actual source methods: old saved sub-answer cannot advance a newer game", async () =>
        {
            var probe = New("bossrush", "standard"); var old = probe.Session;
            probe.HoldNextSave(); var pending = probe.SubAnswer(true, [Apple]);
            Assert(!await probe.SubAnswer(false, [Pear]), "Atomic answer gate must reject concurrent clicks.");
            probe.Leave(); probe.Start("memory", "standard"); var current = probe.Session;
            probe.ReleaseSave(); Assert(!await pending, "Stale save continuation was allowed to advance the board.");
            Assert(probe.Active && old.Round == 1 && old.Score == 0 && current.Round == 1 && current.Score == 0);
            // An answer successfully committed before leaving is retained, not erased.
            Credit(probe.Progress, 1, 0, Apple);
            Assert(probe.Progress.CompletedGames == 0);
        });

        Async("Actual resolver: final board result awards points without an extra objective answer", async () =>
        {
            var probe = New("memory", "standard");
            Assert(await probe.SubAnswer(true, [Apple]));
            Assert(await probe.SubAnswer(false, [Pear]));
            var before = Json(probe.Progress);
            await probe.Resolve(true, "board finished: apple + pear", [Pear], alreadyRecorded: true);
            Assert(before == Json(probe.Progress) && probe.SaveAttempts == 2 && probe.Session.Round == 2 && probe.Session.Score > 0);
        });

        Async("Bingo attribution: partial line credits its tested words, never untested board cells", async () =>
        {
            var probe = New("bingo", "standard");
            var keys = Enumerable.Range(0, 16).Select(i => Word("bingo" + i).Key).ToArray();
            foreach (var key in keys.Take(3)) Assert(await probe.SubAnswer(true, [key]));
            Assert(await probe.SubAnswer(false, [keys[3]]));
            await probe.Resolve(false, "wrong selection — " + keys[3], [keys[3]], true);
            Credit(probe.Progress, 3, 1, keys.Take(4).ToArray());
            Assert(keys.Skip(4).All(key => !probe.Progress.Reviews.ContainsKey(key)) && probe.Progress.CompletedGames == 0);
        });

        Async("Memory attribution: mismatched pair is one answer/two keys; six matches are six answers", async () =>
        {
            var probe = New("memory", "standard");
            var keys = Enumerable.Range(0, 6).Select(i => Word("memory" + i).Key).ToArray();
            Assert(await probe.SubAnswer(false, [keys[0], keys[1]]));
            Credit(probe.Progress, 0, 1, keys[0], keys[1]);
            foreach (var key in keys) Assert(await probe.SubAnswer(true, [key]));
            await probe.Resolve(true, "6 / 6", [keys[^1]], true);
            Credit(probe.Progress, 6, 1, keys);
            Assert(probe.Progress.Reviews[keys[0]].Mistakes == 1 && probe.Progress.Reviews[keys[2]].Mistakes == 0);
        });

        Async("Boss attribution: all fifteen hits and two misses survive round-final feedback", async () =>
        {
            var probe = New("bossrush", "standard");
            var keys = Enumerable.Range(0, 18).Select(i => Word("boss" + i).Key).ToArray();
            foreach (var key in keys.Take(2)) Assert(await probe.SubAnswer(false, [key]));
            foreach (var key in keys.Skip(2).Take(15)) Assert(await probe.SubAnswer(true, [key]));
            await probe.Resolve(true, "three bosses defeated", [keys[16]], true);
            Credit(probe.Progress, 15, 2, keys.Take(17).ToArray());
            Assert(!probe.Progress.Reviews.ContainsKey(keys[17]) && probe.Progress.CompletedGames == 0);
        });

        Async("CodyCross attribution: wrong clue tests only that word; all five solved clues are credited", async () =>
        {
            var probe = New("codycross", "standard");
            var keys = Enumerable.Range(0, 5).Select(i => Word("clue" + i).Key).ToArray();
            Assert(await probe.SubAnswer(false, [keys[0]])); Credit(probe.Progress, 0, 1, keys[0]);
            foreach (var key in keys) Assert(await probe.SubAnswer(true, [key]));
            await probe.Resolve(true, "a b c d e — bonus", [keys[^1]], true);
            Credit(probe.Progress, 5, 1, keys);
        });

        Async("Crossword attribution: partial Down miss never fails or rescores solved Across", async () =>
        {
            var probe = New("crossword", "standard");
            Assert(await probe.SubAnswer(true, [Apple]));
            Assert(await probe.SubAnswer(false, [Pear]));
            Assert(probe.Progress.Reviews[Apple].Mistakes == 0 && probe.Progress.Reviews[Pear].Mistakes == 1);
            Assert(await probe.SubAnswer(true, [Pear]));
            await probe.Resolve(true, "Across: apple · Down: pear", [Pear], true);
            Credit(probe.Progress, 2, 1, Apple, Pear);
        });

        Async("Full-word attempts: Word Guess and Scramble record tries but never twice on final", async () =>
        {
            foreach (var id in new[] { "wordguess", "scramble" })
            {
                var probe = New(id, "standard");
                Assert(await probe.SubAnswer(false, [Apple])); Assert(await probe.SubAnswer(true, [Apple]));
                await probe.Resolve(true, "APPLE", [Apple], true);
                Credit(probe.Progress, 1, 1, Apple);
            }
        });

        Async("Semantic keys: only exact current headwords; two keys are still one objective answer", async () =>
        {
            var pair = new SemanticPair("café", "thé", false);
            VocabularyEntry[] words = [Word("café", "fr"), Word("CAFÉ", "fr"), Word("cafe", "fr"), Word("le café", "fr"), Word("thé", "fr", "B1"), Word("thé", "en")];
            var keys = GameEngine.SemanticWordKeys(pair, words, "fr", "A1");
            Assert(keys.SequenceEqual(new[] { "fr:A1:café" }));
            Assert(GameEngine.SemanticWordKeys(new("absent", "ghost", true), words, "fr", "A1").Count == 0);
            keys = GameEngine.SemanticWordKeys(pair, words.Concat([Word("thé", "fr"), Word("café", "fr")]), "fr", "A1");
            var probe = New("wordmorph", "standard");
            await probe.Resolve(true, "not an attribution key — translated relationship", keys);
            Credit(probe.Progress, 1, 0, "fr:A1:café", "fr:A1:thé");
        });

        Async("Actual completion: failures roll back count AND best, retry commits once", async () =>
        {
            var probe = New(); probe.Session.Score = 250; probe.FailuresRemaining = 2;
            var before = Json(probe.Progress);
            await probe.Complete();
            Assert(probe.Progress.CompletedGames == 1 && !probe.Active && probe.SaveAttempts == 3);
            Assert(probe.Rollbacks.Count == 2 && probe.Rollbacks.All(p => p == before));
            Assert(probe.Progress.GameBestScores[GameEngine.ScoreKey("en", "A1", "timed", "speedround")] == 250);
            Assert(probe.Progress.DailyReviewedWords.Count == 0 && probe.Progress.StudyStreak == 0);
            var saved = Json(probe.Progress); await probe.Complete(); await probe.Complete();
            Assert(Json(probe.Progress) == saved && probe.SaveAttempts == 3);
            // A different completed game counts even when it does not beat the best.
            probe.Start(); probe.Session.Score = 100; await probe.Complete();
            Assert(probe.Progress.CompletedGames == 2 && probe.Progress.GameBestScores.Values.Single() == 250);
        });

        Async("Actual completion: concurrent callback or abandoning a failed save cannot inflate totals", async () =>
        {
            var probe = New(); probe.Session.Score = 250; probe.HoldNextSave();
            var pending = probe.Complete(); Assert(!pending.IsCompleted);
            await probe.Complete(); Assert(probe.SaveAttempts == 1);
            probe.ReleaseSave(); await pending;
            Assert(probe.Progress.CompletedGames == 1 && probe.SaveAttempts == 1);
            var failed = New(); failed.Session.Score = 250; failed.FailuresRemaining = 1; failed.LeaveOnFeedback = true;
            var before = Json(failed.Progress); await failed.Complete(); await failed.Complete();
            Assert(Json(failed.Progress) == before && !failed.Active);
        });

        check("Progress rules: elapsed seconds / 60, daily / 1, finite practice and hidden survival", () =>
        {
            foreach (var game in GameCatalog.All.Where(GameEngine.HasClock))
            {
                var timed = new GameSession(game) { Round = 31, SecondsRemaining = 23 };
                Assert(GameEngine.Progress(timed, "timed") == (60, 37, true) && GameEngine.RoundLimit(timed, "timed") is null);
                timed.SecondsRemaining = 0;
                Assert(!timed.IsTimed && GameEngine.Progress(timed, "timed") == (60, 60, true));
                timed.SecondsRemaining = -4; Assert(GameEngine.Progress(timed, "timed") == (60, 60, true));
                timed.SecondsRemaining = 99; Assert(GameEngine.Progress(timed, "timed") == (60, 0, true));
                timed.Round = 4; timed.SecondsRemaining = 0;
                Assert(GameEngine.Progress(timed, "practice") == (10, 3, true));
                Assert(GameEngine.Duration(game, true) == "practice" && GameEngine.Duration(game, false) == "minute");
            }
            var daily = new GameSession(GameCatalog.All.Single(g => g.Id == "dailychallenge"));
            Assert(GameEngine.Progress(daily, "standard") == (1, 0, true)); daily.Round++;
            Assert(GameEngine.Progress(daily, "practice") == (1, 1, true));
            var survival = new GameSession(GameCatalog.All.Single(g => g.Id == "survival")) { Round = 99 };
            Assert(GameEngine.RoundLimit(survival, "standard") is null && !GameEngine.Progress(survival, "standard").Visible);
            Assert(GameEngine.RoundLimit(survival, "practice") is null && !GameEngine.Progress(survival, "practice").Visible);
        });
    }
}