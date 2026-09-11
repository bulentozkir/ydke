using System.Globalization;
using System.Text.RegularExpressions;

namespace YDKE_Windows;

/// <summary>Focused source contracts, not a substitute for WinUI rendering/UIA checks.
/// Reads complete current method bodies, including local functions and both dispatch
/// switches. No app/profile initialization, WinUI dependency or copied renderer fixture.</summary>
internal static class GameReadabilityContracts
{
    private static readonly (string Method, string[] Games, (string Panel, string Value)[] Questions)[] Renderers =
    [
        ("RenderHangmanRound", ["hangman"], [("panel", "LocalizedPart(entry.Definition)")]),
        ("RenderScrambleRound", ["scramble"], [("panel", "LocalizedPart(entry.Definition)")]),
        ("RenderAudioGame", ["dictation", "listeningchoice"], [("panel", "GameTask(session.Game)")]),
        ("RenderChoiceChallenge", ["speedround", "survival"], [("panel", "LocalizedPart(entry.Definition)")]),
        ("RenderTrueFalseChallenge", ["truefalse"], [("panel", "$\"{entry.Word}\\n{LocalizedPart(shown.Definition)}\"")]),
        ("RenderClassGame", ["wordclass"], [("panel", "$\"{word.Word}\\n{GameEngine.NativeText(word.Example)}\"")]),
        ("RenderMemoryRound", ["memory"], [("panel", "U:Games.Memory.Pairs")]),
        ("RenderOddGame", ["oddoneout"], [("panel", "category.Key")]),
        ("RenderTypingChallenge", ["wordrace"], [("panel", "LocalizedPart(entry.Definition)")]),
        ("RenderClozeGame", ["clozetest"], [("panel", "picked.Mask!")]),
        ("RenderSentenceGame", ["sentencescramble"], [("panel", "GameTask(session.Game)")]),
        ("RenderLocalDataGame", ["readingcomprehension", "wordmorph"],
            [("panel", "pages[0]"), ("questionPanel", "question.Prompt"), ("panel", "pair.Word + \" ↔ \" + pair.Related")]),
        ("RenderBingoGame", ["bingo"], [("panel", "LocalizedPart(words[target].Definition)")]),
        ("RenderBossRushRound", ["bossrush"], [("panel", "string.Empty")]),
        ("RenderCodyCrossRound", ["codycross"], [("rowStack", "LocalizedPart(entries[row].Definition)")]),
        ("RenderCrosswordGame", ["crossword"],
            [("panel", "acrossLabel + \": \" + LocalizedPart(crossing.Across.Definition)"),
             ("panel", "downLabel + \": \" + LocalizedPart(crossing.Down.Definition)")]),
        ("RenderWordGuessRound", ["dailychallenge", "wordguess"], [("panel", "U:Games.Guess.Prompt")]),
        ("RenderRackGame", ["scrabble"], [("panel", "GameTask(session.Game)")]),
        ("RenderCategoryGame", ["categorysprint"], [("panel", "_categoryWords[0].Category")]),
        ("RenderClueGame", ["cluedetective"], [("panel", "GameTask(session.Game)")]),
        ("RenderMatrixGame", ["matrix"], [("panel", "LocalizedPart(entry.Definition)")]),
    ];

    public static void Verify(string sourceDirectory, IEnumerable<string> partials, Action<bool, string> check)
    {
        var source = string.Join("\n", partials);
        var assertions = 0;
        var failures = 0;
        void Test(bool condition, string message)
        {
            assertions++;
            if (!condition) failures++;
            check(condition, "Game readability: " + message);
        }
        void Has(string body, string fragment, string message) => Test(Compact(body).Contains(Compact(fragment), StringComparison.Ordinal), message);

        var expectedIds = Renderers.SelectMany(r => r.Games).ToArray();
        Test(expectedIds.Length == 25 && expectedIds.Distinct(StringComparer.Ordinal).Count() == 25 &&
            GameCatalog.All.Count == 25 && GameCatalog.All.Select(g => g.Id).ToHashSet(StringComparer.Ordinal).SetEquals(expectedIds),
            "exactly the 25 real catalog IDs must be mapped, with no omitted or duplicate game.");
        Test(Renderers.Select(r => r.Method).Distinct(StringComparer.Ordinal).Count() == 21,
            "expected 21 renderers (audio, choice, word guess and local data each serve two games).");

        var dispatch = Method(source, "RenderGameRound");
        var byId = Routes(Switch(dispatch, "session.Game.Id"), Test);
        var byMechanic = Routes(Switch(dispatch, "session.Game.Mechanic"), Test);
        var bodies = Renderers.ToDictionary(r => r.Method, r => Method(source, r.Method), StringComparer.Ordinal);
        var questionArguments = new List<string>();
        var questionCount = 0;
        foreach (var renderer in Renderers)
        {
            foreach (var id in renderer.Games)
            {
                var game = GameCatalog.All.SingleOrDefault(g => g.Id == id);
                var actual = byId.GetValueOrDefault(id) ?? (game is null ? null : byMechanic.GetValueOrDefault("GameMechanic." + game.Mechanic));
                Test(actual == renderer.Method, $"{id}: actual dispatch is {actual ?? "unmapped"}, expected {renderer.Method}.");
            }
            var body = bodies[renderer.Method];
            var questions = Calls(body, "AddGameQuestion").ToArray();
            questionCount += questions.Length;
            Test(questions.Length == renderer.Questions.Length, $"{renderer.Method}: question call sites changed; review the real prompts, not just helper presence.");
            foreach (var expected in renderer.Questions)
                Test(questions.Any(call => call.Args.Length == 2 && Compact(call.Args[0]) == expected.Panel && MatchesValue(call.Args[1], expected.Value)),
                    $"{renderer.Method}: {expected.Panel}/{expected.Value} must be passed to AddGameQuestion.");
            questionArguments.AddRange(questions.SelectMany(q => q.Args.Skip(1)));

            foreach (var helper in new[] { "GameCaption", "GamePrompt" })
                Test(!Calls(body, helper).Any(call => Regex.IsMatch(call.Args[0], @"\.(?:Definition|Example|Prompt|Related|Mask)\b")),
                    $"{renderer.Method}: a definition/example/question still uses {helper}.");
            var scenes = Calls(body, "AddGameScene").ToArray();
            Test(scenes.Length > 0 && scenes.All(call => call.Args.Length is 2 or 3 &&
                Compact(call.Args[0]) == "session.Game" && call.Args[1].Trim() is "panel" or "content"),
                $"{renderer.Method}: real question content must reach AddGameScene directly, not through a scaling wrapper.");
            Test(Initializers(body, "Viewbox").All(box => renderer.Method == "RenderBossRushRound" && Compact(Property(box, "Child")) == "hpTrack"),
                $"{renderer.Method}: only the decorative HP bar may be scaled; learning text and boards must not shrink.");
            foreach (var question in questions)
            {
                var binding = Regex.Match(Code(body[..question.Start]), @"\bvar\s+(?<name>\w+)\s*=\s*$");
                if (!binding.Success) continue;
                var name = binding.Groups["name"].Value;
                Test(!Regex.IsMatch(Code(body), @"\b" + Regex.Escape(name) + @"\.(?:FontSize|Foreground|Opacity|TextWrapping|TextTrimming|TextLineBounds|LineHeight|Height|MaxHeight|MaxLines|Clip|RenderTransform)\s*="),
                    $"{renderer.Method}: the live question reference '{name}' overrides shared readability properties.");
            }
        }
        Test(questionCount == 24, "expected 24 real prompt/task call sites, including both crossword clues and both local-data branches.");

        var questionBody = Method(source, "AddGameQuestion");
        Test(Regex.IsMatch(Code(source), @"\bTextBlock\s+AddGameQuestion\s*\(\s*Panel\s+panel\s*,\s*string\s+text\s*\)"),
            "AddGameQuestion must still return a mutable TextBlock and accept Panel/string.");
        var prompt = NamedInitializer(questionBody, "prompt", "TextBlock");
        var card = NamedInitializer(questionBody, "card", "Border");
        var font = FontValue(prompt, "FontSize");
        var lineHeight = FontValue(prompt, "LineHeight");
        Test(font == 26 && lineHeight == 36 && lineHeight / font >= 1.3, "question needs Font(26) and generous Font(36) line height.");
        foreach (var (property, value) in new[]
        {
            ("Text", "text"), ("TextAlignment", "TextAlignment.Left"), ("TextWrapping", "TextWrapping.Wrap"),
            ("TextTrimming", "TextTrimming.None"), ("TextLineBounds", "TextLineBounds.Full"),
            ("HorizontalAlignment", "HorizontalAlignment.Stretch"), ("IsTextSelectionEnabled", "true"),
        })
            Test(Compact(Property(prompt, property)) == value, $"question {property} must be {value}.");
        Test(Unclipped(questionBody), "question subtree must not set fixed dimensions, MaxLines, clipping, visibility or transforms.");
        Test(!Initializers(questionBody, "Viewbox").Any(), "question subtree must not use Viewbox.");
        Test(Opaque(questionBody), "question/card/label must have effective local opacity 1.");
        foreach (var fragment in new[]
        {
            "AutomationProperties.SetAutomationId(prompt, \"game.Question\")", "Live(prompt)",
            "copy.Children.Add(prompt)", "Child = copy", "panel.Children.Add(card)", "return prompt;",
            "AutomationProperties.SetAutomationId(card, \"game.QuestionCard\")",
        }) Has(questionBody, fragment, "missing question attachment/live-reference contract: " + fragment);

        var foreground = ColorValue(prompt, "Foreground");
        var background = ColorValue(card, "Background");
        Test(foreground == (255, 15, 23, 42) && background == (255, 248, 250, 252), "question must use the actual opaque ink/paper brushes.");
        var contrast = Contrast(foreground, background);
        Test(foreground.A == 255 && background.A == 255 && contrast >= 7, $"production question contrast {contrast:F2}:1 must be >= 7:1, with no translucent brush.");
        var labels = Initializers(questionBody, "TextBlock").Where(block => Property(block, "Text").StartsWith("U(", StringComparison.Ordinal)).ToArray();
        Test(labels.Length == 1 && MatchesValue(Property(labels[0], "Text"), "U:Games.Question"), "question card must actually render its localized Question label.");
        if (labels.Length == 1)
        {
            var ink = ColorValue(labels[0], "Foreground");
            Test(ink.A == 255 && Contrast(ink, background) >= 7, "the production Question label also needs >= 7:1 contrast.");
        }

        var scene = Method(source, "AddGameScene");
        var contentCard = NamedInitializer(scene, "contentCard", "Border");
        var contentInk = ColorValue(contentCard, "Background");
        Test(contentInk == (255, 15, 23, 42), "content card must be solid dark opaque, not a translucent scrim or gradient.");
        Test(!Regex.IsMatch(scene, "[✦✧★☆]") && !Initializers(scene, "TextBlock").Any(), "AddGameScene must not add decorative stars/text behind the question.");
        foreach (var fragment in new[] { "right.Children.Add(content)", "Child = right", "layout.Children.Add(contentCard)",
            "ConfigureGameLayout(layout, visualHost, contentCard)", "grid.Children.Add(layout)", "PageContent.Children.Add(arena)" })
            Has(scene, fragment, "missing unscaled content attachment: " + fragment);
        var viewboxes = Initializers(scene, "Viewbox").ToArray();
        Test(viewboxes.Length == 1 && Compact(Property(viewboxes[0], "Child")) == "visual??DefaultGameVisual(game)",
            "AddGameScene's single Viewbox must scale only the separate left visual.");
        Has(scene, "visualHost.Children.Add(new Viewbox", "visual Viewbox must stay in the visual host, not the question ancestry.");
        foreach (var parent in new[] { contentCard, NamedInitializer(scene, "right", "StackPanel"),
            NamedInitializer(scene, "layout", "Grid"), NamedInitializer(scene, "grid", "Grid"), NamedInitializer(scene, "arena", "Border"), Method(source, "GameSceneContent") })
            Test(Unclipped(parent) && Opaque(parent), "question content ancestors must not clip, scale or lower permanent opacity.");
        var entrance = Method(source, "AnimateEntrance");
        Test(!Regex.IsMatch(Code(entrance), @"\b(?:ScaleTransform|CompositeTransform|Viewbox)\b"), "arena entrance must not scale question text.");
        Has(entrance, "var fade = new DoubleAnimation { To = 1,", "arena entrance must finish at full opacity.");

        var caption = Method(source, "GameCaption");
        var captionInk = ColorValue(caption, "Foreground");
        Test(FontValue(caption, "FontSize") >= 18 && Opaque(caption) && captionInk.A == 255 &&
            captionInk.R >= 240 && captionInk.G >= 240 && captionInk.B >= 240 && Contrast(captionInk, contentInk) >= 7,
            "GameCaption must have child-readable type, opaque near-white, with >= 7:1 contrast on the dark content card.");
        Has(source, "private static double ReadingSize(double size) => Math.Max(18, Font(size))", "child text must never shrink below 18 logical pixels.");
        Has(scene, "if (interactiveBoard)", "interactive game boards need an unscaled branch.");
        Has(scene, "visualHost.Children.Add(visual!)", "interactive board must not be placed in a Viewbox.");
        Has(scene, "game.Id is \"matrix\" or \"wordguess\" or \"dailychallenge\"", "Matrix and guessed-letter boards must reserve unscaled space.");
        Has(scene, "else if (visual is StackPanel) visualHost.Children.Add(visual)", "boss/bonus captions must not be scaled with decorative artwork.");
        Has(source, "Text = guess[index] + \"\\n\"", "guessed letters and position marks need separate readable lines.");
        Has(bodies["RenderCodyCrossRound"], "ConfigureResponsiveGrid(bonusRow, 5, 38)", "bonus letters must reflow rather than shrink.");
        Has(bodies["RenderCodyCrossRound"], "FontSize = ReadingSize(20)", "CodyCross input must honor the reading floor.");
        Has(bodies["RenderCrosswordGame"], "AddGameScene(session.Game, panel, board)", "crossword letters must not pass through a scaling wrapper.");
        Has(bodies["RenderCrosswordGame"], "Math.Max(34, ReadingSize(18) * 1.4 + 4)", "crossword cells must grow with readable text.");
        Has(bodies["RenderMatrixGame"], "var visual = board; board.Width = 308", "Matrix needs room for 48px touch targets.");
        var layout = Method(source, "ConfigureGameLayout");
        Has(layout, "width < 760 ? 120 : Math.Clamp(width * 0.23, 120, 220)", "left visual must reserve a compact 120px while remaining capped at 220 / 23%.");
        Test(Calls(layout, "Grid.SetColumn").Select(c => Compact(string.Join(",", c.Args))).SequenceEqual(new[] { "visual,0", "content,1" }) &&
            Calls(layout, "Grid.SetRow").Select(c => Compact(string.Join(",", c.Args))).SequenceEqual(new[] { "visual,0", "content,0" }),
            "visual and content must stay side-by-side in row 0, including compact widths.");
        Test(!Regex.IsMatch(Code(layout), @"\bRowDefinitions\s*\.\s*Add\s*\("), "no above-content stacking row may be introduced.");
        var round = Method(source, "RenderGameRound");
        Has(round, "new StackPanel { Spacing = 4, Children = { hud, _gamePrimaryProgress } }", "game progress must share the HUD row instead of consuming another page row.");
        var toolbar = Method(source, "GameToolbar");
        Has(toolbar, "toolbar.ColumnDefinitions.Add(new ColumnDefinition())", "game title and actions must remain on one toolbar row at compact widths.");
        Has(toolbar, "Grid.SetColumn(actions, 1)", "game actions must stay in the toolbar's second column.");
        Test(!toolbar.Contains("ConfigureResponsiveGrid(toolbar", StringComparison.Ordinal), "the game toolbar must not stack into a second row.");
        foreach (var file in new[] { "MainPage.xaml", "MainWindow.xaml" })
            Test(!Regex.IsMatch(File.ReadAllText(Path.Combine(sourceDirectory, file)), @"<(?:\w+:)?Viewbox\b"), $"{file}: no outer Viewbox may scale game questions.");

        // These are real live TextBlock references, not merely a matching helper definition.
        Has(bodies["RenderBingoGame"], "var clue = AddGameQuestion(panel, LocalizedPart(words[target].Definition))", "Bingo must retain the displayed question reference.");
        Has(bodies["RenderBingoGame"], "target = remaining[_random.Next(remaining.Length)]; Announce(clue, LocalizedPart(words[target].Definition))", "Bingo must announce the new target through that reference.");
        Has(bodies["RenderClueGame"], "var caption = AddGameQuestion(panel, GameTask(session.Game))", "Clue Detective must retain the displayed question reference.");
        Has(bodies["RenderClueGame"], "Announce(caption, string.Join(\" · \", clues.Take(revealed)))", "Clue Detective must update the same question when a clue is revealed.");
        Has(bodies["RenderBossRushRound"], "var word = AddGameQuestion(panel, string.Empty)", "Boss Rush must retain the displayed question reference.");
        Has(Method(bodies["RenderBossRushRound"], "NextQuestion"), "word.Text = correctEntry.Word", "Boss Rush NextQuestion must update that TextBlock.");
        var codyRows = Method(bodies["RenderCodyCrossRound"], "RenderAllRows");
        foreach (var fragment in new[] { "rowsPanel.Children.Clear()", "if (row != activeRow) continue", "AddGameQuestion(rowStack, LocalizedPart(entries[row].Definition))", "rowsPanel.Children.Add(rowStack)", "var capturedRow = row", "SubmitRowAsync(capturedRow, input, cellsGrid, submit)" })
            Has(codyRows, fragment, "CodyCross live row/capture contract missing: " + fragment);
        Has(bodies["RenderCodyCrossRound"], "var bonusRow = new Grid { ColumnSpacing = 4, RowSpacing = 4", "CodyCross bonus letters must stay in one compact row.");
        Has(Method(bodies["RenderCodyCrossRound"], "SubmitRowAsync"), "RenderAllRows()", "CodyCross must rebuild questions after advancing the active row.");
        Has(bodies["RenderCodyCrossRound"], "panel.Children.Add(rowsPanel)", "CodyCross questions must reach the scene content, not its scaled bonus visual.");
        Has(Method(bodies["RenderLocalDataGame"], "Refresh"), "Announce(text, pages[page])", "reading pagination must update the returned question TextBlock.");
        Has(bodies["RenderLocalDataGame"], "content.Children.Add(questionPanel)", "reading question must reach the unscaled scene content.");
        Has(Method(source, "Announce"), "text.Text = message", "live announcements must mutate TextBlock.Text.");

        var keys = SourceAudit.Calls("question helpers", questionBody + "\n" + string.Join("\n", questionArguments), []).Select(c => c.Key)
            .Concat(SourceAudit.Instructions(source).Values).Distinct(StringComparer.Ordinal).ToArray();
        Test(keys.Contains("Games.Question") && keys.Contains("Games.Memory.Pairs") && keys.Contains("Games.Guess.Prompt"), "resource audit must derive actual question labels, not just list fixture keys.");
        Test(Localizer.UiLanguages.Count == 7, "question labels need all seven UI locales.");
        foreach (var language in Localizer.UiLanguages)
            foreach (var key in keys)
                Test(Localizer.MissingKeys(language.Code, [key]).Count == 0 &&
                    !string.IsNullOrWhiteSpace(ExperienceStrings.Get(language.Code, key, "__missing__", "__missing__")) &&
                    ExperienceStrings.Get(language.Code, key, "__missing__", "__missing__") is var value && value != key && value != "__missing__",
                    $"{language.Code}/{key}: missing explicit question/task label.");

        // Negative controls use mutations of the real helper, never a renderer/color fixture.
        Test(!Unclipped(questionBody.Replace("Text = text,", "Text = text, MaxLines = 2,", StringComparison.Ordinal)), "clipping audit failed its real-source negative control.");
        Test(!Opaque(questionBody.Replace("Text = text,", "Text = text, Opacity = 0.5,", StringComparison.Ordinal)), "opacity audit failed its real-source negative control.");
        var lowContrast = prompt.Replace("Color.FromArgb(255, 15, 23, 42)", "Color.FromArgb(255, 248, 250, 252)", StringComparison.Ordinal);
        Test(Contrast(ColorValue(lowContrast, "Foreground"), background) < 7, "contrast audit failed its mutated production-brush negative control.");
        Console.WriteLine($"READABILITY checks={assertions} errors={failures} catalog={expectedIds.Length} renderers={Renderers.Length} question-sites={questionCount} locales={Localizer.UiLanguages.Count} question-contrast={contrast.ToString("F2", CultureInfo.InvariantCulture)}:1");
        foreach (var renderer in Renderers) Console.WriteLine($"READABILITY_MAP {string.Join(",", renderer.Games)} -> {renderer.Method}");
    }

    private static string Compact(string value) => Regex.Replace(SourceAudit.WithoutComments(value), @"\s+", "");
    private static bool MatchesValue(string actual, string expected) => expected.StartsWith("U:", StringComparison.Ordinal)
        ? Calls(actual, "U").Any(c => c.Start == 0 && c.Args.Length == 3 && c.Args[0] == "\"" + expected[2..] + "\"")
        : Compact(actual) == Compact(expected);
    private static bool Unclipped(string body) => !Regex.IsMatch(Code(body), @"\b(?:Width|Height|MaxHeight|MaxLines|Clip|RenderTransform|LayoutTransform|Visibility)\s*=");
    private static bool Opaque(string body) => Regex.Matches(body, @"\bOpacity\s*=\s*(?<value>[^,;}]+)")
        .All(m => double.TryParse(m.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity) && opacity == 1);
    private static double FontValue(string body, string property)
    {
        var match = Regex.Match(body, @"\b" + property + @"\s*=\s*(?:Font|ReadingSize)\(\s*(?<value>\d+(?:\.\d+)?)\s*\)");
        if (!match.Success) throw new InvalidOperationException($"Cannot audit real {property}/Font value.");
        return double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
    }
    private static (int A, int R, int G, int B) ColorValue(string body, string property)
    {
        var match = Regex.Match(body, @"\b" + property + @"\s*=\s*new\s+SolidColorBrush\(\s*Color.FromArgb\(\s*(?<n>\d+)\s*,\s*(?<n>\d+)\s*,\s*(?<n>\d+)\s*,\s*(?<n>\d+)\s*\)\s*\)");
        var values = match.Groups["n"].Captures.Select(c => int.Parse(c.Value, CultureInfo.InvariantCulture)).ToArray();
        if (!match.Success || values.Length != 4 || values.Any(n => n is < 0 or > 255)) throw new InvalidOperationException($"Cannot audit real {property}/ARGB brush.");
        return (values[0], values[1], values[2], values[3]);
    }
    private static double Contrast((int A, int R, int G, int B) a, (int A, int R, int G, int B) b)
    {
        static double Channel(int channel) { var c = channel / 255.0; return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        static double Luminance((int A, int R, int G, int B) c) => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        var x = Luminance(a); var y = Luminance(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }
    private static string Property(string body, string property)
    {
        var match = Regex.Match(Code(body), @"\b" + Regex.Escape(property) + @"\s*=\s*");
        if (!match.Success) return "";
        return Parts(body[(match.Index + match.Length)..]).First().Trim();
    }
    private static Dictionary<string, string> Routes(string body, Action<bool, string> check)
    {
        var routes = new Dictionary<string, string>(StringComparer.Ordinal);
        var count = 0;
        foreach (Match match in Regex.Matches(body, @"(?:case\s+(?<key>" + SourceAudit.Literal + @"|GameMechanic\.\w+)\s*:\s*)+(?<renderer>Render\w+)\s*\(\s*session\s*\)\s*;\s*(?:return|break)\s*;"))
        {
            foreach (Capture key in match.Groups["key"].Captures)
            {
                count++;
                check(routes.TryAdd(key.Value.StartsWith('"') ? SourceAudit.Unquote(key.Value) : key.Value, match.Groups["renderer"].Value), "duplicate real dispatch case.");
            }
        }
        check(count == Regex.Matches(Code(body), @"\bcase\b").Count && count > 0, "every real switch case must be recognized; unsupported dispatch syntax fails closed.");
        return routes;
    }
    private static string Switch(string source, string expression)
    {
        var code = Code(source);
        var match = Regex.Match(code, @"\bswitch\s*\(\s*" + Regex.Escape(expression) + @"\s*\)\s*\{");
        if (!match.Success) throw new InvalidOperationException("Missing real dispatch switch: " + expression);
        var start = match.Index + match.Length - 1;
        return source[(start + 1)..Close(code, start)];
    }
    private static string Method(string source, string name)
    {
        var code = Code(source);
        var matches = Regex.Matches(code, @"\b(?:void|Grid|TextBlock|StackPanel|Task(?:<[^>]+>)?)\s+" + Regex.Escape(name) + @"\s*\([^{};]*?\)\s*(?:=>|\{)");
        if (matches.Count != 1) throw new InvalidOperationException($"Expected one real {name} method, found {matches.Count}.");
        var start = matches[0].Index + matches[0].Length;
        return code[start - 1] == '{' ? source[start..Close(code, start - 1)] : source[start..code.IndexOf(';', start)];
    }
    private static string NamedInitializer(string source, string name, string type)
    {
        var code = Code(source);
        var match = Regex.Match(code, @"\bvar\s+" + Regex.Escape(name) + @"\s*=\s*new\s+" + Regex.Escape(type) + @"\s*\{");
        if (!match.Success) throw new InvalidOperationException($"Missing real {type} initializer '{name}'.");
        var start = match.Index + match.Length - 1;
        return source[(start + 1)..Close(code, start)];
    }
    private static IEnumerable<string> Initializers(string source, string type)
    {
        var code = Code(source);
        foreach (Match match in Regex.Matches(code, @"\bnew\s+" + Regex.Escape(type) + @"\s*\{"))
        {
            var start = match.Index + match.Length - 1;
            yield return source[(start + 1)..Close(code, start)];
        }
    }
    private static IEnumerable<(int Start, string[] Args)> Calls(string source, string name)
    {
        var code = Code(source);
        foreach (Match match in Regex.Matches(code, @"\b" + Regex.Escape(name) + @"\s*\("))
        {
            var start = match.Index + match.Length - 1;
            yield return (match.Index, Parts(source[(start + 1)..Close(code, start)]).Select(p => p.Trim()).ToArray());
        }
    }
    private static IEnumerable<string> Parts(string source)
    {
        var code = Code(source); var start = 0; var depth = 0;
        for (var i = 0; i < code.Length; i++)
        {
            if (code[i] is '(' or '[' or '{') depth++;
            if (code[i] is ')' or ']' or '}') depth--;
            if (code[i] != ',' || depth != 0) continue;
            yield return source[start..i]; start = i + 1;
        }
        yield return source[start..];
    }
    private static int Close(string code, int start)
    {
        var opening = code[start]; var closing = opening switch { '(' => ')', '[' => ']', '{' => '}', _ => throw new InvalidOperationException("Expected delimiter.") };
        var depth = 1;
        for (var i = start + 1; i < code.Length; i++)
        {
            if (code[i] == opening) depth++;
            if (code[i] == closing && --depth == 0) return i;
        }
        throw new InvalidOperationException("Unbalanced real source body.");
    }

    // Mask trivia/literals without shifting offsets. Interpolated strings are skipped
    // recursively so quotes/braces in U(...) or string.Join(...) cannot end a method.
    private static string Code(string source)
    {
        var code = source.ToCharArray();
        for (var i = 0; i < source.Length;)
        {
            var end = LiteralEnd(source, i);
            if (end == i) { i++; continue; }
            for (; i < end; i++) if (code[i] is not ('\r' or '\n')) code[i] = ' ';
        }
        return new string(code);
    }
    private static int LiteralEnd(string text, int start)
    {
        var tail = text.AsSpan(start);
        if (tail.StartsWith("//")) { var end = text.IndexOf('\n', start); return end < 0 ? text.Length : end; }
        if (tail.StartsWith("/*")) { var end = text.IndexOf("*/", start + 2, StringComparison.Ordinal); return end < 0 ? text.Length : end + 2; }
        var interpolated = tail.StartsWith("$\"") || tail.StartsWith("$@\"") || tail.StartsWith("@$\"");
        var verbatim = tail.StartsWith("@\"") || tail.StartsWith("$@\"") || tail.StartsWith("@$\"");
        var prefix = interpolated && verbatim ? 2 : interpolated || verbatim ? 1 : 0;
        if (prefix == 0 && text[start] is not ('"' or '\'')) return start;
        var quote = text[start + prefix];
        for (var i = start + prefix + 1; i < text.Length; i++)
        {
            if (!verbatim && text[i] == '\\') { i++; continue; }
            if (text[i] == quote)
            {
                if (verbatim && i + 1 < text.Length && text[i + 1] == quote) { i++; continue; }
                return i + 1;
            }
            if (!interpolated || text[i] != '{') continue;
            if (i + 1 < text.Length && text[i + 1] == '{') { i++; continue; }
            var depth = 1;
            for (i++; i < text.Length && depth > 0; i++)
            {
                var end = LiteralEnd(text, i);
                if (end != i) { i = end - 1; continue; }
                if (text[i] == '{') depth++;
                if (text[i] == '}') depth--;
            }
            i--;
        }
        throw new InvalidOperationException("Unterminated literal in real source.");
    }
}