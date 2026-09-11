using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace YDKE_Windows;

// Source contracts only: no WinUI activation, profile access or copied learning implementation.
internal static class StudyUxContracts
{
    public static void Verify(string sourceDirectory, IEnumerable<string> partials, Action<bool, string> check)
    {
        var source = File.ReadAllText(Path.Combine(sourceDirectory, "MainPage.Study.cs"));
        var assertions = 0;
        var failures = 0;
        void Test(bool condition, string message)
        {
            assertions++;
            if (!condition) failures++;
            check(condition, "Study UX: " + message);
        }
        Test(partials.Any(partial => Compact(partial) == Compact(source)), "audit the current owned partial, not a stale renderer fixture.");
        var cards = Member(source, "RenderCards");
        var quiz = Member(source, "RenderQuiz");
        var rating = Member(source, "RateCardAsync");
        var move = Member(source, "MoveCardAsync");
        var undo = Member(source, "BuildUndoButton");
        var answer = Member(source, "AnswerQuizAsync");
        var next = Member(source, "ContinueQuizAsync");
        var save = Member(source, "MutateStudyAsync");
        var feedback = Member(source, "AddQuizFeedback");
        var text = Member(source, "StudyText");
        var surface = Member(source, "StudySurface");
        var layout = Member(source, "ConfigureStudyChoices");
        var option = Member(source, "StudyOptionContent");
        var tools = Member(source, "CompactStudyAction");
        var disclosure = Member(source, "StudyDisclosure");
        var popup = Member(source, "StudyPopupButton");
        var readableAction = Member(source, "ReadableStudyAction");
        var optionButton = Member(source, "StudyOptionButton");
        var wordRow = Member(source, "StudyWordRow");
        var cardHelp = Member(source, "AddCardsHintButton");

        Test(CardsGated(cards), "rating buttons must require reveal and an unrated card, and invoke the selected RecallRating.");
        var revealed = Block(cards, "if (_cardRevealed || rated)");
        Test(Has(revealed, "StudyDetail(T(\"Cards.Meaning\"), LocalizedPart(entry.Definition)") && Has(revealed, "StudyDetail(T(\"Cards.Example\"), entry.Example"),
            "meaning and example must remain visible on already-rated navigation as well as after reveal.");
        Test(Has(revealed, "StudyDetail(T(\"Cards.Meaning\"), LocalizedPart(entry.Definition), 24)") &&
            Has(revealed, "if (!string.IsNullOrWhiteSpace(entry.Example)) content.Children.Add(StudyDetail(T(\"Cards.Example\"), entry.Example, 20))"),
            "the real meaning must use 24-point reading text, followed by the real example at 20 points only when present.");
        Test(Has(cards, "StudyText(entry.Word, 40, emphasis: true, selectable: true)"), "the focused word must be large, full text and selectable.");
        Test(Has(cards, "var reveal = ReadableStudyAction(AccentButton(U(\"Kids.Cards.Reveal\"") && Has(cards, "if (!_cardRevealed && !rated) content.Children.Add(reveal)"),
            "reveal must be a primary action before recall, not a permanent redundant toolbar item.");
        Test(StagedRatings(cards), "rating choices must enter the visual tree only after reveal or when revisiting an already-rated card.");
        Test(Has(cards, "var next = rated ? ReadableStudyAction(AccentButton(T(\"Cards.Next\")") && Has(cards, "next.IsEnabled = rated"),
            "Next must be primary on an already-rated card and unavailable otherwise.");
        Test(Has(cards, "previous.IsEnabled = session.Index > 0") && Has(move, "if (delta > 0 && !session.Answers.ContainsKey(session.WordKeys[session.Index])) return"),
            "previous/next boundary and rating guards must remain in the UI and action.");
        Test(Has(rating, "if (!_cardRevealed || session is null || session.Index >= session.WordKeys.Count) return") &&
            Has(rating, "if (session.Answers.ContainsKey(key)) return"), "keyboard/programmatic ratings must retain action-level gates.");
        Test(Has(rating, "LearningEngine.RecordCardReview(_progress, key, rating, Today)") &&
            Has(rating, "session.Index = LearningEngine.NextUnansweredIndex(session)") && Has(rating, "keepUndo: true"),
            "rating must keep real grading, next-unanswered auto-advance and Undo capture.");
        Test(Has(rating, "session.Answers[key] = rating != RecallRating.Again") &&
            Has(rating, "_cardUndo = before; _cardUndoContext = StudyContext; _cardRevealed = false;") &&
            !Regex.IsMatch(WithoutComments(rating), @"WordKeys\.(?:Add|Insert)|StartStudySessionAsync"),
            "Help me must still record Again as unsuccessful, capture Undo, and never append or restart the queue.");
        Test(Has(undo, "undo.IsEnabled = _cardUndo is not null && _cardUndoContext == StudyContext") &&
            Has(undo, "if (_cardUndo is not { } snapshot || _cardUndoContext != StudyContext) return") &&
            Has(undo, "MutateStudyAsync(() => _progress = JsonSerializer.Deserialize<ProgressState>(snapshot)!)"),
            "Undo must restore the real context-scoped snapshot through persistence.");
        foreach (var id in new[] { "cards.Reveal", "cards.Listen", "cards.Previous", "cards.Next" })
            Test(Regex.IsMatch(cards, @"FocusTarget\(\w+,\s*""" + Regex.Escape(id) + @"""\)"), "missing focus target " + id);
        Test(Has(cards, "FocusTarget(button, $\"cards.Rating.{rating}\")") && Has(undo, "FocusTarget(undo, \"cards.Undo\")"), "rating and Undo IDs must remain stable.");
        foreach (var key in new[] { "Kids.Cards.StepRecall", "Kids.Cards.StepRate", "Kids.Cards.StepRated", "Kids.Rating.AgainHelp", "Kids.Rating.HardHelp", "Kids.Rating.GoodHelp", "Kids.Rating.EasyHelp" })
            Test(cards.Contains("U(\"" + key + "\",", StringComparison.Ordinal), "missing rendered cue/description " + key);
        Test(OrderedRatingCopy(cards, "labels", "", new[] { "Help me", "Hard", "I knew it", "Easy!" }),
            "the four child-friendly labels must map in the exact Again/Hard/Good/Easy order, not change the ratings.");
        Test(OrderedRatingCopy(cards, "descriptions", "Help", new[] { "Try this one again", "I needed time", "I remembered", "I knew it quickly" }),
            "short visible helpers must retain the same four recall meanings and order.");
        Test(Regex.IsMatch(WithoutComments(File.ReadAllText(Path.Combine(sourceDirectory, "Models.cs"))),
            @"enum\s+RecallRating\s*\{\s*Again\s*,\s*Hard\s*,\s*Good\s*,\s*Easy\s*\}"),
            "the renderer's indexed labels must match the real RecallRating enum, not a copied grading fixture.");
        Test(Has(cards, "descriptions[index], palette.BoxBrush, palette.BoxForegroundBrush)") &&
            Has(cards, "AutomationProperties.SetHelpText(button, descriptions[index])"), "rating explanations must appear visually and in accessibility help.");
        Test(!optionButton.Contains("AccentButton(", StringComparison.Ordinal) &&
            Has(tools, "button.Background = palette.BoxBrush") && Has(tools, "button.Foreground = palette.BoxForegroundBrush"),
            "utility/rating/choice controls must not all use primary accent fill.");
        Test(Has(Member(source, "StudyMarkButton"), "known ? KnownButton(entry, Refresh) : FavoriteButton(entry, Refresh)"),
            "compact mark controls must retain the real in-place favorite/known actions.");
        Test(Has(cards, "content.Children.Add(StudyWordRow(word, listen))") &&
            Has(cards, "listen.Click += async (_, _) => await PlayWordAsync(entry.Word, listen)") &&
            Regex.Matches(WithoutComments(cards), @"\bPlayWordAsync\(").Count == 1 &&
            !quiz.Contains("PlayWordAsync", StringComparison.Ordinal) && !feedback.Contains("PlayWordAsync", StringComparison.Ordinal),
            "Listen must stay next to the word and speak only that word, never a localized meaning or example in the wrong voice.");
        Test(Has(cards, "tools.Children.Add(StudyMarkButton(entry, known: false))") &&
            Has(cards, "tools.Children.Add(StudyMarkButton(entry, known: true))") && Has(cards, "list.Children.Add(tools)") &&
            Has(cards, "AddCardsHintButton(list, navigation)") && Has(cards, "ConfigureResponsiveGrid(navigation, 5") &&
            Has(cardHelp, "StudyPopupButton(U(\"Kids.Cards.WordList\", \"My word list\", \"Kelime listem\"), wordList, \"cards.WordList\")"),
            "Favorite and Known must remain reachable through a fixed popup rather than crowding or lengthening the recall page.");
        Test(Has(Member(source, "OnStudyKeyDown"), "RequestUiFocus(\"cards.WordList\")"),
            "the U shortcut must return focus to the visible word-list popup button after rebuilding.");
        Test(Has(Member(source, "StudyMarkButton"), "AutomationProperties.SetName(button, label)") &&
            Has(Member(source, "StudyMarkButton"), "AutomationProperties.SetHelpText(button, hint)"),
            "in-place word-list toggles must refresh accessible wording as well as visible state.");

        Test(Has(quiz, "var answered = session.Answers.TryGetValue(answer.Key, out var correctAnswer)") &&
            Has(quiz, "var selected = options.Answers.Keys.FirstOrDefault()"), "answer state and selection must come from saved session state.");
        Test(Has(quiz, "var choice = _words.First(word => word.Key == options.WordKeys[index])") && !quiz.Contains("_random", StringComparison.Ordinal),
            "rendering must preserve the stored option order.");
        Test(QuizCues(quiz), "only the correct option gets a check; the incorrect chosen option gets a cross and explicit text.");
        Test(Has(quiz, "StudyOptionContent(index + 1, label, status, brushes.Background, brushes.Foreground)"), "result cues must reach the rendered option, not just an unused status variable.");
        var readOnly = Block(quiz, "if (answered)");
        Test(Has(readOnly, "choices.Children.Add(StudySurface(content, 14, brushes.Background") &&
            !readOnly.Contains("Click +=", StringComparison.Ordinal) && !readOnly.Contains("_quizChoiceButtons.Add", StringComparison.Ordinal),
            "answered choices must be noninteractive result surfaces, not gray disabled feedback buttons.");
        Test(Has(quiz, "if (answered && !isCorrectChoice && !isWrongSelection) continue") &&
            Has(quiz, "if (!answered) PageContent.Children.Add(StudyPopupButton"),
            "answered feedback must retain only the correct/selected tiles and remove redundant help to fit one screen.");
        Test(Has(readOnly, "AutomationProperties.SetName(label, $\"{index + 1}: {choice.Word}\")") &&
            Has(readOnly, "AutomationProperties.SetAutomationId(label, $\"quiz.Choice.{index}\")") &&
            Has(quiz, "AutomationProperties.SetName(button, $\"{index + 1}: {choice.Word}\")") &&
            Has(quiz, "FocusTarget(button, $\"quiz.Choice.{index}\")"), "number/name/ID automation contracts must survive the answer transition.");
        Test(Has(quiz, "button.IsEnabled = !answered") && Has(quiz, "_quizChoiceButtons.Clear()"), "only current unanswered options may be keyboard-answerable.");
        Test(ExplicitContinue(quiz, answer, next), "Continue must be explicitly clicked/keyed and gated on a persisted answer; answering must never auto-advance.");
        Test(Has(answer, "if (session.Answers.ContainsKey(key) || options is null || !options.WordKeys.Contains(selected)) return") &&
            Has(answer, "options.Answers[selected] = correct"), "duplicate and invalid selection guards/persisted selection must remain intact.");
        Test(Has(answer, "var correct = key == selected") &&
            Has(answer, "if (!LearningEngine.RecordQuizResponse(_progress, session, key, correct, Today)) return") &&
            Has(answer, "options.Index = options.WordKeys.Count"),
            "encouraging feedback must not turn a wrong choice into a correct result or bypass real attribution.");
        Test(Has(save, "await _storage.SaveProgressAsync(_progress)") &&
            Has(save, "_progress = JsonSerializer.Deserialize<ProgressState>(before)!") && Has(save, "return false"),
            "failed saves must roll back answer state rather than presenting a successful answer.");
        Test(Has(quiz, "if (answered) AddQuizFeedback(") && Has(quiz, "if (answered) AutomationProperties.SetHelpText(_quizContinue, feedbackMessage)"),
            "feedback and its Continue announcement must only render for persisted answers.");
        Test(Has(quiz, "correctAnswer ? $\"✓ {U(\"Kids.Quiz.GotIt\", \"You got it!\", \"Bildin!\")}") &&
            Has(quiz, ": $\"{U(\"Kids.Quiz.GoodTry\", \"Good try!\", \"Güzel deneme!\")}") &&
            quiz.Contains("U(\"Kids.Quiz.AnswerWord\", \"Here is the word:\",", StringComparison.Ordinal) &&
            !quiz.Contains("T(\"Common.Wrong\")", StringComparison.Ordinal),
            "feedback must encourage both attempts without calling a wrong answer correct or adding a shame cross to the message.");
        Test(Has(quiz, "AddQuizFeedback(answer, _words.FirstOrDefault(word => word.Key == selected), correctAnswer, feedbackMessage, _quizContinue)") &&
            Has(quiz, "else PageContent.Children.Add(_quizContinue)") && Has(feedback, "feedback.Children.Add(status)") &&
            Has(feedback, "feedback.Children.Add(continueButton)") &&
            feedback.IndexOf("feedback.Children.Add(continueButton)", StringComparison.Ordinal) < feedback.IndexOf("feedback.Children.Add(lookAtAnswer)", StringComparison.Ordinal),
            "answered Continue must be a full-width primary action above optional details, with no automatic advance.");
        var questionPosition = quiz.IndexOf("question.Children.Add(meaning)", StringComparison.Ordinal);
        Test(questionPosition >= 0 && questionPosition < quiz.IndexOf("if (answered)", StringComparison.Ordinal) &&
            Has(quiz, "StudyText(LocalizedPart(answer.Definition), 26, emphasis: true, selectable: true)") && Has(quiz, "PageContent.Children.Add(StudySurface(question))"),
            "the question must be visible before any answer, with its full meaning inside the standalone surface.");
        Test(Has(quiz, "StudyLabel(U(\"Kids.Quiz.ChooseWord\", \"Choose a word\", \"Bir kelime seç\"))") &&
            Has(quiz, "if (!answered) question.Children.Add(StudyLabel(U(\"Kids.Quiz.ChooseOne\", \"Choose one answer.\", \"Bir yanıt seç.\")))") &&
            Has(quiz, "StudyText(choice.Word, 24, brushes.Foreground, emphasis: true)"),
            "the prompt, helper, and full-size answer words must make the click action clear before answering.");
        var wrongComparison = Block(feedback, "if (!correctAnswer)");
        Test(wrongComparison.Contains("LocalizedPart(selectedWord.Definition)", StringComparison.Ordinal) &&
            wrongComparison.Contains("LocalizedPart(answer.Definition)", StringComparison.Ordinal) &&
            Regex.Matches(feedback, @"\.Definition\b").Count == Regex.Matches(wrongComparison, @"\.Definition\b").Count,
            "compare both meanings only when wrong; correct feedback must not duplicate the question/definition.");
        Test(Regex.Matches(feedback, @"StudyDetail\(T\(""Cards\.Example""\)").Count == 1,
            "feedback must have one example section, not duplicate selected/correct examples.");
        Test(Has(feedback, "if (!string.IsNullOrWhiteSpace(answer.Example)) details.Children.Add(StudyDetail(T(\"Cards.Example\"), answer.Example, 20, brushes.Foreground))") &&
            Has(feedback, "StudyPopupButton(U(\"Kids.Quiz.AnswerDetails\", \"Look at the answer\", \"Yanıta bakalım\"), details, \"quiz.AnswerDetails\")"),
            "an available example must remain readable inside optional popup details; absent examples must not be invented.");
        Test(Has(feedback, "StudyLive(status, \"quiz.Feedback\")") && Has(cards, "StudyLive(step, \"cards.Step\")") &&
            Has(Member(source, "StudyLive"), "IsWithin(text, PageContent)") && Has(Member(source, "StudyLive"), "Announce(text, text.Text)"),
            "live study cues must announce only their attached current text.");

        foreach (var fragment in new[] { "FontSize = ReadingSize(size)", "LineHeight = ReadingSize(size) * 1.4",
            "LineStackingStrategy = LineStackingStrategy.MaxHeight", "FontStyle = Windows.UI.Text.FontStyle.Normal",
            "TextAlignment = TextAlignment.Left", "TextWrapping = TextWrapping.Wrap", "TextTrimming = TextTrimming.None",
            "TextLineBounds = TextLineBounds.Full", "HorizontalAlignment = HorizontalAlignment.Stretch" })
            Test(Has(text, fragment), "full-text sizing/readability contract: " + fragment);
        Test(!Regex.IsMatch(WithoutComments(text), @"ActualWidth|ActualHeight|FontScale|\bFont\(") &&
            !source.Contains("IsTextScaleFactorEnabled = false", StringComparison.Ordinal),
            "study text must use the shared ReadingSize floor, never viewport-based shrinking or disabled Windows text scaling.");
        Test(Has(readableAction, "button.MinHeight = Math.Max(48, button.MinHeight)") &&
            Has(readableAction, "button.MinWidth = Math.Max(48, button.MinWidth)") &&
            Has(readableAction, "button.FontSize = ReadingSize(18)") && Has(readableAction, "text.FontSize = ReadingSize(18)") &&
            Has(readableAction, "text.LineHeight = ReadingSize(18) * 1.4") && Has(readableAction, "text.TextWrapping = TextWrapping.Wrap") &&
            Has(readableAction, "text.TextTrimming = TextTrimming.None") && Has(tools, "button = ReadableStudyAction(button)"),
            "both accent and neutral study actions must keep at least 48x48 targets and wrapped 18-point button text.");
        Test(Has(optionButton, "button.MinHeight = 56") &&
            Has(optionButton, "CompactStudyAction(SecondaryButton(name, \"\"))") &&
            Has(disclosure, "MinHeight = 48") && Has(disclosure, "MinWidth = 48") &&
            Has(disclosure, "Header = StudyText(title, 18, emphasis: true)"),
            "options need at least 56-point touch height and disclosures need full-size readable headers and targets.");
        Test(Has(Member(source, "SessionStartButton"), "ReadableStudyAction(AccentButton(label, \"\"))") &&
            Has(quiz, "_quizContinue = ReadableStudyAction(AccentButton(") &&
            Has(Member(source, "AddMissedSessionSummary"), "var retry = ReadableStudyAction(AccentButton("),
            "start, Continue and retry must receive the same reading-size/touch-target wrapper as reveal and rated Next.");
        foreach (var body in new[] { cards, quiz, text, surface, feedback, disclosure, option })
            Test(!Regex.IsMatch(WithoutComments(body), @"\b(?:Height|MaxHeight|MaxWidth|MaxLines|Clip|RenderTransform|LayoutTransform|Visibility|Opacity)\s*="),
                "study text/ancestors must not clip, hide, fade, scale or fix text height.");
        Test(Has(surface, "Background = background ?? AppearancePalette.Current.BoxBrush") &&
            Has(text, "Foreground = foreground ?? AppearancePalette.Current.BoxForegroundBrush") && !surface.Contains("ApplyReadableForeground", StringComparison.Ordinal),
            "study surface/text must use a matched palette pair without recursive recoloring.");
        Test(Has(cards, "ConfigureStudyChoices(ratings, 4, 190)") && Has(quiz, "ConfigureStudyChoices(choices, 2, 230)"),
            "real rating and quiz grids must use responsive 4/2/1 and 2/1 layouts.");
        Test(Has(layout, "minimumColumnWidth * Math.Max(1, AppearancePalette.Current.FontScale)") &&
            Has(layout, "? maximumColumns : width >= 2 * minimum + grid.ColumnSpacing ? 2 : 1") &&
            Has(layout, "Height = GridLength.Auto") && Has(layout, "grid.SizeChanged += (_, _) => Reflow()"),
            "reflow must account for text growth without shrinking below the reading floor, balanced columns, automatic row height and live resizing.");
        Test(Has(option, "content.Children.Add(badge)") && Has(option, "number.ToString(CultureInfo.CurrentCulture)") &&
            Has(option, "copy.Children.Add(label)") && Has(option, "StudyText(description, 18, foreground)") &&
            Has(option, "StudyText(number.ToString(CultureInfo.CurrentCulture), 18, background, emphasis: true)"),
            "number badges and full descriptions must be rendered at the same 18-point reading floor, never as tiny labels.");
        Test(Has(wordRow, "row.Children.Add(word)") && Has(wordRow, "row.Children.Add(listen)") &&
            Has(wordRow, "Grid.SetColumnSpan(word, narrow ? 2 : 1)") && Has(wordRow, "Grid.SetRow(listen, narrow ? 1 : 0)") &&
            Has(wordRow, "Height = GridLength.Auto") && Has(wordRow, "row.SizeChanged += (_, _) => Reflow()") &&
            !Regex.IsMatch(WithoutComments(wordRow), @"\b(?:Height|MaxHeight|MaxWidth|MaxLines|Clip|Visibility|Opacity)\s*=\s*(?!GridLength\.Auto\b)\S"),
            "word and Listen must reflow into automatic-height rows when narrow, without constraining the reading text.");
        foreach (var body in new[] { cards, quiz, feedback, cardHelp })
            Test(Regex.Matches(WithoutComments(body), @"\bRowSpacing\s*=\s*(\d+)").All(match =>
                int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) >= 12),
                "study action, choice and disclosure rows must keep at least 12-point spacing when wrapped.");
        var summary = Member(source, "AddMissedSessionSummary");
        Test(Has(summary, "detailHost.Content = wordDetail") && Has(summary, "StudyPopupButton(missedLabel, details,") &&
            Has(Member(source, "AddCardsHintButton"), "StudyPopupButton("), "missed details and secondary help must use page-sized popups, not permanent walls of text.");
        Test(Has(summary, "SessionStartButton(mode, restart: true, secondary: missed.Length > 0)") &&
            Has(summary, "StartStudySessionAsync(mode, missed, isRetry: true)"), "completion must prioritize finite missed practice over starting a fresh session.");
        Test(Has(summary, "U(\"Kids.Session.Complete\", \"Great practice!\", \"Güzel çalıştın!\")") &&
            Has(summary, "U(\"Kids.Session.Practiced\", \"You practiced {0} words.\", \"{0} kelime çalıştın.\"), session.Answers.Count") &&
            Has(summary, "StudyPopupButton(U(\"Kids.Session.Details\", \"More details\", \"Daha fazla bilgi\"), result, \"quiz.Results\")") &&
            Has(summary, "var correct = session.Answers.Count(answer => answer.Value)") &&
            Has(summary, "100.0 * correct / session.Answers.Count"),
            "completion must celebrate real practice first and keep unchanged quiz percentages in optional details.");
        Test(Has(cardHelp, "help.Children.Add(StudyText(U(\"Kids.Cards.ClickHelp\"") &&
            Has(cardHelp, "help.Children.Add(StudyText(U(\"Kids.Cards.Shortcuts\"") &&
            Has(quiz, "help.Children.Add(StudyText(U(\"Kids.Quiz.ClickHelp\"") &&
            Has(quiz, "help.Children.Add(StudyText(U(\"Kids.Quiz.Shortcuts\"") &&
            Has(cardHelp, "StudyPopupButton(U(\"Kids.Study.Help\"") && Has(quiz, "StudyPopupButton(U(\"Kids.Study.Help\""),
            "help must explain clicking first and keep keyboard tips together in one optional no-scroll popup.");
        Test(Has(popup, "button.Flyout = flyout") && Has(popup, "surface.MaxWidth = Math.Max(280, Math.Min(620, width - 64))") &&
            Has(popup, "FocusTarget(button, id)"), "optional study details must use a viewport-bounded, keyboard-focusable popup.");
        Test(!cards.Contains("_settings.Level", StringComparison.Ordinal) && !quiz.Contains("_settings.Level", StringComparison.Ordinal) &&
            Has(Member(source, "AddSessionStart"), "U(\"Kids.Cards.StartHint\"") &&
            Has(Member(source, "AddSessionStart"), "U(\"Kids.Quiz.StartHint\""),
            "study subtitles and start hints must explain the next action rather than repeating CEFR or queue jargon.");
        var startSession = Member(source, "StartStudySessionAsync");
        Test(Has(startSession, "DistinctBy(word => word.Key).Take(mode == \"cards\" ? 20 : 8).ToArray()") &&
            Has(startSession, "LearningEngine.CreateSession(entries.Select(word => word.Key), isRetry)") &&
            Has(startSession, "DistinctBy(word => word.Word).OrderBy(_ => _random.Next()).Take(3)") &&
            Has(startSession, "WordKeys = choices"),
            "shorter start copy must not alter queue length, retry attribution, distractor selection or persisted option order.");
        var progress = Member(source, "AddSessionProgress");
        Test(Has(progress, "Value = session.Answers.Count") && !progress.Contains("session.Index", StringComparison.Ordinal) &&
            cards.Contains("U(\"Cards.Position\",", StringComparison.Ordinal) && quiz.Contains("U(\"Quiz.Position\",", StringComparison.Ordinal),
            "persisted session totals and current position must remain distinct.");
        var keys = Member(source, "OnStudyKeyDown");
        Test(Has(keys, "VirtualKey.Space && !_cardRevealed") && Has(keys, "number is >= 0 and <= 3 && _cardRevealed") &&
            Has(keys, "VirtualKey.Enter && _quizContinue?.IsEnabled == true") && Has(keys, "e.KeyStatus.WasKeyDown"),
            "reviewer shortcuts and explicit quiz continuation must retain repeat/input guards.");
        var settings = Member(source, "ChangeQuickSettingAsync");
        Test(Has(settings, "_navigationBusy = true; SyncAccountStatus();") &&
            Has(settings, "finally { _navigationBusy = false; SyncAccountStatus(); }"), "quick-setting account status must synchronize on both busy transitions.");

        var colors = Regex.Matches(Member(source, "StudyResultBrushes"), @"Color\.FromArgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)")
            .Select(match => match.Groups.Cast<Group>().Skip(1).Select(group => int.Parse(group.Value, CultureInfo.InvariantCulture)).ToArray()).ToArray();
        Test(colors.Length == 8, "review result brush mappings if the four light/dark background/foreground pairs change shape.");
        if (colors.Length == 8)
        {
            foreach (var pair in new[] { (0, 2), (1, 3), (4, 6), (5, 7) })
                Test(colors[pair.Item1][0] == 255 && colors[pair.Item2][0] == 255 && Contrast(colors[pair.Item1], colors[pair.Item2]) >= 7,
                    "actual success/retry text and backgrounds must be opaque with at least 7:1 contrast in both themes.");
            Test(colors[4][2] > colors[4][3] && colors[5][2] > colors[5][3],
                "wrong-selection backgrounds must use the warm retry treatment, not the former pink/red error treatment.");
        }

        var kidsCalls = Regex.Matches(WithoutComments(source), @"\bU\(\s*""(Kids\.[^""]+)""\s*,\s*""((?:\\.|[^""\\])*)""\s*,\s*""((?:\\.|[^""\\])*)""\s*\)");
        Test(kidsCalls.Count > 0 && kidsCalls.Count == Regex.Matches(WithoutComments(source), @"\bU\(\s*""Kids\.").Count &&
            kidsCalls.All(call => !string.IsNullOrWhiteSpace(call.Groups[2].Value) && !string.IsNullOrWhiteSpace(call.Groups[3].Value)),
            "every new Kids key must have literal, nonempty English and Turkish fallbacks for the resource handoff.");
        Test(kidsCalls.GroupBy(call => call.Groups[1].Value).All(group =>
            group.Select(call => (call.Groups[2].Value, call.Groups[3].Value)).Distinct().Count() == 1),
            "reused Kids keys must not carry conflicting visible copy in different study states.");

        Test(!CardsGated(cards.Replace("_cardRevealed && !rated", "true", StringComparison.Ordinal)), "rating gate audit must reject an ungated real renderer.");
        Test(!StagedRatings(cards.Replace("if (_cardRevealed || rated) PageContent.Children.Add(ratings)", "PageContent.Children.Add(ratings)", StringComparison.Ordinal)),
            "the staged-rating audit must reject attaching all rating controls before reveal.");
        Test(!OrderedRatingCopy(cards.Replace("\"Kids.Rating.Again\"", "\"Kids.Rating.Good\"", StringComparison.Ordinal),
            "labels", "", new[] { "Help me", "Hard", "I knew it", "Easy!" }),
            "the label-mapping audit must reject a child-friendly label routed to the wrong rating.");
        Test(!QuizCues(quiz.Replace("choice.Key == answer.Key", "choice.Key == selected", StringComparison.Ordinal)), "cue audit must reject the original check-on-any-selected defect.");
        Test(!ExplicitContinue(quiz.Replace("_quizContinue.IsEnabled = answered", "_quizContinue.IsEnabled = true", StringComparison.Ordinal), answer, next),
            "Continue audit must reject premature enabling.");
        Test(!Has("// button.IsEnabled = _cardRevealed && !rated;", "button.IsEnabled = _cardRevealed && !rated"), "a comment must not satisfy a contract.");
        Console.WriteLine($"STUDY_UX checks={assertions} errors={failures}");
    }

    private static bool CardsGated(string body) => Has(body, "var rating = (RecallRating)index") &&
        Has(body, "button.IsEnabled = _cardRevealed && !rated") && Has(body, "button.Click += async (_, _) => await RateCardAsync(rating)");

    private static bool StagedRatings(string body) => Has(body, "if (_cardRevealed || rated) PageContent.Children.Add(ratings)") &&
        Regex.Matches(WithoutComments(body), @"PageContent\.Children\.Add\(ratings\)").Count == 1;

    private static bool OrderedRatingCopy(string body, string array, string suffix, string[] english)
    {
        var declaration = Regex.Match(WithoutComments(body), @"string\[\]\s+" + Regex.Escape(array) + @"\s*=\s*\[([\s\S]*?)\];");
        if (!declaration.Success) return false;
        var calls = Regex.Matches(declaration.Groups[1].Value, @"U\(""Kids\.Rating\.([^""]+)"",\s*""([^""]+)"",\s*""([^""]+)""\)");
        return calls.Count == 4 && calls.Select(call => call.Groups[1].Value)
            .SequenceEqual(new[] { "Again", "Hard", "Good", "Easy" }.Select(rating => rating + suffix)) &&
            calls.Select(call => call.Groups[2].Value).SequenceEqual(english) &&
            calls.All(call => !string.IsNullOrWhiteSpace(call.Groups[3].Value));
    }

    private static bool QuizCues(string body) => Has(body, "var isCorrectChoice = answered && choice.Key == answer.Key") &&
        Has(body, "var isWrongSelection = answered && choice.Key == selected && !isCorrectChoice") &&
        Has(body, "isCorrectChoice ? \"✓ \" + U(\"Kids.Quiz.CorrectChoice\"") && Has(body, "isWrongSelection ? \"✕ \" + U(\"Kids.Quiz.YourChoice\"");

    private static bool ExplicitContinue(string quiz, string answer, string next) => Has(quiz, "_quizContinue.IsEnabled = answered") &&
        Has(quiz, "_quizContinue.Click += async (_, _) => await ContinueQuizAsync()") && Has(quiz, "FocusTarget(_quizContinue, \"quiz.Continue\")") &&
        !answer.Contains("ContinueQuizAsync", StringComparison.Ordinal) && !Regex.IsMatch(answer, @"session\.Index\s*(?:\+\+|=)") &&
        Has(next, "!session.Answers.ContainsKey(session.WordKeys[session.Index])) return") && Has(next, "await MutateStudyAsync(() => session.Index++)");

    private static string WithoutComments(string source) => Regex.Replace(source, @"(?m)^[ \t]*//[^\r\n]*|/\*[\s\S]*?\*/", "");
    private static string Compact(string source) => Regex.Replace(WithoutComments(source), @"\s+", "");
    private static bool Has(string source, string fragment) => Compact(source).Contains(Compact(fragment), StringComparison.Ordinal);

    // Deliberately bounded to class-indented private members; unsupported/duplicate declarations fail closed.
    private static string Member(string source, string name)
    {
        source = WithoutComments(source);
        var matches = Regex.Matches(source, @"(?m)^    private [^\r\n]*\b" + Regex.Escape(name) + @"\s*\(");
        if (matches.Count != 1) throw new InvalidOperationException($"Study UX: expected one private {name} member, found {matches.Count}.");
        var start = matches[0].Index;
        var tailStart = start + matches[0].Length;
        var next = Regex.Match(source[tailStart..], @"(?m)^    private ");
        return source[start..(next.Success ? tailStart + next.Index : source.LastIndexOf('}'))];
    }

    private static string Block(string source, string condition)
    {
        var matches = Regex.Matches(source, Regex.Escape(condition) + @"\s*\{");
        if (matches.Count != 1) throw new InvalidOperationException("Study UX: unsupported conditional block: " + condition);
        var start = matches[0].Index + matches[0].Length;
        var depth = 1;
        var quoted = false;
        for (var index = start; index < source.Length; index++)
        {
            if (quoted)
            {
                if (source[index] == '\\') index++;
                else if (source[index] == '"') quoted = false;
            }
            else if (source[index] == '"') quoted = true;
            else if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0) return source[start..index];
        }
        throw new InvalidOperationException("Study UX: unbalanced conditional block: " + condition);
    }

    private static double Contrast(int[] first, int[] second)
    {
        static double Channel(int value) { var x = value / 255.0; return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
        static double Luminance(int[] color) => 0.2126 * Channel(color[1]) + 0.7152 * Channel(color[2]) + 0.0722 * Channel(color[3]);
        var a = Luminance(first); var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }
}