using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace YDKE_Windows;

/// <summary>Contracts against the current native input source, not a copied game
/// engine or UI fixture. No app launch, profile I/O, scoring mutation or resource
/// fallback. The parent test runner registers Verify after translations land.</summary>
internal static class GameInputUxContracts
{
    public static void Verify(string sourceDirectory, Action<bool, string> check)
    {
        var assertions = 0;
        var failures = 0;
        void Test(bool condition, string message)
        {
            assertions++;
            if (!condition) failures++;
            check(condition, "Game input UX: " + message);
        }
        void Has(string body, string fragment, string message) => Test(Compact(body).Contains(Compact(fragment), StringComparison.Ordinal), message);
        void Before(string body, string first, string second, string message)
        {
            var code = Compact(body);
            var a = code.IndexOf(Compact(first), StringComparison.Ordinal);
            var b = code.IndexOf(Compact(second), StringComparison.Ordinal);
            Test(a >= 0 && b > a, message);
        }

        var path = Path.Combine(sourceDirectory, "MainPage.Games.cs");
        Test(File.Exists(path), "the native Games partial must exist.");
        if (!File.Exists(path)) return;
        var source = File.ReadAllText(path);
        var input = Member(source, "GameInput", "string label");
        Has(source, "private TextBox GameInput(StackPanel panel) =>", "keep the original one-argument input entry point.");
        Has(input, "string? help = null", "the labeled input overload must keep optional help.");
        foreach (var fragment in new[]
        {
            "Header = header", "Description = guidance", "PlaceholderText = T(\"Game.TypeAnswer\")",
            "header.TextAlignment = TextAlignment.Left", "guidance.TextAlignment = TextAlignment.Left",
            "AutomationProperties.SetName(input, label)", "AutomationProperties.SetHelpText(input, help)",
            "AutomationProperties.SetAutomationId(input, \"game.Answer\")", "panel.Children.Add(input)",
        }) Has(input, fragment, "persistent visible/accessibility input contract: " + fragment);
        Test(!Regex.IsMatch(Code(input), @"\b(?:KeyDown|PreviewKeyDown|KeyboardAccelerators)\b|\bAcceptsReturn\s*=\s*true"),
            "input must not intercept the shared Enter shortcut or accept a newline instead.");

        var discovery = Member(source, "GameAnswerInputs");
        foreach (var fragment in new[] { "parent is TextBox input", "panel.Children.Cast<DependencyObject>()",
            "Border { Child:", "ContentControl { Content:", "GameAnswerInputs(child)" })
            Has(discovery, fragment, "discover actual scene inputs before Loaded, including nested containers: " + fragment);
        Test(!Regex.IsMatch(Code(discovery), @"\b(?:IsEnabled|IsLoaded|GameDescendants)\b"),
            "discovery must retain disabled inputs for later unlocks and avoid templated control internals.");
        var available = Member(source, "GameElementAvailable");
        Has(available, "current = VisualTreeHelper.GetParent(current)", "check all ancestors, not only the TextBox IsEnabled.");
        Has(available, "Control { IsEnabled: false } or UIElement { Visibility: Visibility.Collapsed }", "disabled audio hosts and collapsed round ancestors must be unavailable.");
        Has(available, "if (element is null) return false", "a detached shortcut parent must fail closed.");
        var hasAnswer = Member(source, "HasGameAnswer");
        Has(hasAnswer, "if (string.IsNullOrWhiteSpace(input.Text)) return false", "blank and whitespace input must not be scored.");
        Has(hasAnswer, "GameAnswer(input.Text).Any(char.IsLetterOrDigit)", "normalization-empty and symbol-only input must not be scored.");
        Has(hasAnswer, "catch (ArgumentException) { return false; }", "malformed pasted Unicode must fail validation, not scoring.");
        Test(!Regex.IsMatch(Code(hasAnswer), @"\b(?:_words|_progress|ResolveGameAnswerAsync|RecordGameSubAnswerAsync)\b"),
            "input validity must not inspect answer correctness or mutate learning state.");

        var submit = Member(source, "SubmitGame");
        Has(submit, "Func<Button, Task> submit, Func<bool>? canSubmit = null", "keep existing submit calls valid; optional selection predicate is last.");
        Has(submit, "var inputs = GameAnswerInputs(panel).ToArray()", "validate inputs local to this submit, not every page editor.");
        Has(submit, "inputs.Where(input => !input.IsReadOnly && GameElementAvailable(input))", "only currently editable, ancestor-enabled inputs may block submission.");
        var canSubmit = Block(submit, "bool CanSubmit()");
        Has(canSubmit, "inputs.Length == 0 || active.Length > 0", "all disabled text inputs must not pass by vacuous All().");
        Has(canSubmit, "active.All(HasGameAnswer) && (canSubmit?.Invoke() ?? true)", "require actual text and the optional structural predicate.");
        Has(canSubmit, "GameElementAvailable(panel)", "an answer panel under a disabled host must not enable Submit.");
        var refresh = Block(submit, "void Refresh()");
        Has(refresh, "button.IsEnabled = CanSubmit()", "refresh must recompute current input validity.");
        Test(!Regex.IsMatch(Code(canSubmit + refresh), @"\b(?:GameCanAnswer|_gameLocalBusy|_resolvingGame|_studyBusy|_navigationBusy)\b"),
            "initial button state must not latch transient async-render execution gates.");
        foreach (var fragment in new[]
        {
            "input.TextChanged += (_, _) => Refresh()", "input.IsEnabledChanged += (_, _) => Refresh()",
            "input.RegisterPropertyChangedCallback(UIElement.VisibilityProperty", "input.RegisterPropertyChangedCallback(TextBox.IsReadOnlyProperty",
            "button.Loaded += (_, _) => Refresh()", "panel.Children.Add(button); Refresh(); return button;",
        }) Has(submit, fragment, "reactive submit wiring: " + fragment);
        var validation = Block(submit, "bool Validate()");
        Has(validation, "if (CanSubmit())", "the click guard must re-evaluate, not trust a stale button state.");
        Has(validation, "validation.Visibility = Visibility.Visible", "invalid submission must have visible inline guidance.");
        Has(validation, "Announce(validation,", "invalid submission must be announced.");
        Has(validation, "ActiveInputs().FirstOrDefault(input => !HasGameAnswer(input))?.Focus(FocusState.Programmatic)", "invalid submission must focus only an active incomplete input.");
        Has(submit, "Live(validation)", "validation must be a polite live region.");
        Has(submit, "AutomationProperties.SetAutomationId(validation, \"game.Validation\")", "keep a stable inline validation ID.");
        var click = Block(submit, "button.Click += async (_, _) =>");
        Has(click, "!button.IsLoaded || !IsWithin(button, PageContent)", "detached stale controls must not submit.");
        Has(click, "!GameCanAnswer(session) || _gameLocalBusy", "keep actual execution gates at click time.");
        Has(click, "if (!Validate()) { Refresh(); return; }", "invalid input must return before the scoring delegate.");
        Before(click, "if (!Validate())", "await submit(button)", "validation must execute BEFORE the delegate, not after scoring.");
        Test(Regex.Matches(Code(click), @"\bsubmit\s*\(").Count == 1, "one valid click may call the scoring delegate only once.");
        Has(Member(source, "UpdateGameSubmitState"), "if (button.Tag is GameSubmitState state) state.Refresh()", "provide the explicit, local refresh hook for board/host transitions.");

        var keys = Member(source, "GameSceneKeyDown");
        Has(keys, "if (focus is TextBox) editing = true", "a TextBox must not prevent Enter from reaching Submit.");
        Has(keys, "if (e.Key == VirtualKey.Enter)", "preserve Enter submission/Continue.");
        Has(keys, "else if (!editing && !_gameLocalBusy && !_gameRoundClosed)", "number shortcuts must stay out of editable text.");
        Before(keys, "if (e.Key == VirtualKey.Enter)", "else if (!editing", "Enter must be outside the non-editing-only choice branch.");
        foreach (var fragment in new[] { "e.KeyStatus.WasKeyDown", "VirtualKey.Control", "VirtualKey.Menu", "VirtualKey.Shift",
            "ComboBox or Slider or NumberBox or AutoSuggestBox or PasswordBox or RichEditBox", "VirtualKey.Number1", "VirtualKey.NumberPad1",
            "GameElementAvailable(b)", "GameElementAvailable(VisualTreeHelper.GetParent(b))", "state.Validate(); e.Handled = true; return;" })
            Has(keys, fragment, "safe, discoverable keyboard behavior: " + fragment);
        Before(keys, "AutomationProperties.GetAutomationId(b) == \"game.Continue\"", "AutomationProperties.GetName(b) == T(\"Game.Submit\")", "feedback Next must take precedence over Submit regardless of translated label.");
        Has(keys, "tag == $\"game-choice-{n}\"", "preserve the stable 1–4 choice routing tags.");
        var choices = Member(source, "GameChoices");
        foreach (var fragment in new[] { "GameChoiceButton($\"{i + 1} · {labels[i]}\", session.Game)", "button.Tag = $\"game-choice-{i}\"",
            "AutomationProperties.SetHelpText(button, shortcut)", "AutomationProperties.SetAcceleratorKey(button,", "ToolTipService.SetToolTip(button, shortcut)" })
            Has(choices, fragment, "neutral choice shortcut affordance: " + fragment);
        Test(!Regex.IsMatch(Code(choices), @"\b(?:Background|Foreground|BorderBrush)\s*=|\b(?:correct|target)\b"), "choice appearance must not identify the answer before submission.");

        foreach (var renderer in new[] { "RenderAudioGame", "RenderClozeGame", "RenderCategoryGame", "RenderClueGame", "RenderRackGame", "RenderCrosswordGame" })
        {
            var body = Member(source, renderer);
            Has(body, "GameInput(", renderer + " must use persistent labeled inputs.");
            Has(body, "SubmitGame(", renderer + " must use shared pre-scoring validation.");
            Test(!Regex.IsMatch(Code(body), @"\bnew\s+TextBox\b"), renderer + " must not bypass the shared input helper.");
        }
        foreach (var renderer in new[] { "RenderAudioGame", "RenderSentenceGame", "RenderRackGame", "RenderClueGame" })
        {
            var body = Member(source, renderer);
            Has(body, "AddGameQuestion(panel, GameTask(session.Game))", renderer + " must show a short task, not a 26-point rules wall.");
            Test(!Compact(body).Contains("AddGameQuestion(panel,GameInstructions(", StringComparison.Ordinal), renderer + " still uses full instructions as a question.");
        }
        var audio = Member(source, "RenderAudioGame");
        Has(audio, "Content = answers, IsEnabled = false", "audio answers must start locked.");
        Has(audio, "Games.Audio.PlayFirst", "explain how to unlock audio answers.");
        Has(audio, "}, () => played)", "dictation submit must also require successful playback.");
        Has(audio, "answersHost.IsEnabled = played; _gameLocalBusy = false; if (audioSubmit is not null) UpdateGameSubmitState(audioSubmit)", "refresh after playback unlocks the ancestor host.");

        var sentence = Member(source, "RenderSentenceGame");
        Has(sentence, "() => selected.Count == tokens.Length", "sentence Submit must require every tile, not a silently ignored partial selection.");
        Has(sentence, "undo.IsEnabled = false", "empty sentence Undo must start disabled.");
        Has(sentence, "undo.IsEnabled = selected.Count > 0", "Undo state must follow the current selection.");
        Has(sentence, "selected.RemoveAt(selected.Count - 1); RefreshSelection()", "Undo must refresh assembled text, count and Submit.");
        Has(sentence, "selected.Add(index); button.IsEnabled = false; RefreshSelection()", "each selected tile must update the same live state.");
        Has(sentence, "{selected.Count} / {tokens.Length}", "show the required tile count without revealing their order.");
        Has(sentence, "GameEngine.SentenceKey(assembled.Text) == GameEngine.SentenceKey(sentence)", "retain exact sentence scoring.");
        Test(!Regex.IsMatch(Code(Block(sentence, "void RefreshSelection()")), @"\bsentence\b"), "live assembled text must never substitute the correct sentence.");

        var matrix = Member(source, "RenderMatrixGame");
        Has(matrix, "() => selected.Count == target.Length && GameEngine.StraightSelection(selected, width)", "Matrix submit validity must be length plus straight geometry, not correct letters.");
        Has(matrix, "GameEngine.StraightSelection(selected.Append(index).ToArray(), width)", "disable bent, disjoint or row-wrapped extensions before selection.");
        Has(matrix, "selected.Count < target.Length && !selected.Contains(index)", "new squares may not exceed length or reuse a square.");
        Has(matrix, "selected.RemoveRange(previous, selected.Count - previous)", "selected squares must support backtracking without an attempt.");
        Has(matrix, "buttons[i].IsEnabled = chosen || CanExtend(i)", "keep backtracking available even with a full path.");
        Has(matrix, "selected.Clear(); RefreshSelection()", "Clear must restore a new selection at any point.");
        Has(matrix, "{selected.Count} / {target.Length}", "show selected/required letters separately from user-selected text.");
        Has(matrix, "AutomationProperties.SetHelpText(board, selectionHelp)", "keep full Matrix instructions available without a visible scrolling paragraph.");
        Has(matrix, "ConfigureResponsiveGrid(status, 2, 150)", "selected letters and count must share one compact status row.");
        Has(matrix, "button.HorizontalContentAlignment = HorizontalAlignment.Center", "center the matrix cell content.");
        Has(matrix, "label.TextAlignment = TextAlignment.Center", "center labels even when the shared choice helper wraps them.");
        Has(matrix, "GameEngine.StraightSelection(selected, width) && selection.Text == target", "retain answer scoring after structural validation.");
        var extend = Regex.Match(Code(matrix), @"\bbool\s+CanExtend\([^;]*;").Value.Replace("target.Length", "", StringComparison.Ordinal);
        Test(extend.Length > 0 && !Regex.IsMatch(extend, @"\b(?:target|letters|entry)\b"), "clickable Matrix paths may inspect target length only, never hidden answer characters.");

        var crossword = Member(source, "RenderCrosswordGame");
        foreach (var fragment in new[] { "Games.Crossword.EntryOrder", "{acrossLabel} · {a.Length}", "{downLabel} · {d.Length}", "down.IsEnabled = false",
            "var entry = acrossSolved ? crossing.Down : crossing.Across", "var input = acrossSolved ? down : across",
            "_gameHintProvider = () => WordHint(acrossSolved ? crossing.Down : crossing.Across)",
            "if (!await RecordGameSubAnswerAsync(session, correct, b, [entry.Key]) || !IsCurrentGameRound(session, epoch)) return;",
            "acrossSolved = true; across.IsEnabled = false; down.IsEnabled = true", "UpdateGameSubmitState(b)",
            "down.Focus(FocusState.Programmatic)", "input.Focus(FocusState.Programmatic)", "answerAlreadyRecorded: true" })
            Has(crossword, fragment, "independent active-entry crossword contract: " + fragment);
        Before(crossword, "if (!await RecordGameSubAnswerAsync", "acrossSolved = true", "do not switch entries before the atomic save accepts Across.");
        Before(crossword, "if (!await RecordGameSubAnswerAsync", "acrossCard.Visibility = Visibility.Collapsed", "compact Across only after its accepted score.");
        Has(crossword, "UpdateGameSubmitState(b); down.Focus(FocusState.Programmatic)", "refresh once the round is visible again, before focusing Down.");
        Test(!Regex.IsMatch(Code(Block(crossword, "void Update()")), @"\b(?:Foreground|Background|Brush|GameEngine|correct)\b"), "typed crossword patterns must not disclose correctness through color before scoring.");

        var reading = Member(source, "RenderLocalDataGame");
        foreach (var fragment in new[] { "AutomationProperties.SetAutomationId(pageStatus, \"game.Reading.Page\")", "Announce(pageStatus,", "Announce(text, pages[page])",
            "AddGameQuestion(questionPanel, question.Prompt)", "questionPanel.Visibility = Visibility.Visible", "questionPanel.Children.Add(reread)",
            "question.Explanation, b, []", "content.Children.Add(questionPanel)" })
            Has(reading, fragment, "full reading text, separate page status, question/reread and unchanged explanation: " + fragment);
        Has(reading, "current.Length + token.Length > 120", "reading passages must use short no-scroll text pages at compact widths.");
        Has(reading, "ConfigureResponsiveGrid(titleRow, 2, 180)", "reading title and page count must share one compact row.");
        Test(!Regex.IsMatch(Code(reading), @"\b(?:Viewbox|MaxHeight|MaxLines|Clip)\b"), "reading text/questions must not be clipped or scaled to fit.");

        var feedback = Member(source, "PauseGameFeedbackAsync");
        Has(feedback, "PauseGameFeedbackAsync(GameSession session, string message)", "keep the string-only feedback signature used by the source probe.");
        foreach (var fragment in new[] { "var text = GameFeedbackText(feedback, message); Live(text)", "AutomationProperties.SetAutomationId(next, \"game.Continue\")",
            "next.Focus(FocusState.Programmatic)", "Announce(text, message)", "new TaskCompletionSource<bool>()", "var continued = await completion.Task",
            "feedback.Unloaded += OnUnloadedFeedback", "feedback.Unloaded -= OnUnloadedFeedback", "completion.TrySetResult(false)", "_gameLocalBusy = false" })
            Has(feedback, fragment, "preserve accessible explicit Continue and feedback lifecycle: " + fragment);
        Test(!Regex.IsMatch(Code(feedback), @"\b(?:Delay|DispatcherTimer|WaitAsync|RenderGameRound)\b"), "feedback must not auto-close or auto-advance.");
        Console.WriteLine($"GAME_INPUT_UX checks={assertions} errors={failures} source-only=true profile-io=none");
    }

    private static string Compact(string text) => Regex.Replace(SourceAudit.WithoutComments(text), @"\s+", "");

    // Read complete current members at their declaring indentation; local handlers
    // and functions remain inside the audited member rather than becoming fixtures.
    private static string Member(string source, string name, string signaturePart = "")
    {
        var code = Code(source);
        var matches = Regex.Matches(code, @"(?m)^    private\b[^\r\n]*?\b" + Regex.Escape(name) + @"\s*\([^\r\n]*")
            .Where(m => m.Value.Contains(signaturePart, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException($"Expected one real {name} member ({signaturePart}), found {matches.Length}.");
        var start = matches[0].Index;
        var next = Regex.Match(code[(start + 1)..], @"(?m)^    private\b");
        return source[start..(next.Success ? start + 1 + next.Index : source.Length)];
    }

    private static string Block(string source, string marker)
    {
        var code = Code(source);
        var markerAt = code.IndexOf(marker, StringComparison.Ordinal);
        if (markerAt < 0) throw new InvalidOperationException("Missing real block: " + marker);
        var start = code.IndexOf('{', markerAt + marker.Length);
        if (start < 0) throw new InvalidOperationException("Missing real block body: " + marker);
        var depth = 1;
        for (var i = start + 1; i < code.Length; i++)
        {
            if (code[i] == '{') depth++;
            if (code[i] == '}' && --depth == 0) return source[(start + 1)..i];
        }
        throw new InvalidOperationException("Unbalanced real block: " + marker);
    }

    // Offset-preserving lexical mask. Interpolated strings can contain calls with
    // quoted localization arguments, so a flat quoted-string regex is insufficient.
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
        throw new InvalidOperationException("Unterminated literal in real game source.");
    }
}