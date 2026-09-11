using System.Text;
using System.Text.RegularExpressions;
using YDKE_Windows;

Console.OutputEncoding = Encoding.UTF8;
var passed = 0;
var errors = new List<string>();
var warnings = new List<string>();
void Check(bool condition, string message)
{
    if (condition) passed++;
    else errors.Add(message);
}
string Canonical(string text) => Regex.Replace(text.Normalize(NormalizationForm.FormC)
    .Replace('’', '\'').Replace('‘', '\''), @"\s+", " ").Trim();

try
{
    var root = SourceAudit.FindRoot(args.FirstOrDefault());
    var directory = Path.Combine(root, "windows", "src", "YDKE.Windows");
    var sources = Directory.GetFiles(directory, "MainPage*.cs").Order(StringComparer.Ordinal)
        .ToDictionary(path => Path.GetFileName(path), File.ReadAllText);
    Check(sources.Count >= 4, "Expected the native MainPage partials; source audit must not be empty.");
    var gamesSource = sources["MainPage.Games.cs"];
    var mainSource = sources["MainPage.xaml.cs"];
    var modeExpression = SourceAudit.Expression(gamesSource, "ScoreMode");
    var modes = Regex.Matches(modeExpression, SourceAudit.Literal).Select(m => SourceAudit.Unquote(m.Value))
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    Check(modes.SequenceEqual(new[] { "practice", "standard", "timed" }), "Score modes changed: update localization and mode contracts.");
    var calls = sources.SelectMany(pair => SourceAudit.Calls(pair.Key!, pair.Value, modes)).ToArray();
    var callKeys = calls.Select(call => call.Key).ToHashSet(StringComparer.Ordinal);
    Check(callKeys.Count > 150, "Suspiciously few T/U keys; the source parser may have lost coverage.");
    Check(new[] { "Games.SimpleTitle", "Games.ComplexTitle", "Games.SimpleSubtitle", "Games.ComplexSubtitle" }.All(callKeys.Contains),
        "Conditional T keys must include both game groups.");
    var instructionKeys = SourceAudit.Instructions(gamesSource);
    Check(instructionKeys.Count == GameCatalog.All.Count, "Expected 24 explicit instruction cases and the Speed default.");
    var defaultGames = GameCatalog.All.Where(game => !instructionKeys.ContainsKey(game.Id)).ToArray();
    Check(defaultGames.Length == 1 && defaultGames[0].Id == "speedround", "Unreviewed game is falling through to speed instructions.");

    var expected = callKeys.Concat(GameCatalog.All.Select(game => $"GameDescription.{game.Id}"))
        .Concat(instructionKeys.Values).Concat(modes.Select(mode => $"Games.Mode.{mode}"))
        .Concat(Enum.GetNames<RecallRating>().Select(rating => "Rating." + rating))
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    Check(GameCatalog.All.Count == 25, "Expected 25 real catalog games.");
    Check(Localizer.UiLanguages.Select(l => l.Code).Order().SequenceEqual(new[] { "de", "en", "es", "fr", "nl", "pt", "tr" }),
        "UI language set changed; update the seven-language contracts.");

    foreach (var language in Localizer.UiLanguages)
    {
        var missing = Localizer.MissingKeys(language.Code, expected);
        Check(missing.Count == 0, $"{language.Code}: missing explicit translations: {string.Join(", ", missing)}");
        Check(ExperienceStrings.MissingKeys(language.Code).Count == 0, $"{language.Code}: incomplete resource table");
        foreach (var key in expected.Concat(ExperienceStrings.Keys).Distinct(StringComparer.Ordinal))
        {
            var text = ExperienceStrings.Get(language.Code, key, "__EN_FALLBACK__", "__TR_FALLBACK__");
            Check(!string.IsNullOrWhiteSpace(text) && text != key && !text.Contains("__", StringComparison.Ordinal),
                $"{language.Code}/{key}: empty, raw key or fallback result: {text}");
            Check(Localizer.Get(language.Code, key) == text, $"{language.Code}/{key}: T/U routes disagree");
            Check(Localizer.Get(language.Code.ToUpperInvariant() + "_ZZ", key) == text,
                $"{language.Code}/{key}: regional language normalization differs");
            if (language.Code != "en" && Regex.Matches(Localizer.Get("en", key), @"\p{L}+").Count >= 5)
                Check(text != Localizer.Get("en", key), $"{language.Code}/{key}: long English text copied into translated column");
        }
        foreach (var game in GameCatalog.All)
        {
            var key = $"GameDescription.{game.Id}";
            Check(ExperienceStrings.GameDescription(language.Code, game) == Localizer.Get(language.Code, key),
                $"{language.Code}/{game.Id}: description not routed through the explicit resource");
            var instruction = instructionKeys.GetValueOrDefault(game.Id, instructionKeys["_"]);
            Check(ExperienceStrings.GameInstructions(language.Code, game) == Localizer.Get(language.Code, instruction),
                $"{language.Code}/{game.Id}: instruction helper differs from real MainPage switch ({instruction})");
        }
        foreach (var key in ExperienceStrings.Keys.Where(key => key.StartsWith("Skill.", StringComparison.Ordinal)))
            Check(ExperienceStrings.SkillLabel(language.Code, key[6..]) == Localizer.Get(language.Code, key),
                $"{language.Code}/{key}: skill expansion differs");
        Console.WriteLine($"LANGUAGE {language.Code}: expected={expected.Length}, resources={ExperienceStrings.Keys.Count}, missing={missing.Count}");
    }

    // The real game agent's English copy is the semantic baseline, not a fixture
    // copied from ExperienceStrings. Changing a duration, scope or instruction in
    // the UI without reviewing translations therefore fails even when keys exist.
    foreach (var call in calls.Where(call => call.Key.StartsWith("Games.", StringComparison.Ordinal) && call.English is not null))
        Check(Canonical(Localizer.Get("en", call.Key)) == Canonical(call.English!),
            $"Stale English {call.Key} at {call.File}:{call.Line}; UI: {call.English}; resource: {Localizer.Get("en", call.Key)}");

    foreach (var game in GameCatalog.All.Where(game => game.Mechanic is GameMechanic.TimedChoice or GameMechanic.TimedTyping or GameMechanic.CategorySprint))
        Check(new GameSession(game).SecondsRemaining == 60, $"{game.Id}: runtime duration changed; review all duration translations.");
    Check(Regex.IsMatch(modeExpression, @"UntimedPractice\s*\?\s*""practice"""), "Practice no longer selects an isolated score mode.");
    Check(Regex.IsMatch(gamesSource, @"GameEngine\.ScoreKey\(_settings\.StudyLanguage,\s*_settings\.Level,\s*ScoreMode\(game\),\s*game\.Id\)"),
        "Best score lookup no longer includes language, level, mode and game.");
    Check(Regex.IsMatch(mainSource, @"_gameScoreKey\s*=\s*GameEngine\.ScoreKey\(_settings\.StudyLanguage,\s*_settings\.Level,\s*_gameMode,\s*game\.Id\)"),
        "Best score persistence no longer includes language, level, mode and game.");
    Check(mainSource.Contains("if (_settings.UntimedPractice) _activeGame.SecondsRemaining = 0;", StringComparison.Ordinal),
        "Untimed practice no longer disables the timer.");

    MechanicsContracts.Verify(directory, sources.Values, Check);
    GameReadabilityContracts.Verify(directory, sources.Values, Check);
    StudyUxContracts.Verify(directory, sources.Values, Check);
    SettingsUxContracts.Verify(directory, Check);
    GameInputUxContracts.Verify(directory, Check);
    ShellUxContracts.Verify(directory, Check);
    HelpUxContracts.Verify(directory, Check);

    // Audit the parser itself, including a missing/new key, conditional branches,
    // interpolated UI content and an unknown dynamic argument that must fail closed.
    var sample = """
        // T("Ignored.Comment")
        var a = U("New.Missing", "English", "Türkçe");
        var b = T(group == GameGroup.Simple ? "Games.SimpleTitle" : "Games.ComplexTitle");
        var c = $"label: {T("Game.Score")}";
        var d = T("Games.Mode." + ScoreMode(game));
        """;
    var parsed = SourceAudit.Calls("parser self-test", sample, modes).Select(call => call.Key).ToArray();
    Check(parsed.Length == 7 && !parsed.Contains("Ignored.Comment") && parsed.Contains("Game.Score"),
        "Source parser regression: literal/conditional/interpolation/mode coverage.");
    Check(Localizer.MissingKeys("de", parsed).SequenceEqual(new[] { "New.Missing" }), "Missing-key audit accepted an unknown UI key.");
    var rejected = false;
    try { _ = SourceAudit.Calls("parser self-test", "T(unreviewedDynamicKey)", modes).ToArray(); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "Unaudited dynamic key was silently ignored.");
    Check(Localizer.MissingKeys("zz", expected).Count == expected.Length, "Unsupported-language audit masked missing resources with English.");
    Check(ExperienceStrings.Get("zz", "Game.Score", "bad", "bad") == Localizer.Get("en", "Game.Score"), "Unsupported-language fallback changed.");
    Check(ExperienceStrings.Get("tr", "Unknown.Control", "English control", "Türkçe kontrol") == "Türkçe kontrol", "Turkish unknown-key fallback changed.");
    Check(ExperienceStrings.Get("de", "Unknown.Control", "English control", "Türkçe kontrol") == "English control", "English unknown-key fallback changed.");
    Check(ExperienceStrings.Get("en", "Unknown.Control", "", "") == "Unknown.Control", "Raw-key negative control failed.");

    // Known UI integration gap: resources alone cannot translate a value which
    // bypasses both U and T. Keep this visible without editing another owner's UI.
    if (Regex.IsMatch(gamesSource, @"\{_settings\.Level\}:\{ScoreMode\(game\)\}"))
        warnings.Add("GameBestLabel displays raw practice/timed/standard identifiers; Games.Mode.* translations exist but the UI must route the label through them.");

    Console.WriteLine($"AUDIT files={sources.Count}, call-key-occurrences={calls.Length}, distinct-call-keys={callKeys.Count}, instruction-mappings={GameCatalog.All.Count}, descriptions={GameCatalog.All.Count}, dynamic-modes={modes.Length}");
}
catch (Exception ex)
{
    errors.Add(ex.ToString());
}
foreach (var warning in warnings) Console.WriteLine("AUDIT_WARNING " + warning);
foreach (var error in errors) Console.WriteLine("FAIL " + error);
Console.WriteLine($"RESULT passed={passed} errors={errors.Count} audit-warnings={warnings.Count}");
return errors.Count == 0 ? 0 : 1;