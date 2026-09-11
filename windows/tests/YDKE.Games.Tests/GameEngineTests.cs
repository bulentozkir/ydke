using YDKE_Windows;

var failures = 0;
var total = 0;
void Check(string name, Action test)
{
    total++;
    try { test(); Console.WriteLine("PASS " + name); }
    catch (Exception e) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + e.Message); }
}
void Assert(bool value) { if (!value) throw new InvalidOperationException("Assertion failed"); }
VocabularyEntry Word(string word, string example = "", string category = "Food", string language = "en") => new(word, "noun", "A1", category, "definition", example, language);

Check("Catalog remains exactly 25 / 11 / 14, unique IDs", () => Assert(GameCatalog.All.Count == 25 && GameCatalog.Get(GameGroup.Simple).Count == 11 && GameCatalog.Get(GameGroup.Complex).Count == 14 && GameCatalog.All.Select(g => g.Id).Distinct().Count() == 25));
Check("Turn gate closes once before save and ignores stale callbacks", () =>
{
    var gate = new GameTurnGate(); var first = gate.Begin();
    Assert(gate.CanTick(first, false) && !gate.CanTick(first, true));
    Assert(gate.TryClose(first) && !gate.TryClose(first) && !gate.CanTick(first, false));
    var second = gate.Begin();
    Assert(!gate.TryClose(first) && !gate.CanTick(first, false) && gate.CanTick(second, false));
    Assert(gate.TryClose(second) && !gate.TryClose(second));
});
Check("Scoped scores obey existing validation and isolate practice/timed/level/language", () =>
{
    var p = new ProgressState();
    foreach (var language in new[] { "en", "fr" }) foreach (var level in new[] { "A1", "B2" }) foreach (var mode in new[] { "practice", "timed", "standard" })
        p.GameBestScores.Add(GameEngine.ScoreKey(language, level, mode, "categorysprint"), 120);
    p.GameBestScores["categorysprint"] = 1234;
    LearningEngine.ValidateProgress(p); Assert(p.GameBestScores.Count == 13);
});
Check("Cloze masks every whole occurrence without leaking translation", () => Assert(GameEngine.Cloze(Word("cat", "A cat sees another cat. - Bir kedi başka bir kedi görür.")) == "A _____ sees another _____."));
Check("Cloze rejects substring and inflected-only matches", () => Assert(GameEngine.Cloze(Word("cat", "The cats sleep on the mat.")) is null && GameEngine.Cloze(Word("art", "The artist works here.")) is null));
Check("Placeholder examples never become sentence puzzles", () => Assert(!GameEngine.UsableExample("No example sentence available for this word.")));
Check("Cloze strips language-specific article, preserves accents", () => Assert(GameEngine.Cloze(Word("le café", "Le café est très chaud.;Kahve sıcak.", language: "fr")) == "Le _____ est très chaud."));
Check("Sentence tokens preserve duplicate tiles and punctuation", () => Assert(GameEngine.Tokens("Yes, yes, we can.").SequenceEqual(new[] { "Yes,", "yes,", "we", "can." }) && GameEngine.SentenceKey("The cat.") != GameEngine.SentenceKey("cat.")));
Check("Rack requires real vocabulary and repeated accented letters", () =>
{
    var words = new[] { Word("letter"), Word("tree"), Word("café") };
    Assert(GameEngine.RackWord(words, "letter", "leter", new HashSet<string>(), "en") is null);
    Assert(GameEngine.RackWord(words, "tree", "letter", new HashSet<string>(), "en")?.Word == "tree");
    Assert(GameEngine.RackWord(words, "leet", "letter", new HashSet<string>(), "en") is null);
    Assert(GameEngine.RackWord(words, "tree", "letter", new HashSet<string> { "tree" }, "en") is null);
    Assert(GameEngine.RackWord(words, "cafe", "café", new HashSet<string>(), "en") is null);
});
Check("Class metadata rejects ambiguous class", () => Assert(GameEngine.WordClass("noun, verb") is null && GameEngine.WordClass("adj.") == "adjective"));
Check("Categories exclude General and duplicate headwords", () => Assert(!GameEngine.Categories(new[] { Word("one", category: "General"), Word("two", category: "General"), Word("cat"), Word("cat") }, 2).Any()));
Check("Crossword has an actual shared square", () =>
{
    var puzzle = GameEngine.FindCrossing(new[] { Word("cat"), Word("tap") })!;
    Assert(puzzle is not null && GameEngine.Bare(puzzle.Across)[puzzle.AcrossIndex] == GameEngine.Bare(puzzle.Down)[puzzle.DownIndex]);
    Assert(GameEngine.FindCrossing(new[] { Word("cat"), Word("fig") }) is null);
});
Check("Word guess consumes duplicate letters only once", () => Assert(GameEngine.LetterFeedback("allee", "apple").SequenceEqual(new[] { 2, 1, 0, 0, 2 })));
Check("Matrix prohibits row wrap/reuse/bent path", () => Assert(GameEngine.StraightSelection(new[] { 0, 7, 14 }, 6) && GameEngine.StraightSelection(new[] { 14, 7, 0 }, 6) && !GameEngine.StraightSelection(new[] { 5, 6, 7 }, 6) && !GameEngine.StraightSelection(new[] { 0, 1, 7 }, 6) && !GameEngine.StraightSelection(new[] { 0, 1, 0 }, 6)));
Check("Bingo validates rows columns diagonals", () => Assert(GameEngine.BingoLine(new HashSet<int> { 4, 5, 6, 7 }) && GameEngine.BingoLine(new HashSet<int> { 0, 4, 8, 12 }) && GameEngine.BingoLine(new HashSet<int> { 0, 5, 10, 15 }) && GameEngine.BingoLine(new HashSet<int> { 3, 6, 9, 12 }) && !GameEngine.BingoLine(new HashSet<int> { 0, 1, 2, 7 })));
Check("Daily target deterministic", () => Assert(GameEngine.DailyIndex(1000, "en:A1", new(2026, 9, 8)) == GameEngine.DailyIndex(1000, "en:A1", new(2026, 9, 8))));
Check("JS parser handles comments concatenation escapes trailing commas", () =>
{
    var value = LocalGameData.LiteralArray("// [comment]\nwindow.TEST = [{text: 'a' + /* comment */ \"b\\u00e9\", n: 2,},];");
    Assert(value[0]!["text"]!.GetValue<string>() == "abé");
});
Check("JS parser never executes expressions", () =>
{
    try { LocalGameData.LiteralArray("window.TEST = [{text: fetch('https://example.invalid')}];"); }
    catch (InvalidDataException) { return; }
    throw new Exception("Accepted executable expression");
});
Check("Semantic evidence is reciprocal and non-conflicting", () =>
{
    var source = "window.TEST = [{word:'hot',level:'A1',synonyms:'warm',antonyms:'cold'}, {word:'warm',level:'A1',synonyms:'hot'}, {word:'cold',level:'A1',antonyms:'hot'}, {word:'fake',level:'A1',synonyms:'hot'}];";
    var pairs = LocalGameData.ReadRelationships(source, "A1");
    Assert(pairs.Length == 4 && !pairs.Any(p => p.Word == "fake"));
    Assert(LocalGameData.ReadRelationships("window.TEST = [{word:'hot',level:'A1',synonyms:'warm',antonyms:'warm'},{word:'warm',level:'A1',synonyms:'hot'}];", "A1").Length == 0);
});

// Runs solely against repository data; never reads/writes AppStorage or a user profile.
var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../data"));
GameAttributionTests.Register(Check, root);
foreach (var (language, file) in new[] { ("en", "readingcompencefr.js"), ("de", "readingcompde.js"), ("fr", "readingcompfr.js") })
foreach (var level in new[] { "A1", "A2", "B1", "B2", "C1", "C2" })
    Check($"Actual reading {language}:{level}", () => { var passages = LocalGameData.ReadPassages(File.ReadAllText(Path.Combine(root, file)), level); Assert(passages.Length > 0 && passages.All(p => p.Questions.Length > 0 && p.Text.Length > 30)); });
foreach (var (language, file) in new[] { ("en", "synanten.js"), ("de", "synantde.js"), ("fr", "synantfr.js") })
    Check($"Actual reciprocal relationships {language}", () =>
    {
        var source = File.ReadAllText(Path.Combine(root, file));
        foreach (var level in new[] { "A1", "A2", "B1", "B2", "C1", "C2" })
        {
            var pairs = LocalGameData.ReadRelationships(source, level); Console.WriteLine($"  {level}: {pairs.Length} reciprocal pairs");
            // A zero count is an explicit unavailable route, never permission to
            // manufacture synonyms from categories (German has sparse levels).
            Assert(pairs.All(p => p.Word != p.Related));
            if (pairs.Length == 0) Console.WriteLine($"  UNAVAILABLE {language}:{level}: no reciprocal evidence");
        }
    });
foreach (var language in VocabularyRepository.Languages)
foreach (var level in language.Levels)
    Check($"Actual word eligibility {language.Code}:{level}", () =>
    {
        var words = JavaScriptVocabularyParser.Parse(File.ReadAllText(Path.Combine(root, language.Files[level])), language.Code);
        var cloze = words.Count(w => GameEngine.Cloze(w) is not null);
        var categories = GameEngine.Categories(words, 3).Count();
        var crossing = GameEngine.FindCrossing(words);
        Assert(words.Count > 0);
        Console.WriteLine($"  words={words.Count} cloze={cloze} categories={categories} crossing={crossing is not null}");
    });
Console.WriteLine($"RESULT {total - failures}/{total}");
return failures == 0 ? 0 : 1;