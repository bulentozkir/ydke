using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI;

namespace YDKE_Windows;

public sealed partial class MainPage
{
    private const int QuizQuestionsPerExam = QuizExamEngine.QuestionsPerExam;
    private const int QuizChoiceCount = 4;
    private const string QuizChoiceCustomPrefix = "custom:";
    private static readonly string[] QuizChoicePalettes = { "classic", "meadow", "sunset", "slate" };

    // Localization handoff: each U call is the key / English / Turkish manifest.
    // Non-Turkish UI languages temporarily use English, never missing-key labels.
    private string U(string key, string english, string turkish) => ExperienceStrings.Get(_settings.UiLanguage, key, english, turkish);
    private string StudyContext => $"{_settings.StudyLanguage}:{_settings.Level}";
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
    private static string DayKey(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private string SessionKey(string mode) => $"{StudyContext}:{mode}";
    private StudySessionState? Session(string mode) => _progress.Sessions.GetValueOrDefault(SessionKey(mode));
    private bool _studyBusy;
    private bool _syncingSettings;
    private bool _navigationBusy;
    private bool _dialogOpen;
    private TaskCompletionSource<bool>? _dialogClosed;
    private GameSession? _resolvingGame;
    private GameSession? _completingGame;
    private bool _cardRevealed;
    private string? _revealedCardKey;
    private string? _cardUndo;
    private string? _cardUndoContext;
    private AutoSuggestBox? _cardsSearchBox;
    private string _cardsSearchQuery = "";
    private readonly List<Button> _quizChoiceButtons = [];
    private Button? _quizContinue;
    private int _quizExamPage;
    private int _quizExamPageSize = 15;
    private int _quizExamColumns = 5;
    private int? _quizExamIndex;
    private string? _quizExamContext;
    private long _focusVersion;
    private (long Version, string Page, string Context, string Id, DependencyObject? Source)? _pendingFocus;

    // Request BEFORE rebuilding. Loaded + token/context/attachment checks reject stale
    // renders; a user who has since moved focus (especially into an editor) wins.
    private void RequestUiFocus(string id, string? page = null)
    {
        _pendingFocus = (++_focusVersion, page ?? _currentPage, StudyContext, id,
            FocusManager.GetFocusedElement(XamlRoot) as DependencyObject);
    }

    private static bool IsWithin(DependencyObject? element, DependencyObject parent)
    {
        for (; element is not null; element = VisualTreeHelper.GetParent(element))
            if (ReferenceEquals(element, parent)) return true;
        return false;
    }

    private void FocusTarget(Control control, string id)
    {
        AutomationProperties.SetAutomationId(control, id);
        if (_pendingFocus is not { } pending || pending.Id != id) return;
        void Restore()
        {
            if (_pendingFocus?.Version != pending.Version) return;
            if (_currentPage != pending.Page || StudyContext != pending.Context) { _pendingFocus = null; return; }
            if (_studyBusy || _navigationBusy || _dialogOpen || !control.IsLoaded || !control.IsEnabled || !IsWithin(control, PageContent)) return;
            var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            for (var item = focused; item is not null; item = VisualTreeHelper.GetParent(item))
                if (item is TextBox or PasswordBox or RichEditBox or AutoSuggestBox or ComboBox or NumberBox)
                { _pendingFocus = null; return; }
            if (focused is not null && !ReferenceEquals(focused, pending.Source) &&
                !ReferenceEquals(focused, control) && IsWithin(focused, this) &&
                focused is Control && !ReferenceEquals(focused, this))
            { _pendingFocus = null; return; }
            _pendingFocus = null;
            control.Focus(FocusState.Programmatic);
        }
        if (control.IsLoaded) DispatcherQueue.TryEnqueue(Restore);
        else
        {
            RoutedEventHandler? loaded = null;
            loaded = (_, _) => { control.Loaded -= loaded; DispatcherQueue.TryEnqueue(Restore); };
            control.Loaded += loaded;
        }
    }

    private void RequestSessionFocus(string mode)
    {
        var session = mode == "quiz" ? SelectedQuizSession() : Session(mode);
        var startId = mode == "quiz" ? QuizExamFocusId(_quizExamPage * _quizExamPageSize) : $"{mode}.Start";
        if (mode == "quiz")
        {
            RequestUiFocus(session is null ? startId : session.Index >= session.WordKeys.Count ? "quiz.RestartExam"
                : session.Answers.ContainsKey(session.WordKeys[session.Index]) ? "quiz.Continue" : "quiz.Choice.0", mode);
            return;
        }
        var id = session is null ? startId : session.Index >= session.WordKeys.Count
            ? LearningEngine.MissedKeys(session).Count > 0 ? $"{mode}.RetryMissed" : startId
            : mode == "cards" ? session.Answers.ContainsKey(session.WordKeys[session.Index]) ? "cards.Next" : "cards.Reveal"
            : session.Answers.ContainsKey(session.WordKeys[session.Index]) ? "quiz.Continue" : "quiz.Choice.0";
        RequestUiFocus(id, mode);
    }

    private bool _storageBlocked;

    // A reload-required error means old in-memory state may already be stale/mixed.
    // Stop every interaction and require restart (which recovers via LoadStateAsync)
    // rather than risk resuming with data that no longer matches disk.
    private void BlockStorageUntilRestart(Exception error)
    {
        _storageBlocked = true;
        StopGameTimer();
        StopSpeechPlayback();
        _wordLoadCancellation?.Cancel();
        _activeGame = null;
        _cardUndo = null;
        _cardUndoContext = null;
        _revealedCardKey = null;
        _pendingFocus = null;
        _quizChoiceButtons.Clear();
        _quizContinue = null;
        SetStudyBusy(true);
        Notice.IsClosable = false;
        ShowNotice(T("Common.Error"), error.Message, InfoBarSeverity.Error);
    }

    private void SetStudyBusy(bool busy)
    {
        busy |= _storageBlocked;
        _studyBusy = busy;
        ContentScroll.IsEnabled = !busy && !LoadingRing.IsActive;
        PageContent.IsHitTestVisible = !busy && !LoadingRing.IsActive;
        ApplyQuickSettingsAvailability();
        Navigation.IsPaneToggleButtonVisible = !busy;
    }

    // UI-thread snapshots: persistence includes answers + counters + position in one progress file.
    // Failed saves restore memory too; no duplicate action is left waiting to be retried.
    private async Task<bool> MutateStudyAsync(Action mutation, bool keepUndo = false)
    {
        if (_studyBusy || _dialogOpen || _navigationBusy) return false;
        SetStudyBusy(true);
        var before = JsonSerializer.Serialize(_progress);
        try
        {
            mutation();
            await _storage.SaveProgressAsync(_progress);
            if (!keepUndo) { _cardUndo = null; _cardUndoContext = null; }
            return true;
        }
        catch (ImportReloadRequiredException ex)
        {
            BlockStorageUntilRestart(ex);
            return false;
        }
        catch (Exception ex)
        {
            _progress = JsonSerializer.Deserialize<ProgressState>(before)!;
            ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error);
            return false;
        }
        finally { SetStudyBusy(false); }
    }

    private VocabularyEntry[] CardsDeckPool() => _words
        .Where(word => word.LanguageCode == _settings.StudyLanguage && word.Level == _settings.Level)
        .DistinctBy(word => word.Key)
        .OrderBy(word => word.Word, StringComparer.OrdinalIgnoreCase)
        .ThenBy(word => word.Key, StringComparer.Ordinal)
        .ToArray();

    private VocabularyEntry[] QuizExamPool() => QuizExamEngine.CreatePool(_words, _settings.StudyLanguage, _settings.Level);

    private static string QuizExamFocusId(int examIndex) => $"quiz.Exam.{examIndex + 1}";

    private StudySessionState? SelectedQuizSession() => _quizExamIndex is { } index && _quizExamContext == StudyContext
        ? _progress.Sessions.GetValueOrDefault(QuizExamEngine.SessionKey(StudyContext, index)) : null;

    private StudySessionState? QuizOptions(int questionIndex) => _quizExamIndex is { } index && _quizExamContext == StudyContext
        ? _progress.Sessions.GetValueOrDefault(QuizExamEngine.OptionsKey(StudyContext, index, questionIndex)) : null;

    // Selection is navigation state, not saved progress. Every entry to Quiz starts
    // at the islands; an old global :quiz session must never bypass the picker.
    private void ResetQuizSelection()
    {
        _quizExamIndex = null;
        if (_quizExamContext != StudyContext) _quizExamPage = 0;
        _quizExamContext = StudyContext;
        _quizChoiceButtons.Clear();
        _quizContinue = null;
    }

    private void ShowQuizExamPicker()
    {
        if (_studyBusy || _navigationBusy || _dialogOpen || _storageBlocked) return;
        ResetQuizSelection();
        RequestSessionFocus("quiz");
        if (_currentPage != "quiz") NavigateTo("quiz", QuizItem);
        else RenderQuiz();
    }

    private Button QuizExamPickerButton(string id)
    {
        var button = CompactStudyAction(SecondaryButton(U("Kids.Quiz.ChooseExam", "Choose a test island", "Test adacığı seç"), ""));
        AutomationProperties.SetHelpText(button, T("Kids.Quiz.ChangeExamHelp"));
        FocusTarget(button, id);
        button.Click += (_, _) => ShowQuizExamPicker();
        return button;
    }

    private async Task StartQuizExamSessionAsync(int examIndex, bool restart = false)
    {
        if (_studyBusy || _navigationBusy || _dialogOpen || _storageBlocked || _currentPage != "quiz") return;
        var pool = QuizExamPool();
        if (pool.Select(word => word.Word).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).Count() < 2)
        {
            ShowNotice(T("Common.Error"), T("Words.Empty"), InfoBarSeverity.Warning);
            return;
        }
        var total = QuizExamEngine.ExamCount(pool.Length);
        if (examIndex < 0 || examIndex >= total) return;
        if (!await MutateStudyAsync(() => QuizExamEngine.OpenExam(_progress, pool, examIndex, _random, restart))) return;
        _quizExamIndex = examIndex;
        _quizExamContext = StudyContext;
        _quizExamPage = examIndex / _quizExamPageSize;
        RequestSessionFocus("quiz");
        RenderQuiz();
    }

    private bool TryGetQuizExamInfo(StudySessionState session, out int examIndex, out int fromWord, out int toWord)
    {
        fromWord = toWord = 0;
        if (!QuizExamEngine.TryGetExamIndex(QuizExamPool(), session, out examIndex)) return false;
        var start = examIndex * QuizQuestionsPerExam;
        fromWord = start + 1;
        toWord = start + session.WordKeys.Count;
        return true;
    }

    private async Task StartCardsSessionAsync(IEnumerable<VocabularyEntry>? subset = null, bool isRetry = false)
    {
        if (_studyBusy || _navigationBusy || _dialogOpen) return;
        var existing = Session("cards");
        if (existing is not null && existing.Index < existing.WordKeys.Count &&
            !await ConfirmAsync(U("Session.Replace", "Replace the unfinished session?", "Bitmemiş oturum değiştirilsin mi?"),
                U("Session.ReplaceHint", "Recorded answers and reviews stay in your history; the session queue will be replaced.", "Kaydedilen yanıtlar ve tekrarlar geçmişte kalır; oturum sırası değiştirilir."))) return;
        var source = subset ?? CardsDeckPool();
        var queue = source.Where(word => word.LanguageCode == _settings.StudyLanguage && word.Level == _settings.Level)
            .DistinctBy(word => word.Key);
        var entries = queue.ToArray();
        if (await MutateStudyAsync(() =>
        {
            var session = LearningEngine.CreateSession(entries.Select(word => word.Key), isRetry);
            if (session.WordKeys.Count > 0)
            {
                if (subset is null && !isRetry && _progress.CardPositions.TryGetValue(StudyContext, out var resumeKey))
                {
                    var resumeIndex = session.WordKeys.IndexOf(resumeKey);
                    if (resumeIndex >= 0) session.Index = resumeIndex;
                }
                _progress.CardPositions[StudyContext] = session.WordKeys[session.Index];
            }
            _progress.Sessions[SessionKey("cards")] = session;
        }))
        {
            _revealedCardKey = null;
            _cardRevealed = false;
            _cardsSearchQuery = "";
            RequestSessionFocus("cards");
            if (_currentPage != "cards") NavigateTo("cards", CardsItem);
            else RenderCurrentPage();
        }
    }

    private bool TryUpgradeLegacyCardsSession(StudySessionState session, out StudySessionState upgraded)
    {
        upgraded = session;
        if (session.IsRetry || session.WordKeys.Count != 20) return false;
        var deck = CardsDeckPool();
        if (deck.Length <= session.WordKeys.Count) return false;

        var full = LearningEngine.CreateSession(deck.Select(word => word.Key), session.IsRetry);
        foreach (var answer in session.Answers)
            if (full.WordKeys.Contains(answer.Key, StringComparer.Ordinal))
                full.Answers[answer.Key] = answer.Value;

        var anchorKey = session.Index < session.WordKeys.Count ? session.WordKeys[session.Index] : null;
        if (anchorKey is null && _progress.CardPositions.TryGetValue(StudyContext, out var positionKey) &&
            full.WordKeys.Contains(positionKey, StringComparer.Ordinal))
            anchorKey = positionKey;
        if (anchorKey is null)
            anchorKey = session.WordKeys.FirstOrDefault(key => full.WordKeys.Contains(key, StringComparer.Ordinal));

        if (anchorKey is null) return false;
        var anchorIndex = full.WordKeys.IndexOf(anchorKey);
        if (anchorIndex >= 0) full.Index = anchorIndex;
        if (full.Index < full.WordKeys.Count && full.Answers.ContainsKey(full.WordKeys[full.Index]))
            full.Index = LearningEngine.NextUnansweredIndex(full);
        if (full.Index >= full.WordKeys.Count && full.WordKeys.Count > 0)
            full.Index = 0;

        _progress.Sessions[SessionKey("cards")] = full;
        if (full.WordKeys.Count > 0) _progress.CardPositions[StudyContext] = full.WordKeys[full.Index];
        upgraded = full;
        return true;
    }

    private static TextBlock StudyText(string text, double size, Brush? foreground = null, bool emphasis = false, bool selectable = false) => new()
    {
        Text = text,
        FontSize = ReadingSize(size),
        LineHeight = ReadingSize(size) * 1.4,
        LineStackingStrategy = LineStackingStrategy.MaxHeight,
        FontWeight = emphasis ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
        FontStyle = Windows.UI.Text.FontStyle.Normal,
        Foreground = foreground ?? AppearancePalette.Current.BoxForegroundBrush,
        TextAlignment = TextAlignment.Left,
        TextWrapping = TextWrapping.Wrap,
        TextTrimming = TextTrimming.None,
        TextLineBounds = TextLineBounds.Full,
        IsTextSelectionEnabled = selectable,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    // Unlike Card, this surface never recolors nested buttons or result text.
    private static Border StudySurface(UIElement content, double padding = 18, Brush? background = null, Brush? border = null) => new()
    {
        Child = content,
        Padding = new Thickness(padding),
        CornerRadius = new CornerRadius(16),
        Background = background ?? AppearancePalette.Current.BoxBrush,
        BorderBrush = border ?? AppearancePalette.Current.BorderBrush,
        BorderThickness = new Thickness(1),
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private static UIElement StudyLabel(string text)
    {
        var palette = AppearancePalette.Current;
        var background = AppearancePalette.EnsureFillContrast(palette.Box, palette.Border, 4.5);
        var foreground = AppearancePalette.EnsureTextContrast(background, palette.BackgroundForeground);
        var preferredBorder = AppearancePalette.EnsureBoundaryContrast(palette.Box, palette.Border);
        var border = AppearancePalette.EnsureBoundaryContrast(background, preferredBorder);
        var heading = StudyText(text, 18, new SolidColorBrush(foreground), emphasis: true);
        AutomationProperties.SetHeadingLevel(heading, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level3);
        AutomationProperties.SetName(heading, text);
        return new Border
        {
            Child = heading,
            Padding = new Thickness(10, 5, 10, 5),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(background),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            HighContrastAdjustment = ElementHighContrastAdjustment.Auto,
        };
    }

    private static StackPanel StudyDetail(string label, string text, double size = 20, Brush? foreground = null)
    {
        var detail = new StackPanel { Spacing = 4 };
        var labelBadge = StudyLabel(label);
        var body = StudyText(text, size, foreground, selectable: true);
        AutomationProperties.SetName(body, text);
        detail.Children.Add(labelBadge);
        detail.Children.Add(body);
        return detail;
    }

    private StackPanel StudyShortcutRows(string shortcuts, string automationPrefix)
    {
        var rows = new StackPanel { Spacing = 12 };
        var parts = shortcuts.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < parts.Length; index++)
        {
            var item = parts[index].Trim();
            if (item.Length == 0) continue;
            rows.Children.Add(StudyShortcutRow(item, $"{automationPrefix}.{index}"));
        }
        return rows;
    }

    private static UIElement StudyShortcutRow(string shortcut, string automationId)
    {
        var separator = shortcut.IndexOf(':');
        if (separator <= 0 || separator >= shortcut.Length - 1)
        {
            var plain = StudyText(shortcut, 18, emphasis: true);
            AutomationProperties.SetAutomationId(plain, automationId);
            AutomationProperties.SetName(plain, shortcut);
            return plain;
        }

        var row = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        var key = StudyText(shortcut[..separator].Trim() + ":", 20, emphasis: true);
        key.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
        var action = StudyText(shortcut[(separator + 1)..].Trim(), 20);
        Grid.SetColumn(action, 1);
        row.Children.Add(key);
        row.Children.Add(action);
        AutomationProperties.SetAutomationId(row, automationId);
        AutomationProperties.SetName(row, shortcut);
        return row;
    }

    private void StudyLive(TextBlock text, string id)
    {
        AutomationProperties.SetAutomationId(text, id);
        Live(text);
        text.Loaded += (_, _) => { if (text.IsLoaded && IsWithin(text, PageContent)) Announce(text, text.Text); };
    }

    private Expander StudyDisclosure(string title, UIElement content, string id)
    {
        var palette = AppearancePalette.Current;
        var disclosure = new Expander
        {
            Header = StudyText(title, 18, emphasis: true),
            Content = StudySurface(content, 14),
            IsExpanded = false,
            FontSize = ReadingSize(18),
            MinHeight = 48,
            MinWidth = 48,
            Background = palette.BoxBrush,
            Foreground = palette.BoxForegroundBrush,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        foreach (var state in new[] { "", "PointerOver", "Pressed", "Disabled" })
        {
            disclosure.Resources[$"ExpanderHeaderBackground{state}"] = palette.BoxBrush;
            disclosure.Resources[$"ExpanderHeaderForeground{state}"] = palette.BoxForegroundBrush;
        }
        AutomationProperties.SetName(disclosure, title);
        FocusTarget(disclosure, id);
        return disclosure;
    }

    private Button StudyPopupButton(string title, UIElement content, string id)
    {
        var palette = AppearancePalette.Current;
        var flyoutBackground = AppearancePalette.EnsureFillContrast(palette.Background, palette.Box, 4.5);
        var flyoutForeground = EnsureStrongTextContrast(flyoutBackground, palette.BoxForeground);
        var flyoutBorder = AppearancePalette.EnsureBoundaryContrast(flyoutBackground, palette.Border);
        var surface = StudySurface(content, 14,
            new SolidColorBrush(flyoutBackground),
            new SolidColorBrush(flyoutBorder));
        ApplyReadableForeground(surface, new SolidColorBrush(flyoutForeground));
        // Near a screen edge the presenter's on-screen width is narrower than MaxWidth;
        // with horizontal scroll left enabled it renders text at the wider MaxWidth
        // instead of rewrapping, so it clips lines behind a scrollbar. Disabling it
        // forces the real, narrower available width through, so text wraps instead.
        var noHorizontalScroll = new Style(typeof(FlyoutPresenter));
        noHorizontalScroll.Setters.Add(new Setter(ScrollViewer.HorizontalScrollModeProperty, ScrollMode.Disabled));
        noHorizontalScroll.Setters.Add(new Setter(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled));
        var flyout = new Flyout { Content = surface, FlyoutPresenterStyle = noHorizontalScroll };
        void Fit()
        {
            var width = XamlRoot?.Size.Width ?? 640;
            surface.MaxWidth = Math.Max(280, Math.Min(620, width - 64));
        }
        flyout.Opening += (_, _) => Fit();
        var button = CompactStudyAction(SecondaryButton(title, ""));
        var buttonBackground = AppearancePalette.EnsureFillContrast(palette.Background, palette.Button, 4.5);
        var buttonForeground = AppearancePalette.EnsureTextContrast(buttonBackground, palette.ButtonForeground);
        var buttonBorder = AppearancePalette.EnsureBoundaryContrast(buttonBackground, palette.ButtonBorder);
        ApplyAccessibleButtonVisuals(button, buttonBackground, buttonForeground, buttonBorder);
        button.Flyout = flyout;
        AutomationProperties.SetHelpText(button, T("Shell.ShowDetails"));
        FocusTarget(button, id);
        Fit();
        return button;
    }

    private Button StudyContentDialogButton(string title, UIElement content, string id, string helpText)
    {
        var button = CompactStudyAction(SecondaryButton(title, ""));
        FocusTarget(button, id);
        AutomationProperties.SetHelpText(button, helpText);
        ToolTipService.SetToolTip(button, helpText);
        button.Click += async (_, _) =>
        {
            if (!IsLoaded || _dialogOpen || _studyBusy || _navigationBusy || _storageBlocked) return;
            var focus = FocusManager.GetFocusedElement(XamlRoot) as Control;
            var page = _currentPage;
            var context = StudyContext;
            _dialogOpen = true;
            try
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    RequestedTheme = RequestedTheme,
                    Title = title,
                    Content = StudySurface(content, 14),
                    CloseButtonText = T("Help.Close"),
                    DefaultButton = ContentDialogButton.Close,
                };
                AutomationProperties.SetAutomationId(dialog, id + ".Dialog");
                AutomationProperties.SetName(dialog, title);
                AutomationProperties.SetHelpText(dialog, helpText);
                ConfigureReadingDialog(dialog);
                await dialog.ShowAsync();
            }
            catch (Exception ex) { ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error); }
            finally
            {
                _dialogOpen = false;
                RestoreDialogFocus(focus, page, context);
            }
        };
        return button;
    }

    private static Button ReadableStudyAction(Button button)
    {
        button.MinHeight = Math.Max(48, button.MinHeight);
        button.MinWidth = Math.Max(48, button.MinWidth);
        button.FontSize = ReadingSize(18);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.IsTabStop = true;
        button.UseSystemFocusVisuals = true;
        if (button.Content is Grid copy)
            foreach (var text in copy.Children.OfType<TextBlock>())
            {
                text.FontSize = ReadingSize(18);
                text.LineHeight = ReadingSize(18) * 1.4;
                text.LineStackingStrategy = LineStackingStrategy.MaxHeight;
                text.TextWrapping = TextWrapping.Wrap;
                text.TextTrimming = TextTrimming.None;
                text.TextLineBounds = TextLineBounds.Full;
            }
        return button;
    }

    private static Button CompactStudyAction(Button button)
    {
        button = ReadableStudyAction(button);
        var palette = AppearancePalette.Current;
        var background = AppearancePalette.EnsureFillContrast(palette.Background, palette.Box, 4.5);
        var preferredBorder = AppearancePalette.EnsureBoundaryContrast(palette.Background, palette.Border);
        button.Padding = new Thickness(10, 8, 10, 8);
        button.CornerRadius = new CornerRadius(12);
        ApplyAccessibleButtonVisuals(button, background, palette.BoxForeground, preferredBorder);
        return button;
    }

    private static Color EnsureStrongTextContrast(Color background, Color preferred, double minimumContrast = 7)
    {
        var foreground = AppearancePalette.EnsureTextContrast(background, preferred);
        if (AppearancePalette.ContrastRatio(background, foreground) >= minimumContrast) return foreground;
        return AppearancePalette.ContrastRatio(background, Microsoft.UI.Colors.White) >=
            AppearancePalette.ContrastRatio(background, Microsoft.UI.Colors.Black)
            ? Microsoft.UI.Colors.White
            : Microsoft.UI.Colors.Black;
    }

    private static void ApplyCardsActionVisual(Button button, bool primary = false)
    {
        var palette = AppearancePalette.Current;
        var background = primary
            ? AppearancePalette.EnsureFillContrast(palette.Background, palette.Button, 5.2)
            : AppearancePalette.EnsureFillContrast(palette.Background, palette.ButtonTint, 5.2);
        var foreground = EnsureStrongTextContrast(background,
            primary ? palette.ButtonForeground : palette.BoxForeground);
        var border = AppearancePalette.EnsureBoundaryContrast(background,
            primary ? palette.ButtonBorder : palette.Border);
        ApplyAccessibleButtonVisuals(button, background, foreground, border);
        button.BorderThickness = new Thickness(2);
    }

    private Button StudyMarkButton(VocabularyEntry entry, bool known)
    {
        Button? button = null;
        void Refresh()
        {
            if (button is null) return;
            var marked = (known ? _progress.KnownWords : _progress.FavoriteWords).Contains(entry.Key);
            var label = known
                ? (marked ? "✓ " : "") + U("Kids.Cards.Known", "I know this word", "Bu kelimeyi biliyorum")
                : (marked ? "★ " : "☆ ") + U("Kids.Cards.Favorite", "My favorite", "Sevdiğim kelime");
            var hint = marked ? U("Kids.Cards.Unmark", "Click again to remove this mark.", "Bu işareti kaldırmak için tekrar tıkla.")
                : U("Kids.Cards.Mark", "Click to mark this word in your list.", "Bu kelimeyi listende işaretlemek için tıkla.");
            button.Content = ButtonContent(label, "");
            if (button.Content is UIElement content) ApplyButtonIconForeground(content, button.Foreground);
            AutomationProperties.SetName(button, label);
            AutomationProperties.SetHelpText(button, hint);
            ToolTipService.SetToolTip(button, hint);
        }
        button = CompactStudyAction(known ? KnownButton(entry, Refresh) : FavoriteButton(entry, Refresh));
        ApplyCardsActionVisual(button);
        Refresh();
        return button;
    }

    private static Grid StudyWordRow(StackPanel word, Button listen)
    {
        var row = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(word);
        row.Children.Add(listen);
        listen.HorizontalAlignment = HorizontalAlignment.Left;
        listen.VerticalAlignment = VerticalAlignment.Center;
        bool? stacked = null;
        void Reflow()
        {
            var narrow = row.ActualWidth > 0 && row.ActualWidth < Math.Max(360, Font(360));
            if (stacked == narrow) return;
            stacked = narrow;
            row.RowDefinitions.Clear();
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (narrow) row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumnSpan(word, narrow ? 2 : 1);
            Grid.SetColumn(listen, narrow ? 0 : 1);
            Grid.SetRow(listen, narrow ? 1 : 0);
            Grid.SetColumnSpan(listen, narrow ? 2 : 1);
        }
        row.Loaded += (_, _) => Reflow();
        row.SizeChanged += (_, _) => Reflow();
        Reflow();
        return row;
    }

    private static Grid StudyOptionContent(int number, TextBlock label, string? description, Brush background, Brush foreground)
    {
        var backgroundColor = background is SolidColorBrush backgroundBrush ? backgroundBrush.Color : AppearancePalette.Current.Box;
        var foregroundColor = foreground is SolidColorBrush foregroundBrush ? foregroundBrush.Color : AppearancePalette.Current.BoxForeground;
        var badgeBackground = AppearancePalette.EnsureFillContrast(backgroundColor, foregroundColor, 4.5);
        var badgeForeground = EnsureStrongTextContrast(badgeBackground, backgroundColor);
        var badgeBorder = AppearancePalette.EnsureBoundaryContrast(badgeBackground, backgroundColor);
        var content = new Grid { ColumnSpacing = 12 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition());
        var badgeText = StudyText(number.ToString(CultureInfo.CurrentCulture), 18, background, emphasis: true);
        badgeText.Foreground = new SolidColorBrush(badgeForeground);
        var badge = new Border
        {
            Child = badgeText,
            Background = new SolidColorBrush(badgeBackground),
            BorderBrush = new SolidColorBrush(badgeBorder),
            BorderThickness = new Thickness(2),
            Padding = new Thickness(11, 6, 11, 6),
            CornerRadius = new CornerRadius(8),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 44,
        };
        AutomationProperties.SetAccessibilityView(badge, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        content.Children.Add(badge);
        var copy = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        copy.Children.Add(label);
        if (!string.IsNullOrWhiteSpace(description)) copy.Children.Add(StudyText(description, 18, foreground));
        Grid.SetColumn(copy, 1);
        content.Children.Add(copy);
        return content;
    }

    private sealed record StudyChoiceVisual(Color Background, Color Foreground, Color Border);

    private static StudyChoiceVisual RatingVisual(RecallRating rating)
    {
        var dark = AppearancePalette.Current.Theme == ElementTheme.Dark;
        static StudyChoiceVisual Accessible(Color background, Color foreground, Color border) => new(
            background,
            AppearancePalette.EnsureTextContrast(background, foreground),
            AppearancePalette.EnsureBoundaryContrast(background, border));
        return rating switch
        {
            RecallRating.Again => dark
                ? Accessible(Color.FromArgb(255, 127, 29, 29), Color.FromArgb(255, 254, 226, 226), Color.FromArgb(255, 252, 165, 165))
                : Accessible(Color.FromArgb(255, 254, 226, 226), Color.FromArgb(255, 153, 27, 27), Color.FromArgb(255, 185, 28, 28)),
            RecallRating.Hard => dark
                ? Accessible(Color.FromArgb(255, 120, 53, 15), Color.FromArgb(255, 255, 237, 213), Color.FromArgb(255, 253, 186, 116))
                : Accessible(Color.FromArgb(255, 255, 237, 213), Color.FromArgb(255, 154, 52, 18), Color.FromArgb(255, 194, 65, 12)),
            RecallRating.Good => dark
                ? Accessible(Color.FromArgb(255, 20, 83, 45), Color.FromArgb(255, 220, 252, 231), Color.FromArgb(255, 134, 239, 172))
                : Accessible(Color.FromArgb(255, 220, 252, 231), Color.FromArgb(255, 22, 101, 52), Color.FromArgb(255, 21, 128, 61)),
            _ => dark
                ? Accessible(Color.FromArgb(255, 30, 58, 138), Color.FromArgb(255, 219, 234, 254), Color.FromArgb(255, 147, 197, 253))
                : Accessible(Color.FromArgb(255, 219, 234, 254), Color.FromArgb(255, 30, 64, 175), Color.FromArgb(255, 37, 99, 235)),
        };
    }

    private static string NormalizeQuizChoicePalette(string? palette)
    {
        var value = palette?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return UserSettings.DefaultQuizChoicePalette;
        var preset = value.ToLowerInvariant();
        if (QuizChoicePalettes.Contains(preset, StringComparer.Ordinal)) return preset;
        if (TryDecodeQuizChoiceCustomPalette(value, out var custom))
            return EncodeQuizChoiceCustomPalette(custom);
        return UserSettings.DefaultQuizChoicePalette;
    }

    private void SetQuizChoicePalette(string palette) =>
        _settings.QuizChoicePalette = NormalizeQuizChoicePalette(palette);

    private static bool TryGetQuizChoicePreset(string? palette, out string preset)
    {
        var normalized = NormalizeQuizChoicePalette(palette);
        if (QuizChoicePalettes.Contains(normalized, StringComparer.Ordinal))
        {
            preset = normalized;
            return true;
        }

        preset = UserSettings.DefaultQuizChoicePalette;
        return false;
    }

    private static StudyChoiceVisual CustomQuizChoiceVisual(Color background, Color foreground)
    {
        var preferredBorder = MixColor(background, foreground, 0.34);
        return new StudyChoiceVisual(
            background,
            foreground,
            AppearancePalette.EnsureBoundaryContrast(background, preferredBorder));
    }

    private static bool TryDecodeQuizChoiceCustomPalette(string? palette, out StudyChoiceVisual[] choices)
    {
        choices = [];
        var value = palette?.Trim();
        if (value is null || !value.StartsWith(QuizChoiceCustomPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        static bool TryHex(string text, out Color color)
        {
            color = default;
            if (text.Length != 7 || text[0] != '#' || !text.Skip(1).All(char.IsAsciiHexDigit)) return false;
            color = Color.FromArgb(
                255,
                Convert.ToByte(text[1..3], 16),
                Convert.ToByte(text[3..5], 16),
                Convert.ToByte(text[5..7], 16));
            return true;
        }

        var rows = value[QuizChoiceCustomPrefix.Length..].Split(';', StringSplitOptions.TrimEntries);
        if (rows.Length != QuizChoiceCount) return false;
        var parsed = new StudyChoiceVisual[QuizChoiceCount];
        for (var index = 0; index < rows.Length; index++)
        {
            var parts = rows[index].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !TryHex(parts[0], out var background) || !TryHex(parts[1], out var foreground))
                return false;
            parsed[index] = CustomQuizChoiceVisual(background, foreground);
        }

        choices = parsed;
        return true;
    }

    private static string EncodeQuizChoiceCustomPalette(IReadOnlyList<StudyChoiceVisual> choices)
    {
        if (choices.Count < QuizChoiceCount) return UserSettings.DefaultQuizChoicePalette;
        return QuizChoiceCustomPrefix + string.Join(";", Enumerable.Range(0, QuizChoiceCount)
            .Select(index => $"{AppearancePalette.ToHex(choices[index].Background)},{AppearancePalette.ToHex(choices[index].Foreground)}"));
    }

    private static StudyChoiceVisual UniversalDisabledChoiceVisual(StudyChoiceVisual visual)
    {
        var palette = AppearancePalette.Current;
        var muted = MixColor(visual.Background, palette.Box, 0.58);
        var background = MixColor(muted, palette.Background, 0.22);
        return new StudyChoiceVisual(
            background,
            EnsureStrongTextContrast(background, palette.BoxForeground, 4.5),
            AppearancePalette.EnsureBoundaryContrast(background, palette.Border));
    }

    private static Color MixColor(Color first, Color second, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            255,
            (byte)Math.Round(first.R + ((second.R - first.R) * amount)),
            (byte)Math.Round(first.G + ((second.G - first.G) * amount)),
            (byte)Math.Round(first.B + ((second.B - first.B) * amount)));
    }

    private static StudyChoiceVisual QuizChoiceVisual(int index, string palette)
    {
        if (TryDecodeQuizChoiceCustomPalette(palette, out var custom))
            return custom[index % QuizChoiceCount];

        var dark = AppearancePalette.Current.Theme == ElementTheme.Dark;
        static StudyChoiceVisual Accessible(Color background, Color foreground, Color border) => new(
            background,
            EnsureStrongTextContrast(background, foreground),
            AppearancePalette.EnsureBoundaryContrast(background, border));
        return NormalizeQuizChoicePalette(palette) switch
        {
            "classic" => (index % 4) switch
            {
                0 => dark
                    ? Accessible(Color.FromArgb(255, 30, 58, 138), Color.FromArgb(255, 219, 234, 254), Color.FromArgb(255, 147, 197, 253))
                    : Accessible(Color.FromArgb(255, 224, 236, 255), Color.FromArgb(255, 30, 64, 175), Color.FromArgb(255, 37, 99, 235)),
                1 => dark
                    ? Accessible(Color.FromArgb(255, 20, 83, 45), Color.FromArgb(255, 220, 252, 231), Color.FromArgb(255, 134, 239, 172))
                    : Accessible(Color.FromArgb(255, 220, 252, 231), Color.FromArgb(255, 22, 101, 52), Color.FromArgb(255, 22, 163, 74)),
                2 => dark
                    ? Accessible(Color.FromArgb(255, 76, 29, 149), Color.FromArgb(255, 243, 232, 255), Color.FromArgb(255, 196, 181, 253))
                    : Accessible(Color.FromArgb(255, 243, 232, 255), Color.FromArgb(255, 107, 33, 168), Color.FromArgb(255, 147, 51, 234)),
                _ => dark
                    ? Accessible(Color.FromArgb(255, 124, 45, 18), Color.FromArgb(255, 255, 237, 213), Color.FromArgb(255, 253, 186, 116))
                    : Accessible(Color.FromArgb(255, 255, 244, 229), Color.FromArgb(255, 154, 52, 18), Color.FromArgb(255, 234, 88, 12)),
            },
            "meadow" => (index % 4) switch
            {
                0 => dark
                    ? Accessible(Color.FromArgb(255, 12, 74, 110), Color.FromArgb(255, 224, 242, 254), Color.FromArgb(255, 125, 211, 252))
                    : Accessible(Color.FromArgb(255, 224, 242, 254), Color.FromArgb(255, 3, 105, 161), Color.FromArgb(255, 14, 116, 144)),
                1 => dark
                    ? Accessible(Color.FromArgb(255, 6, 78, 59), Color.FromArgb(255, 209, 250, 229), Color.FromArgb(255, 110, 231, 183))
                    : Accessible(Color.FromArgb(255, 236, 253, 245), Color.FromArgb(255, 6, 95, 70), Color.FromArgb(255, 16, 185, 129)),
                2 => dark
                    ? Accessible(Color.FromArgb(255, 113, 63, 18), Color.FromArgb(255, 254, 249, 195), Color.FromArgb(255, 253, 224, 71))
                    : Accessible(Color.FromArgb(255, 254, 252, 232), Color.FromArgb(255, 133, 77, 14), Color.FromArgb(255, 202, 138, 4)),
                _ => dark
                    ? Accessible(Color.FromArgb(255, 136, 19, 55), Color.FromArgb(255, 255, 228, 230), Color.FromArgb(255, 251, 113, 133))
                    : Accessible(Color.FromArgb(255, 255, 241, 242), Color.FromArgb(255, 159, 18, 57), Color.FromArgb(255, 225, 29, 72)),
            },
            "sunset" => (index % 4) switch
            {
                0 => dark
                    ? Accessible(Color.FromArgb(255, 124, 45, 18), Color.FromArgb(255, 255, 237, 213), Color.FromArgb(255, 253, 186, 116))
                    : Accessible(Color.FromArgb(255, 255, 247, 237), Color.FromArgb(255, 154, 52, 18), Color.FromArgb(255, 249, 115, 22)),
                1 => dark
                    ? Accessible(Color.FromArgb(255, 76, 29, 149), Color.FromArgb(255, 237, 233, 254), Color.FromArgb(255, 196, 181, 253))
                    : Accessible(Color.FromArgb(255, 245, 243, 255), Color.FromArgb(255, 91, 33, 182), Color.FromArgb(255, 139, 92, 246)),
                2 => dark
                    ? Accessible(Color.FromArgb(255, 12, 74, 110), Color.FromArgb(255, 224, 242, 254), Color.FromArgb(255, 125, 211, 252))
                    : Accessible(Color.FromArgb(255, 240, 249, 255), Color.FromArgb(255, 7, 89, 133), Color.FromArgb(255, 14, 165, 233)),
                _ => dark
                    ? Accessible(Color.FromArgb(255, 20, 83, 45), Color.FromArgb(255, 220, 252, 231), Color.FromArgb(255, 134, 239, 172))
                    : Accessible(Color.FromArgb(255, 236, 253, 243), Color.FromArgb(255, 22, 101, 52), Color.FromArgb(255, 34, 197, 94)),
            },
            "slate" => (index % 4) switch
            {
                0 => dark
                    ? Accessible(Color.FromArgb(255, 51, 65, 85), Color.FromArgb(255, 226, 232, 240), Color.FromArgb(255, 148, 163, 184))
                    : Accessible(Color.FromArgb(255, 226, 232, 240), Color.FromArgb(255, 51, 65, 85), Color.FromArgb(255, 100, 116, 139)),
                1 => dark
                    ? Accessible(Color.FromArgb(255, 19, 78, 74), Color.FromArgb(255, 204, 251, 241), Color.FromArgb(255, 94, 234, 212))
                    : Accessible(Color.FromArgb(255, 226, 244, 241), Color.FromArgb(255, 17, 94, 89), Color.FromArgb(255, 15, 118, 110)),
                2 => dark
                    ? Accessible(Color.FromArgb(255, 120, 53, 15), Color.FromArgb(255, 254, 243, 199), Color.FromArgb(255, 252, 211, 77))
                    : Accessible(Color.FromArgb(255, 243, 239, 224), Color.FromArgb(255, 146, 64, 14), Color.FromArgb(255, 180, 83, 9)),
                _ => dark
                    ? Accessible(Color.FromArgb(255, 88, 28, 135), Color.FromArgb(255, 243, 232, 255), Color.FromArgb(255, 196, 181, 253))
                    : Accessible(Color.FromArgb(255, 239, 232, 247), Color.FromArgb(255, 107, 33, 168), Color.FromArgb(255, 124, 58, 237)),
            },
            _ => (index % 4) switch
            {
                0 => dark
                    ? Accessible(Color.FromArgb(255, 30, 58, 138), Color.FromArgb(255, 219, 234, 254), Color.FromArgb(255, 147, 197, 253))
                    : Accessible(Color.FromArgb(255, 224, 236, 255), Color.FromArgb(255, 30, 64, 175), Color.FromArgb(255, 37, 99, 235)),
                1 => dark
                    ? Accessible(Color.FromArgb(255, 20, 83, 45), Color.FromArgb(255, 220, 252, 231), Color.FromArgb(255, 134, 239, 172))
                    : Accessible(Color.FromArgb(255, 220, 252, 231), Color.FromArgb(255, 22, 101, 52), Color.FromArgb(255, 22, 163, 74)),
                2 => dark
                    ? Accessible(Color.FromArgb(255, 76, 29, 149), Color.FromArgb(255, 243, 232, 255), Color.FromArgb(255, 196, 181, 253))
                    : Accessible(Color.FromArgb(255, 243, 232, 255), Color.FromArgb(255, 107, 33, 168), Color.FromArgb(255, 147, 51, 234)),
                _ => dark
                    ? Accessible(Color.FromArgb(255, 124, 45, 18), Color.FromArgb(255, 255, 237, 213), Color.FromArgb(255, 253, 186, 116))
                    : Accessible(Color.FromArgb(255, 255, 244, 229), Color.FromArgb(255, 154, 52, 18), Color.FromArgb(255, 234, 88, 12)),
            },
        };
    }

    private static void ApplyStudyChoiceVisual(Button button, StudyChoiceVisual visual)
    {
        var enabledBackground = new SolidColorBrush(visual.Background);
        var enabledForeground = new SolidColorBrush(visual.Foreground);
        var enabledBorder = new SolidColorBrush(visual.Border);
        var disabled = UniversalDisabledChoiceVisual(visual);
        var disabledBackground = new SolidColorBrush(disabled.Background);
        var disabledForeground = new SolidColorBrush(disabled.Foreground);
        var disabledBorder = new SolidColorBrush(disabled.Border);

        button.Background = enabledBackground;
        button.Foreground = enabledForeground;
        button.BorderBrush = enabledBorder;
        button.BorderThickness = new Thickness(1);
        button.HighContrastAdjustment = ElementHighContrastAdjustment.Auto;
        foreach (var state in new[] { "", "PointerOver", "Pressed", "Focused" })
        {
            SetButtonResource(button, $"ButtonBackground{state}", enabledBackground);
            SetButtonResource(button, $"ButtonForeground{state}", enabledForeground);
            SetButtonResource(button, $"ButtonBorderBrush{state}", enabledBorder);
        }
        SetButtonResource(button, "ButtonBackgroundDisabled", disabledBackground);
        SetButtonResource(button, "ButtonForegroundDisabled", disabledForeground);
        SetButtonResource(button, "ButtonBorderBrushDisabled", disabledBorder);
    }

    private sealed record CardSearchMatch(string Key, string Word, int Index, int Score);

    private IReadOnlyList<CardSearchMatch> FindCardSearchMatches(IReadOnlyList<VocabularyEntry> entries, string query, int limit = 8)
    {
        var normalized = LearningEngine.NormalizeAnswer(query, _settings.StudyLanguage);
        if (string.IsNullOrWhiteSpace(normalized)) return [];
        normalized = normalized.Trim();
        var foldedQuery = FoldForCardSearch(normalized);
        var typoBudget = CardSearchTypoBudget(foldedQuery.Length);
        var matches = new List<CardSearchMatch>(Math.Min(entries.Count, 32));
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var normalizedWord = LearningEngine.NormalizeAnswer(entry.Word, entry.LanguageCode);
            var foldedWord = FoldForCardSearch(normalizedWord);
            var score = CardSearchScore(normalized, foldedQuery, normalizedWord, foldedWord, typoBudget);
            if (score == int.MaxValue) continue;
            matches.Add(new CardSearchMatch(entry.Key, entry.Word, index, score));
        }
        return matches.OrderBy(match => match.Score)
            .ThenBy(match => match.Word, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(match => match.Index)
            .Take(limit)
            .ToArray();
    }

    private static int CardSearchScore(string query, string foldedQuery, string word, string foldedWord, int typoBudget)
    {
        if (word.Equals(query, StringComparison.Ordinal)) return 0;
        if (foldedWord.Equals(foldedQuery, StringComparison.Ordinal)) return 1;
        if (word.StartsWith(query, StringComparison.Ordinal)) return 5 + Math.Abs(word.Length - query.Length);
        if (foldedWord.StartsWith(foldedQuery, StringComparison.Ordinal)) return 10 + Math.Abs(foldedWord.Length - foldedQuery.Length);
        var contains = word.IndexOf(query, StringComparison.Ordinal);
        if (contains >= 0) return 20 + contains;
        var foldedContains = foldedWord.IndexOf(foldedQuery, StringComparison.Ordinal);
        if (foldedContains >= 0) return 30 + foldedContains;
        var distance = BoundedDamerauLevenshtein(foldedWord, foldedQuery, typoBudget);
        return distance <= typoBudget ? 100 + distance * 10 + Math.Abs(foldedWord.Length - foldedQuery.Length) : int.MaxValue;
    }

    private static int CardSearchTypoBudget(int length) => length <= 1 ? 0 : length <= 4 ? 1 : length <= 8 ? 2 : 3;

    private static string FoldForCardSearch(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.Format) continue;
            builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static int BoundedDamerauLevenshtein(string source, string target, int maxDistance)
    {
        if (source.Equals(target, StringComparison.Ordinal)) return 0;
        if (maxDistance < 0) return int.MaxValue;
        if (Math.Abs(source.Length - target.Length) > maxDistance) return maxDistance + 1;
        var previousPrevious = new int[target.Length + 1];
        var previous = new int[target.Length + 1];
        var current = new int[target.Length + 1];
        for (var j = 0; j <= target.Length; j++) previous[j] = j;
        for (var i = 1; i <= source.Length; i++)
        {
            current[0] = i;
            var rowMinimum = current[0];
            for (var j = 1; j <= target.Length; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                var deletion = previous[j] + 1;
                var insertion = current[j - 1] + 1;
                var substitution = previous[j - 1] + cost;
                var best = Math.Min(Math.Min(deletion, insertion), substitution);
                if (i > 1 && j > 1 && source[i - 1] == target[j - 2] && source[i - 2] == target[j - 1])
                    best = Math.Min(best, previousPrevious[j - 2] + 1);
                current[j] = best;
                rowMinimum = Math.Min(rowMinimum, best);
            }
            if (rowMinimum > maxDistance) return maxDistance + 1;
            (previousPrevious, previous, current) = (previous, current, previousPrevious);
        }
        return previous[target.Length];
    }

    private static Button StudyOptionButton(UIElement content, string name, StudyChoiceVisual? visual = null)
    {
        var button = CompactStudyAction(SecondaryButton(name, ""));
        button.Content = content;
        button.Padding = new Thickness(14, 12, 14, 12);
        button.CornerRadius = new CornerRadius(16);
        button.MinHeight = 56;
        button.BorderThickness = new Thickness(2);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        if (visual is not null) ApplyStudyChoiceVisual(button, visual);
        AutomationProperties.SetName(button, name);
        button.IsTabStop = true;
        button.UseSystemFocusVisuals = true;
        return button;
    }

    // Keep multi-choice study grids balanced and responsive, never a stranded row.
    private static void ConfigureStudyChoices(Grid grid, int maximumColumns, double minimumColumnWidth)
    {
        var currentColumns = 0;
        void Reflow()
        {
            var width = grid.ActualWidth;
            var minimum = minimumColumnWidth * Math.Max(1, AppearancePalette.Current.FontScale);
            var columns = width <= 0 || width >= maximumColumns * minimum + (maximumColumns - 1) * grid.ColumnSpacing
                ? maximumColumns : width >= 2 * minimum + grid.ColumnSpacing ? 2 : 1;
            var rows = (grid.Children.Count + columns - 1) / columns;
            if (columns == currentColumns && grid.RowDefinitions.Count == rows) return;
            currentColumns = columns;
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();
            for (var column = 0; column < columns; column++) grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (var row = 0; row < rows; row++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var index = 0; index < grid.Children.Count; index++)
            {
                Grid.SetColumn((FrameworkElement)grid.Children[index], index % columns);
                Grid.SetRow((FrameworkElement)grid.Children[index], index / columns);
            }
        }
        grid.Loaded += (_, _) => Reflow();
        grid.SizeChanged += (_, _) => Reflow();
        Reflow();
    }

    private Button SessionStartButton(bool restart = false, bool secondary = false)
    {
        var label = restart ? U("Kids.Session.New", "Practice more", "Biraz daha çalış")
            : U("Kids.Session.Start", "Let's start", "Hadi başlayalım");
        var start = secondary ? CompactStudyAction(SecondaryButton(label, "")) : ReadableStudyAction(AccentButton(label, ""));
        AutomationProperties.SetHelpText(start,
            U("Kids.Cards.StartHint", "Look at a word. Think, then show its meaning.", "Kelimeye bak. Düşün, sonra anlamını aç."));
        start.HorizontalAlignment = HorizontalAlignment.Stretch;
        start.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        FocusTarget(start, "cards.Start");
        start.Click += async (_, _) => await StartCardsSessionAsync();
        return start;
    }

    private (int Columns, int PageSize) QuizExamLayout()
    {
        var width = Math.Max(1, (PageContent.Width > 0 ? PageContent.Width : 960) - 36);
        var columns = Math.Clamp((int)((width + 12) / (Math.Max(176, Font(176)) + 12)), 1, 5);
        var height = ContentScroll.ActualHeight > 0 ? ContentScroll.ActualHeight : 600;
        var tileHeight = ReadingSize(22) * 1.4 + ReadingSize(18) * 2.8 + 44;
        var reserved = ReadingSize(28) * 2 + ReadingSize(24) * 2.8 + ReadingSize(18) * 2.8 + 144;
        var rows = Math.Clamp((int)((height - reserved) / tileHeight), 1, 4);
        return (columns, columns * rows);
    }

    private void RefreshQuizExamLayout()
    {
        if (_currentPage != "quiz" || _quizExamIndex is not null || _navigationBusy || _studyBusy || _dialogOpen || !IsLoaded) return;
        var layout = QuizExamLayout();
        if (layout == (_quizExamColumns, _quizExamPageSize)) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_currentPage == "quiz" && _quizExamIndex is null && !_navigationBusy && !_studyBusy && !_dialogOpen)
                RenderQuiz();
        });
    }

    private StudyChoiceVisual QuizIslandVisual(int examIndex)
    {
        var palette = AppearancePalette.Current;
        var baseVisual = QuizChoiceVisual(examIndex, _settings.QuizChoicePalette);

        // Keep the quiz palette colors, then nudge shade per island group so visible cards are distinct.
        var shadeStep = ((examIndex / QuizChoiceCount) % 5) * 0.06;
        var target = palette.Theme == ElementTheme.Dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
        var shiftedBackground = shadeStep <= 0
            ? baseVisual.Background
            : MixColor(baseVisual.Background, target, shadeStep);

        var background = AppearancePalette.EnsureFillContrast(palette.Box, shiftedBackground, 3.4);
        var foreground = EnsureStrongTextContrast(background, baseVisual.Foreground, 7);
        var border = AppearancePalette.EnsureBoundaryContrast(background, MixColor(background, foreground, 0.34));
        return new StudyChoiceVisual(background, foreground, border);
    }

    private void AddQuizExamPicker(StackPanel content)
    {
        var pool = QuizExamPool();
        if (pool.Length < 2)
        {
            content.Children.Add(StudyText(T("Words.Empty"), 18));
            return;
        }

        var firstVisibleExam = _quizExamPage * _quizExamPageSize;
        (_quizExamColumns, _quizExamPageSize) = QuizExamLayout();
        var totalExams = QuizExamEngine.ExamCount(pool.Length);
        var totalPages = Math.Max(1, (totalExams + _quizExamPageSize - 1) / _quizExamPageSize);
        _quizExamPage = firstVisibleExam / _quizExamPageSize;
        _quizExamPage = Math.Clamp(_quizExamPage, 0, totalPages - 1);

        content.Children.Add(StudyText(string.Format(CultureInfo.CurrentCulture,
            U("Kids.Quiz.ExamCount", "{0} test islands · {1} words", "{0} test adacığı · {1} kelime"),
            totalExams, pool.Length), 18));

        var examGrid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        AutomationProperties.SetAutomationId(examGrid, "quiz.ExamPicker");
        AutomationProperties.SetName(examGrid, T("Kids.Quiz.ExamTitle"));
        var firstExam = _quizExamPage * _quizExamPageSize;
        var lastExam = Math.Min(totalExams, firstExam + _quizExamPageSize);
        for (var column = 0; column < _quizExamColumns; column++) examGrid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var examIndex = firstExam; examIndex < lastExam; examIndex++)
        {
            var from = examIndex * QuizQuestionsPerExam + 1;
            var to = Math.Min((examIndex + 1) * QuizQuestionsPerExam, pool.Length);
            var examLabel = string.Format(CultureInfo.CurrentCulture,
                U("Kids.Quiz.ExamLabel", "Test {0}", "Test {0}"), examIndex + 1);
            var rangeLabel = string.Format(CultureInfo.CurrentCulture,
                U("Kids.Quiz.ExamRange", "Words {0}-{1}", "Kelimeler {0}-{1}"), from, to);
            var saved = QuizExamEngine.SavedExam(_progress, pool, examIndex);
            var savedLabel = string.Format(CultureInfo.CurrentCulture,
                U("Kids.Quiz.ExamSaved", "{0} of {1} answered", "{1} sorudan {0} tanesi yanıtlandı"), saved?.Answers.Count ?? 0, to - from + 1);
            var visual = QuizIslandVisual(examIndex);
            var foregroundBrush = new SolidColorBrush(visual.Foreground);
            var tile = new StackPanel { Spacing = 4 };
            tile.Children.Add(StudyText(examLabel, 22, foregroundBrush, emphasis: true));
            tile.Children.Add(StudyText(rangeLabel, 18, foregroundBrush, emphasis: true));
            tile.Children.Add(StudyText(savedLabel, 18, foregroundBrush));
            var button = ReadableStudyAction(SecondaryButton(examLabel, ""));
            button.Content = tile;
            button.Padding = new Thickness(12);
            button.MinHeight = 88;
            ApplyStudyChoiceVisual(button, visual);
            button.BorderThickness = new Thickness(2);
            var examHelp = string.Format(CultureInfo.CurrentCulture,
                U("Kids.Quiz.ExamHelp", "{0} questions. Open this test or continue where you left off.", "{0} soru. Bu testi aç veya kaldığın yerden devam et."), to - from + 1);
            AutomationProperties.SetName(button, examLabel + " · " + rangeLabel + " · " + savedLabel);
            AutomationProperties.SetHelpText(button, examHelp);
            ToolTipService.SetToolTip(button, examHelp);
            FocusTarget(button, QuizExamFocusId(examIndex));
            var capturedIndex = examIndex;
            button.Click += async (_, _) =>
            {
                if (IsWithin(button, PageContent)) await StartQuizExamSessionAsync(capturedIndex);
            };
            var position = examIndex - firstExam;
            if (position % _quizExamColumns == 0) examGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(button, position / _quizExamColumns);
            Grid.SetColumn(button, position % _quizExamColumns);
            examGrid.Children.Add(button);
        }
        content.Children.Add(examGrid);

        if (totalPages > 1)
        {
            var pages = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            ConfigureReadingComboBox(pages);
            ApplySettingsComboBoxVisuals(pages);
            AutomationProperties.SetName(pages, U("Kids.Quiz.ExamPages", "Test pages", "Test sayfaları"));
            AutomationProperties.SetHelpText(pages, string.Format(CultureInfo.CurrentCulture,
                U("Kids.Quiz.ExamPage", "Page {0} of {1}", "Sayfa {0} / {1}"), _quizExamPage + 1, totalPages));
            FocusTarget(pages, "quiz.ExamPage");
            for (var page = 0; page < totalPages; page++)
                pages.Items.Add(string.Format(CultureInfo.CurrentCulture,
                    U("Kids.Quiz.ExamPageRange", "Tests {0}-{1}", "Testler {0}-{1}"),
                    page * _quizExamPageSize + 1, Math.Min(totalExams, (page + 1) * _quizExamPageSize)));
            pages.SelectedIndex = _quizExamPage;
            void SelectPage(int page)
            {
                if (page < 0 || page >= totalPages || _studyBusy || _navigationBusy || _dialogOpen || !IsWithin(pages, PageContent)) return;
                _quizExamPage = page;
                RequestUiFocus(QuizExamFocusId(_quizExamPage * _quizExamPageSize), "quiz");
                RenderQuiz();
            }
            pages.SelectionChanged += (_, _) => { if (pages.SelectedIndex != _quizExamPage) SelectPage(pages.SelectedIndex); };
            var previous = CompactStudyAction(SecondaryButton(U("Kids.Quiz.ExamPrevious", "Previous tests", "Önceki testler"), ""));
            previous.IsEnabled = _quizExamPage > 0;
            FocusTarget(previous, "quiz.ExamPage.Previous");
            previous.Click += (_, _) => SelectPage(_quizExamPage - 1);
            var next = CompactStudyAction(SecondaryButton(U("Kids.Quiz.ExamNext", "Next tests", "Sonraki testler"), ""));
            next.IsEnabled = _quizExamPage + 1 < totalPages;
            FocusTarget(next, "quiz.ExamPage.Next");
            next.Click += (_, _) => SelectPage(_quizExamPage + 1);
            var pager = new Grid { ColumnSpacing = 12, RowSpacing = 12, Children = { previous, pages, next } };
            ConfigureResponsiveGrid(pager, 3, 160);
            content.Children.Add(pager);
        }
    }

    private void AddSessionStart(string mode)
    {
        var content = new StackPanel { Spacing = 12 };
        if (mode == "cards")
        {
            content.Children.Add(StudyText(U("Kids.Cards.StartTitle", "Think of the meaning", "Anlamını düşün"), 24, emphasis: true));
            content.Children.Add(StudyText(U("Kids.Cards.StartHint", "Look at a word. Think, then show its meaning.", "Kelimeye bak. Düşün, sonra anlamını aç."), 18));
            var start = SessionStartButton();
            start.HorizontalAlignment = HorizontalAlignment.Left;
            content.Children.Add(start);
        }
        else
        {
            content.Children.Add(StudyText(U("Kids.Quiz.ExamTitle", "Choose a 20-question test island", "20 soruluk bir test adacığı seç"), 24, emphasis: true));
            AutomationProperties.SetHelpText(content, U("Kids.Quiz.ExamIntro", "Open any island to revisit older words whenever you want.", "Eski kelimelere dönmek için istediğin adacığı dilediğin zaman aç."));
            AddQuizExamPicker(content);
        }
        PageContent.Children.Add(StudySurface(content));
    }

    private void AddSessionProgress(StudySessionState session, string mode)
    {
        var content = new StackPanel { Spacing = 4 };
        if (mode == "quiz" && TryGetQuizExamInfo(session, out var examIndex, out var fromWord, out var toWord))
            content.Children.Add(StudyText(string.Format(CultureInfo.CurrentCulture,
                U("Kids.Quiz.ExamProgress", "Test {0} · words {1}-{2}", "Test {0} · kelimeler {1}-{2}"),
                examIndex + 1, fromWord, toWord), 18, emphasis: true));
        var label = mode == "cards" ? U("Kids.Cards.Count", "Cards done", "Bitirdiğin kartlar")
            : U("Kids.Quiz.Count", "Questions done", "Bitirdiğin sorular");
        content.Children.Add(StudyText($"{label}: {session.Answers.Count} / {session.WordKeys.Count}", 18, AppearancePalette.Current.BackgroundForegroundBrush));
        var progress = new ProgressBar { Minimum = 0, Maximum = Math.Max(1, session.WordKeys.Count), Value = session.Answers.Count };
        AutomationProperties.SetName(progress, label);
        content.Children.Add(progress);
        if (session.IsRetry) content.Children.Add(StudyText(U("Kids.Session.RetryHint", "Let's try these words again.", "Bu kelimeleri bir daha deneyelim."), 18, AppearancePalette.Current.BackgroundForegroundBrush));
        if (mode == "quiz")
        {
            var row = new Grid { ColumnSpacing = 12, RowSpacing = 12, Children = { content, QuizExamPickerButton("quiz.BackToExams") } };
            ConfigureResponsiveGrid(row, 2, 230);
            PageContent.Children.Add(row);
        }
        else PageContent.Children.Add(content);
    }

    private void AddMissingSessionNotice(string mode)
    {
        PageContent.Children.Add(new InfoBar
        {
            IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Warning,
            FontSize = ReadingSize(18),
            Message = U("Kids.Session.Missing", "Some words aren't here. Start a new practice. Your saved answers are safe.", "Bazı kelimeler burada yok. Yeni bir alıştırma başlat. Kaydedilen yanıtların güvende."),
        });
        AddSessionStart(mode);
    }

    private void AddCardsSearch(StudySessionState session, IReadOnlyList<VocabularyEntry> entries)
    {
        if (entries.Count == 0) return;
        var title = StudyText(T("Words.Search"), 18, emphasis: true);
        AutomationProperties.SetAutomationId(title, "cards.Search.Label");
        AutomationProperties.SetName(title, T("Words.Search"));
        PageContent.Children.Add(title);

        var search = new AutoSuggestBox
        {
            PlaceholderText = T("Words.Search"),
            QueryIcon = new SymbolIcon(Symbol.Find),
            Text = _cardsSearchQuery,
            FontSize = ReadingSize(18),
            MinHeight = 48,
            MinWidth = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        _cardsSearchBox = search;
        search.UpdateTextOnSelect = true;
        AutomationProperties.SetAutomationId(search, "cards.Search");
        AutomationProperties.SetName(search, T("Words.Search"));
        AutomationProperties.SetHelpText(search,
            U("Kids.Cards.SearchHelp",
                "Type a word with or without small spelling mistakes, then choose a suggestion to jump to that card.",
                "Kelimeyi küçük yazım hataları olsa da yaz, sonra öneriden seçip o karta zıpla."));
        FocusTarget(search, "cards.Search");

        var suggestionMap = new Dictionary<string, string>(StringComparer.Ordinal);

        void UpdateSuggestions(string query)
        {
            suggestionMap.Clear();
            if (string.IsNullOrWhiteSpace(query))
            {
                search.ItemsSource = null;
                search.IsSuggestionListOpen = false;
                return;
            }
            var items = new List<string>();
            foreach (var match in FindCardSearchMatches(entries, query))
            {
                var position = string.Format(CultureInfo.CurrentCulture, T("Cards.Position"), match.Index + 1, session.WordKeys.Count);
                var label = $"{match.Word} - {position}";
                if (!suggestionMap.TryAdd(label, match.Key))
                {
                    var detail = entries[match.Index].PartOfSpeech;
                    label = string.IsNullOrWhiteSpace(detail) ? $"{label} ({match.Index + 1})" : $"{label} - {detail}";
                    suggestionMap[label] = match.Key;
                }
                items.Add(label);
            }
            search.ItemsSource = items;
            search.IsSuggestionListOpen = items.Count > 0;
        }

        async Task JumpToCardAsync(string key)
        {
            var live = Session("cards");
            if (live is null) return;
            var targetIndex = live.WordKeys.IndexOf(key);
            if (targetIndex < 0)
            {
                ShowNotice(U("Library.Empty", "No matching words", "Eşleşen kelime yok"),
                    U("Library.EmptyHint", "Try a different search, clear the filters, or choose another study language or level.", "Farklı bir arama deneyin, filtreleri temizleyin veya başka bir öğrenme dili ya da seviye seçin."),
                    InfoBarSeverity.Informational);
                return;
            }
            if (targetIndex == live.Index)
            {
                RequestSessionFocus("cards");
                RenderCards();
                return;
            }
            if (await MutateStudyAsync(() =>
            {
                live.Index = targetIndex;
                _progress.CardPositions[StudyContext] = live.WordKeys[targetIndex];
            }, keepUndo: true))
            {
                _cardRevealed = false;
                _revealedCardKey = null;
                RequestSessionFocus("cards");
                RenderCards();
            }
        }

        search.TextChanged += (_, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            _cardsSearchQuery = search.Text;
            UpdateSuggestions(_cardsSearchQuery);
        };
        search.QuerySubmitted += async (_, args) =>
        {
            if (_studyBusy || _dialogOpen || _navigationBusy || _currentPage != "cards") return;
            _cardsSearchQuery = args.QueryText;
            if (args.ChosenSuggestion is string suggestion && suggestionMap.TryGetValue(suggestion, out var key))
            {
                await JumpToCardAsync(key);
                return;
            }
            var fallback = FindCardSearchMatches(entries, _cardsSearchQuery, 1).FirstOrDefault();
            if (fallback is not null)
            {
                await JumpToCardAsync(fallback.Key);
                return;
            }
            ShowNotice(U("Library.Empty", "No matching words", "Eşleşen kelime yok"),
                U("Library.EmptyHint", "Try a different search, clear the filters, or choose another study language or level.", "Farklı bir arama deneyin, filtreleri temizleyin veya başka bir öğrenme dili ya da seviye seçin."),
                InfoBarSeverity.Informational);
        };

        UpdateSuggestions(_cardsSearchQuery);

        var surface = new StackPanel { Spacing = 8 };
        surface.Children.Add(search);
        surface.Children.Add(StudyText(U("Kids.Cards.SearchHint",
            "Shortcut: Ctrl+F focuses card search.",
            "Kısayol: Ctrl+F kart aramayı odaklar."), 16, AppearancePalette.Current.BackgroundForegroundBrush));
        PageContent.Children.Add(StudySurface(surface, 12));
    }

    private void RenderCards()
    {
        StopSpeechPlayback();
        _cardsSearchBox = null;
        PageContent.Children.Clear();
        AddPageHeader(T("Cards.Title"), U("Kids.Cards.Subtitle", "Think, look, then choose.", "Düşün, bak, sonra seç."));
        var session = Session("cards");
        if (session is null) { AddSessionStart("cards"); return; }
        if (TryUpgradeLegacyCardsSession(session, out var upgraded)) session = upgraded;
        var byKey = _words
            .DistinctBy(word => word.Key, StringComparer.Ordinal)
            .ToDictionary(word => word.Key, StringComparer.Ordinal);
        if (session.WordKeys.Any(key => !byKey.ContainsKey(key))) { AddMissingSessionNotice("cards"); return; }
        var entries = session.WordKeys.Select(key => byKey[key]).ToArray();
        AddSessionProgress(session, "cards");
        if (session.Index >= session.WordKeys.Count)
        {
            AddMissedSessionSummary("cards", session);
            return;
        }
        AddCardsSearch(session, entries);
        var entry = entries[session.Index];
        if (_revealedCardKey != entry.Key) { _cardRevealed = false; _revealedCardKey = entry.Key; }
        var completed = session.Answers.ContainsKey(entry.Key);
        var content = new StackPanel { Spacing = 12 };
        var context = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        context.Children.Add(StudyText(string.Format(CultureInfo.CurrentCulture,
            U("Cards.Position", "Card {0} of {1}", "Kart {0} / {1}"), session.Index + 1, session.WordKeys.Count), 18));
        var step = StudyText(completed ? U("Kids.Cards.StepRated", "Ready for the next card?", "Sonraki karta hazır mısın?")
            : _cardRevealed ? U("Kids.Cards.StepNext", "Read the meaning, then choose Next.", "Anlamı oku, sonra Sonraki'ni seç.")
            : U("Kids.Cards.StepRecall", "Think. What does it mean?", "Düşün. Anlamı ne?"), 18, emphasis: true);
        StudyLive(step, "cards.Step");
        context.Children.Add(step);
        ConfigureResponsiveGrid(context, 2, Math.Max(230, Font(230)));
        content.Children.Add(context);
        var word = new StackPanel { Spacing = 4 };
        word.Children.Add(StudyText(entry.Word, 40, emphasis: true, selectable: true));
        var listen = CompactStudyAction(SecondaryButton(T("Cards.Listen"), ""));
        ApplyCardsActionVisual(listen);
        FocusTarget(listen, "cards.Listen");
        listen.Click += async (_, _) => await PlayWordAsync(entry.Word, listen);
        content.Children.Add(StudyWordRow(word, listen));
        if (_cardRevealed || completed)
        {
            content.Children.Add(StudyDetail(T("Cards.Meaning"), LocalizedPart(entry.Definition), 24));
            if (!string.IsNullOrWhiteSpace(entry.Example)) content.Children.Add(StudyDetail(T("Cards.Example"), entry.Example, 20));
        }
        var reveal = ReadableStudyAction(AccentButton(U("Kids.Cards.Reveal", "Show meaning", "Anlamı göster"), ""));
        AutomationProperties.SetHelpText(reveal, U("Kids.Cards.ClickHelp", "Click Show meaning. Then choose Next.", "Anlamı göster'e tıkla. Sonra Sonraki'ni seç."));
        reveal.IsEnabled = !_cardRevealed && !completed;
        reveal.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        FocusTarget(reveal, "cards.Reveal");
        reveal.Click += (_, _) => RevealCard();
        if (!_cardRevealed && !completed) content.Children.Add(reveal);
        PageContent.Children.Add(StudySurface(content));
        var navigation = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        var previous = CompactStudyAction(SecondaryButton(T("Cards.Previous"), ""));
        ApplyCardsActionVisual(previous);
        previous.IsEnabled = session.Index > 0;
        SetCardsActionAccessibility(previous, T("Cards.Previous"));
        FocusTarget(previous, "cards.Previous");
        previous.Click += async (_, _) => await MoveCardAsync(-1);
        navigation.Children.Add(previous);
        navigation.Children.Add(BuildUndoButton());
        var next = _cardRevealed || completed ? ReadableStudyAction(AccentButton(T("Cards.Next"), "")) : CompactStudyAction(SecondaryButton(T("Cards.Next"), ""));
        ApplyCardsActionVisual(next, primary: _cardRevealed || completed);
        next.HorizontalAlignment = HorizontalAlignment.Stretch;
        next.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        next.IsEnabled = _cardRevealed || completed;
        SetCardsActionAccessibility(next, T("Cards.Next"));
        FocusTarget(next, "cards.Next");
        next.Click += async (_, _) => await NextCardAsync();
        navigation.Children.Add(next);
        var tools = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        tools.Children.Add(StudyMarkButton(entry, known: false));
        tools.Children.Add(StudyMarkButton(entry, known: true));
        ConfigureResponsiveGrid(tools, 2, Math.Max(180, Font(180)));
        var list = new StackPanel { Spacing = 12 };
        list.Children.Add(tools);
        if (!string.IsNullOrWhiteSpace(entry.PartOfSpeech)) list.Children.Add(StudyText(entry.PartOfSpeech, 18));
        AddCardsHintButton(entry, list, navigation);
        ConfigureResponsiveGrid(navigation, 5, Math.Max(130, Font(130)));
        PageContent.Children.Add(navigation);
    }

    private void AddMissedSessionSummary(string mode, StudySessionState session)
    {
        var keys = LearningEngine.MissedKeys(session).ToHashSet(StringComparer.Ordinal);
        var missed = session.WordKeys.Where(keys.Contains).Select(key => _words.First(word => word.Key == key)).ToArray();
        var summary = new StackPanel { Spacing = 12 };
        var title = StudyText(U("Kids.Session.Complete", "Great practice!", "Güzel çalıştın!"), 24, emphasis: true);
        StudyLive(title, $"{mode}.Complete");
        summary.Children.Add(title);
        summary.Children.Add(StudyText(string.Format(CultureInfo.CurrentCulture,
            U("Kids.Session.Practiced", "You practiced {0} words.", "{0} kelime çalıştın."), session.Answers.Count), 20));
        var missedLabel = $"{U("Kids.Session.TryAgain", "Words to try again", "Bir daha deneyeceğin kelimeler")}: {missed.Length}";
        if (missed.Length > 0)
        {
            var page = 0;
            var detailHost = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
            var status = StudyText("", 18, emphasis: true);
            StudyLive(status, $"{mode}.MissedPage");
            var previous = CompactStudyAction(SecondaryButton(U("Library.Previous", "Previous word", "Önceki kelime"), ""));
            var next = CompactStudyAction(SecondaryButton(U("Library.Next", "Next word", "Sonraki kelime"), ""));
            void RefreshMissed()
            {
                var word = missed[page];
                var wordDetail = StudyDetail(word.Word, LocalizedPart(word.Definition), 24);
                if (!string.IsNullOrWhiteSpace(word.Example)) wordDetail.Children.Add(StudyText(word.Example, 20, selectable: true));
                detailHost.Content = wordDetail;
                Announce(status, $"{page + 1} / {missed.Length}");
                previous.IsEnabled = page > 0;
                next.IsEnabled = page + 1 < missed.Length;
            }
            previous.Click += (_, _) => { if (page > 0) { page--; RefreshMissed(); } };
            next.Click += (_, _) => { if (page + 1 < missed.Length) { page++; RefreshMissed(); } };
            var navigation = new Grid { ColumnSpacing = 8, RowSpacing = 8, Children = { previous, next } };
            ConfigureResponsiveGrid(navigation, 2, 150);
            var details = new StackPanel { Spacing = 12, Children = { status, detailHost, navigation } };
            RefreshMissed();
            summary.Children.Add(StudyPopupButton(missedLabel, details, $"{mode}.MissedWords"));
        }
        else summary.Children.Add(StudyText(U("Kids.Session.AllDone", "All done!", "Hepsi bitti!"), 18));
        var actions = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        if (mode == "cards" && missed.Length > 0)
        {
            var retry = ReadableStudyAction(AccentButton(U("Kids.Session.Retry", "Try these words again", "Bu kelimeleri tekrar dene"), ""));
            retry.HorizontalAlignment = HorizontalAlignment.Stretch;
            retry.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            FocusTarget(retry, $"{mode}.RetryMissed");
            retry.Click += async (_, _) => await StartCardsSessionAsync(missed, isRetry: true);
            actions.Children.Add(retry);
        }
        if (mode == "quiz")
        {
            var restart = ReadableStudyAction(AccentButton(U("Kids.Quiz.RestartExam", "Try this test again", "Bu testi yeniden çöz"), ""));
            FocusTarget(restart, "quiz.RestartExam");
            var completedExam = _quizExamIndex;
            restart.Click += async (_, _) =>
            {
                if (completedExam is { } exam && IsWithin(restart, PageContent))
                    await StartQuizExamSessionAsync(exam, restart: true);
            };
            actions.Children.Add(restart);
            var changeExam = missed.Length > 0
                ? CompactStudyAction(SecondaryButton(U("Kids.Quiz.ChangeExam", "Choose another test island", "Başka bir test adacığı seç"), ""))
                : ReadableStudyAction(AccentButton(U("Kids.Quiz.ChangeExam", "Choose another test island", "Başka bir test adacığı seç"), ""));
            changeExam.HorizontalAlignment = HorizontalAlignment.Stretch;
            changeExam.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            AutomationProperties.SetHelpText(changeExam,
                U("Kids.Quiz.ChangeExamHelp", "Open the test-island picker.", "Test adacığı seçicisini aç."));
            ToolTipService.SetToolTip(changeExam,
                U("Kids.Quiz.ChangeExamHelp", "Open the test-island picker.", "Test adacığı seçicisini aç."));
            FocusTarget(changeExam, "quiz.ChangeExam");
            changeExam.Click += (_, _) => ShowQuizExamPicker();
            actions.Children.Add(changeExam);
        }
        else
        {
            actions.Children.Add(SessionStartButton(restart: true, secondary: missed.Length > 0));
        }
        ConfigureResponsiveGrid(actions, 2, Math.Max(230, Font(230)));
        summary.Children.Add(actions);
        if (mode == "quiz")
        {
            var correct = session.Answers.Count(answer => answer.Value);
            var result = StudyText($"{T("Stats.Success")}: {(session.Answers.Count == 0 ? 0 : 100.0 * correct / session.Answers.Count):0}% · {correct} / {session.WordKeys.Count}", 18);
            summary.Children.Add(StudyPopupButton(U("Kids.Session.Details", "More details", "Daha fazla bilgi"), result, "quiz.Results"));
        }
        if (mode == "cards")
        {
            var undo = BuildUndoButton();
            undo.HorizontalAlignment = HorizontalAlignment.Left;
            summary.Children.Add(undo);
        }
        PageContent.Children.Add(StudySurface(summary));
    }

    private void RevealCard()
    {
        if (_studyBusy || _dialogOpen || _cardRevealed || Session("cards") is not { } session || session.Index >= session.WordKeys.Count || session.Answers.ContainsKey(session.WordKeys[session.Index])) return;
        _cardRevealed = true;
        _revealedCardKey = session.WordKeys[session.Index];
        RequestUiFocus("cards.Next");
        RenderCards();
    }

    private async Task NextCardAsync()
    {
        var session = Session("cards");
        if (session is null || session.Index >= session.WordKeys.Count) return;
        var key = session.WordKeys[session.Index];
        if (!session.Answers.ContainsKey(key) && !_cardRevealed) return;
        if (session.Answers.ContainsKey(key))
        {
            await MoveCardAsync(1);
            return;
        }
        var before = JsonSerializer.Serialize(_progress);
        if (await MutateStudyAsync(() =>
        {
            session.Answers[key] = true;
            session.Index = LearningEngine.NextUnansweredIndex(session);
            if (session.Index < session.WordKeys.Count) _progress.CardPositions[StudyContext] = session.WordKeys[session.Index];
            else if (session.WordKeys.Count > 0) _progress.CardPositions[StudyContext] = session.WordKeys[0];
        }, keepUndo: true))
        {
            _cardUndo = before;
            _cardUndoContext = StudyContext;
            _cardRevealed = false;
        }
        RequestSessionFocus("cards");
        RenderCards();
    }

    private async Task MoveCardAsync(int delta)
    {
        var session = Session("cards");
        if (session is null || session.Index >= session.WordKeys.Count) return;
        if (delta > 0 && !session.Answers.ContainsKey(session.WordKeys[session.Index])) return;
        var index = Math.Clamp(session.Index + delta, 0,
            session.Answers.Count == session.WordKeys.Count ? session.WordKeys.Count : session.WordKeys.Count - 1);
        await MutateStudyAsync(() =>
        {
            session.Index = index;
            if (index < session.WordKeys.Count) _progress.CardPositions[StudyContext] = session.WordKeys[index];
            else if (session.WordKeys.Count > 0) _progress.CardPositions[StudyContext] = session.WordKeys[0];
        }, keepUndo: true);
        _cardRevealed = false;
        RequestSessionFocus("cards");
        RenderCards();
    }

    private Button BuildUndoButton()
    {
        var undoLabel = U("Kids.Cards.Undo", "Undo my choice", "Seçimimi geri al");
        var undo = CompactStudyAction(SecondaryButton(undoLabel, ""));
        ApplyCardsActionVisual(undo);
        undo.IsEnabled = _cardUndo is not null && _cardUndoContext == StudyContext;
        SetCardsActionAccessibility(undo, undoLabel, T("Kids.Cards.UndoHelp"));
        FocusTarget(undo, "cards.Undo");
        undo.Click += async (_, _) =>
        {
            if (_cardUndo is not { } snapshot || _cardUndoContext != StudyContext) return;
            await MutateStudyAsync(() => _progress = JsonSerializer.Deserialize<ProgressState>(snapshot)!);
            _cardRevealed = false;
            RequestSessionFocus("cards");
            RenderCards();
        };
        return undo;
    }

    private static void SetCardsActionAccessibility(Button button, string name, string? help = null)
    {
        help ??= name;
        void Apply()
        {
            AutomationProperties.SetName(button, name);
            AutomationProperties.SetHelpText(button, help);
            ToolTipService.SetToolTip(button, help);
        }
        Apply();
        button.Loaded += (_, _) => Apply();
    }

    private void AddCardsHintButton(VocabularyEntry entry, UIElement wordList, Grid actions)
    {
        var sharedMarks = new StackPanel { Spacing = 12 };
        sharedMarks.Children.Add(StudyText(U("Kids.Cards.WordListSync",
            "Cards and Words share known marks. Mark this word in either place.",
            "Kartlar ve Kelimeler biliniyor işaretlerini paylaşır. Bu kelimeyi iki bölümden birinde işaretleyebilirsin."), 18));
        sharedMarks.Children.Add(wordList);
        var markUnknownLabel = U("Kids.Cards.Unknown", "I don't know this word", "Bu kelimeyi bilmiyorum");
        var markUnknownHelp = U("Library.RepeatHelp", "Schedule this word for review tomorrow. It will then appear under Due.", "Bu kelimeyi yarın tekrar etmek için planla. Sonra Tekrar zamanı filtresinde görünür.");
        var markUnknown = CompactStudyAction(SecondaryButton(markUnknownLabel, "\uE9CE"));
        ApplyCardsActionVisual(markUnknown);
        SetCardsActionAccessibility(markUnknown, markUnknownLabel, markUnknownHelp);
        FocusTarget(markUnknown, "cards.MarkUnknown");
        markUnknown.Click += async (_, _) =>
        {
            if (await MutateStudyAsync(() =>
            {
                _progress.KnownWords.Remove(entry.Key);
                LearningEngine.ScheduleReview(_progress, entry.Key, Today);
            }))
            {
                ShowNotice(U("Library.RepeatScheduled", "Review scheduled for tomorrow", "Yarın için tekrar planlandı"), markUnknownHelp, InfoBarSeverity.Success);
                RequestUiFocus("cards.WordList");
                RenderCards();
            }
        };
        sharedMarks.Children.Add(markUnknown);
        var openWords = CompactStudyAction(SecondaryButton(U("Kids.Cards.OpenInWords",
            "Open this word in Words", "Bu kelimeyi Kelimeler'de aç"), ""));
        ApplyCardsActionVisual(openWords);
        SetCardsActionAccessibility(openWords, T("Kids.Cards.OpenInWords"));
        FocusTarget(openWords, "cards.OpenWords");
        openWords.Click += (_, _) =>
        {
            _libraryQuery = entry.Word;
            _libraryCategory = null;
            _libraryFavorites = _libraryKnown = _libraryDue = false;
            _libraryPage = 0;
            NavigateTo("words", WordsItem);
        };
        sharedMarks.Children.Add(openWords);
        var help = new StackPanel { Spacing = 12 };
        help.Children.Add(StudyText(U("Kids.Cards.ClickHelp", "Click Show meaning. Then choose Next.", "Anlamı göster'e tıkla. Sonra Sonraki'ni seç."), 20, emphasis: true));
        help.Children.Add(StudyText(U("Kids.Cards.Shortcuts", "Ctrl+F: focus search · Space: show meaning · Enter: next after showing meaning · ←/→: previous or next card · U: mark as known", "Ctrl+F: aramaya odaklan · Boşluk: anlamı göster · Enter: anlamı açtıktan sonra sonraki kart · ←/→: önceki veya sonraki kart · U: biliyorum işareti"), 18));
        help.Children.Add(StudyText(U("Kids.Cards.UndoHelp", "Undo brings back your last completed card. Use it before another learning action or leaving this page.", "Geri al, son tamamladığın kartı geri getirir. Başka bir öğrenme işleminden önce veya bu sayfadan ayrılmadan kullan."), 18));
        var wordListButton = StudyPopupButton(U("Kids.Cards.WordList", "My word list", "Kelime listem"), sharedMarks, "cards.WordList");
        ApplyCardsActionVisual(wordListButton);
        SetCardsActionAccessibility(wordListButton, T("Kids.Cards.WordList"));
        var howToPlay = StudyPopupButton(U("Kids.Study.Help", "How to play", "Nasıl oynarım?"), help, "cards.Help");
        ApplyCardsActionVisual(howToPlay);
        SetCardsActionAccessibility(howToPlay, T("Kids.Study.Help"));
        actions.Children.Add(wordListButton);
        actions.Children.Add(howToPlay);
    }


    private void RenderQuiz()
    {
        StopSpeechPlayback();
        PageContent.Children.Clear();
        _quizChoiceButtons.Clear();
        _quizContinue = null;
        AddPageHeader(T("Quiz.Title"), U("Kids.Quiz.Subtitle", "Read, choose, then keep going.", "Oku, seç, sonra devam et."));
        var session = SelectedQuizSession();
        if (session is null || !TryGetQuizExamInfo(session, out var selectedExam, out _, out _) || selectedExam != _quizExamIndex)
        {
            ResetQuizSelection();
            AddSessionStart("quiz");
            return;
        }
        AddSessionProgress(session, "quiz");
        if (session.WordKeys.Any(key => !_words.Any(word => word.Key == key))) { AddMissingSessionNotice("quiz"); return; }
        if (session.Index >= session.WordKeys.Count)
        {
            AddMissedSessionSummary("quiz", session);
            return;
        }
        var answer = _words.First(word => word.Key == session.WordKeys[session.Index]);
        var options = QuizOptions(session.Index);
        if (options is null || options.WordKeys.Count < 2 || !options.WordKeys.Contains(answer.Key) ||
            options.WordKeys.Any(key => !_words.Any(word => word.Key == key))) { AddMissingSessionNotice("quiz"); return; }
        var answered = session.Answers.TryGetValue(answer.Key, out var correctAnswer);
        var quizChoicePalette = NormalizeQuizChoicePalette(_settings.QuizChoicePalette);
        var palette = AppearancePalette.Current;
        var question = new StackPanel { Spacing = 12 };
        question.Children.Add(StudyText(string.Format(CultureInfo.CurrentCulture,
            U("Quiz.Position", "Question {0} of {1}", "Soru {0} / {1}"), session.Index + 1, session.WordKeys.Count), 18));
        question.Children.Add(StudyLabel(U("Kids.Quiz.ChooseWord", "Choose a word", "Bir kelime seç")));
        // The question itself must outweigh its own caption and the answer choices,
        // not blend in below them -- bold and larger than both.
        var meaning = StudyText(LocalizedPart(answer.Definition), 26, emphasis: true, selectable: true);
        AutomationProperties.SetAutomationId(meaning, "quiz.Question");
        AutomationProperties.SetName(meaning, meaning.Text);
        AutomationProperties.SetHelpText(meaning, U("Kids.Accessibility.ReadQuestion", "Read the question, then use the control below.", "Soruyu oku, sonra aşağıdaki kontrolü kullan."));
        AutomationProperties.SetHeadingLevel(meaning, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
        question.Children.Add(meaning);
        if (!answered) question.Children.Add(StudyLabel(U("Kids.Quiz.ChooseOne", "Choose one answer.", "Bir yanıt seç.")));
        PageContent.Children.Add(StudySurface(question));
        var selected = options.Answers.Keys.FirstOrDefault();
        var choices = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        AutomationProperties.SetAutomationId(choices, "quiz.Choices");
        AutomationProperties.SetName(choices, U("Kids.Quiz.ChooseOne", "Choose one answer.", "Bir yanıt seç."));
        AutomationProperties.SetHelpText(choices, U("Kids.Quiz.ChooseOne", "Choose one answer.", "Bir yanıt seç."));
        AutomationProperties.SetAccessibilityView(choices, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Control);
        for (var index = 0; index < options.WordKeys.Count; index++)
        {
            var choice = _words.First(word => word.Key == options.WordKeys[index]);
            var choiceVisual = QuizChoiceVisual(index, quizChoicePalette);
            var choiceBackgroundBrush = new SolidColorBrush(choiceVisual.Background);
            var choiceForegroundBrush = new SolidColorBrush(choiceVisual.Foreground);
            var isCorrectChoice = answered && choice.Key == answer.Key;
            var isWrongSelection = answered && choice.Key == selected && !isCorrectChoice;
            if (answered && !isCorrectChoice && !isWrongSelection) continue;
            var status = isCorrectChoice ? "✓ " + U("Kids.Quiz.CorrectChoice", "This word fits!", "Bu kelime uyuyor!")
                : isWrongSelection ? "✕ " + U("Kids.Quiz.YourChoice", "Your choice", "Senin seçimin") : null;
            var brushes = isCorrectChoice || isWrongSelection ? StudyResultBrushes(isCorrectChoice)
                : (Background: (Brush)choiceBackgroundBrush, Foreground: (Brush)choiceForegroundBrush);
            var label = StudyText(choice.Word, 24, brushes.Foreground, emphasis: true);
            var content = StudyOptionContent(index + 1, label, status, brushes.Background, brushes.Foreground);
            if (answered)
            {
                // Read-only result text keeps its contrast instead of inheriting disabled-button gray.
                AutomationProperties.SetName(label, $"{index + 1}: {choice.Word}");
                AutomationProperties.SetAutomationId(label, $"quiz.Choice.{index}");
                if (status is not null) AutomationProperties.SetHelpText(label, status);
                choices.Children.Add(StudySurface(content, 14, brushes.Background,
                    isCorrectChoice || isWrongSelection ? brushes.Foreground : palette.BorderBrush));
            }
            else
            {
                var button = StudyOptionButton(content, $"{index + 1}: {choice.Word}", choiceVisual);
                AutomationProperties.SetName(button, $"{index + 1}: {choice.Word}");
                AutomationProperties.SetHelpText(button, U("Kids.Accessibility.ChooseAnswer", "Choose this word as your answer.", "Bu kelimeyi yanıt olarak seç."));
                AutomationProperties.SetAcceleratorKey(button, (index + 1).ToString(CultureInfo.InvariantCulture));
                button.IsEnabled = !answered;
                FocusTarget(button, $"quiz.Choice.{index}");
                button.Click += async (_, _) => await AnswerQuizAsync(choice.Key);
                _quizChoiceButtons.Add(button);
                choices.Children.Add(button);
            }
        }
        ConfigureStudyChoices(choices, 2, 230);
        PageContent.Children.Add(choices);
        var feedbackMessage = correctAnswer ? $"✓ {U("Kids.Quiz.GotIt", "You got it!", "Bildin!")} · {answer.Word}"
            : $"{U("Kids.Quiz.GoodTry", "Good try!", "Güzel deneme!")} {U("Kids.Quiz.AnswerWord", "Here is the word:", "İşte kelime:")} {answer.Word}";
        _quizContinue = ReadableStudyAction(AccentButton(U("Quiz.Continue", "Continue", "Devam"), ""));
        _quizContinue.IsEnabled = answered;
        _quizContinue.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        if (answered) AutomationProperties.SetHelpText(_quizContinue, feedbackMessage);
        AutomationProperties.SetHelpText(_quizContinue, answered
            ? $"{feedbackMessage} {U("Kids.Accessibility.Continue", "Continue to the next question.", "Sonraki soruya geç.")}"
            : U("Kids.Accessibility.Continue", "Continue to the next question.", "Sonraki soruya geç."));
        AutomationProperties.SetAcceleratorKey(_quizContinue, "Enter");
        FocusTarget(_quizContinue, "quiz.Continue");
        _quizContinue.Click += async (_, _) => await ContinueQuizAsync();
        if (answered)
            AddQuizFeedback(answer, _words.FirstOrDefault(word => word.Key == selected), correctAnswer, feedbackMessage, _quizContinue);
        else PageContent.Children.Add(_quizContinue);
        var help = new StackPanel { Spacing = 12 };
        help.Children.Add(StudyText(U("Kids.Quiz.ClickHelp", "Click the word that fits. Read the answer, then click Continue.", "Uyan kelimeye tıkla. Yanıtı oku, sonra Devam'a tıkla."), 20, emphasis: true));
        help.Children.Add(StudyText(U("Kids.Quiz.Shortcuts", "1–4: choose a word · Enter: continue after your answer", "1–4: kelime seç · Enter: yanıtından sonra devam et"), 18));
        if (!answered) PageContent.Children.Add(StudyPopupButton(U("Kids.Study.Help", "How to play", "Nasıl oynarım?"), help, "quiz.Help"));
    }

    private static (Brush Background, Brush Foreground) StudyResultBrushes(bool correct)
    {
        var dark = AppearancePalette.Current.Theme == ElementTheme.Dark;
        var background = correct
            ? (dark ? Color.FromArgb(255, 20, 54, 40) : Color.FromArgb(255, 236, 253, 245))
            : (dark ? Color.FromArgb(255, 63, 45, 13) : Color.FromArgb(255, 255, 251, 235));
        var preferred = correct
            ? (dark ? Color.FromArgb(255, 187, 247, 208) : Color.FromArgb(255, 6, 78, 59))
            : (dark ? Color.FromArgb(255, 254, 243, 199) : Color.FromArgb(255, 113, 63, 18));
        return (new SolidColorBrush(background), new SolidColorBrush(AppearancePalette.EnsureTextContrast(background, preferred)));
    }

    private void AddQuizFeedback(VocabularyEntry answer, VocabularyEntry? selectedWord, bool correctAnswer, string message, Button continueButton)
    {
        var brushes = StudyResultBrushes(correctAnswer);
        var backgroundColor = (brushes.Background as SolidColorBrush)?.Color ?? AppearancePalette.Current.Box;
        var valueColor = EnsureStrongTextContrast(backgroundColor,
            (brushes.Foreground as SolidColorBrush)?.Color ?? AppearancePalette.Current.BoxForeground);
        var labelColor = EnsureStrongTextContrast(backgroundColor,
            correctAnswer ? Color.FromArgb(255, 134, 239, 172) : Color.FromArgb(255, 253, 186, 116));
        var labelBrush = new SolidColorBrush(labelColor);
        var valueBrush = new SolidColorBrush(valueColor);
        var feedback = new StackPanel { Spacing = 12 };
        var status = StudyText(message, 22, valueBrush, emphasis: true);
        StudyLive(status, "quiz.Feedback");
        feedback.Children.Add(status);
        var summary = new StackPanel { Spacing = 8 };
        void AddSummaryRow(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var row = new Grid { ColumnSpacing = 10, RowSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var key = StudyText(label + ":", 20, labelBrush, emphasis: true);
            key.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
            key.VerticalAlignment = VerticalAlignment.Top;
            var text = StudyText(value.Trim(), 20, valueBrush, selectable: true);
            Grid.SetColumn(text, 1);
            row.Children.Add(key);
            row.Children.Add(text);
            summary.Children.Add(row);
        }
        AddSummaryRow(U("Quiz.CorrectAnswer", "Correct answer", "Doğru yanıt"), answer.Word);
        if (!correctAnswer || selectedWord is not null)
            AddSummaryRow(U("Quiz.Selected", "Your answer", "Yanıtınız"),
                selectedWord?.Word ?? U("Kids.Quiz.Unrecorded", "This choice wasn't saved.", "Bu seçim kaydedilmemiş."));
        if (!string.IsNullOrWhiteSpace(answer.Example))
            AddSummaryRow(T("Cards.Example"), ExampleWithTurkish(answer.Example));
        feedback.Children.Add(summary);
        // Continue is the primary action here -- give it its own full-width row
        // like every other primary CTA in the app, instead of squeezing it into
        // a column next to the status text while the secondary "look at the
        // answer" button below got the full width and looked more important.
        continueButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        continueButton.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        feedback.Children.Add(continueButton);
        var details = new StackPanel { Spacing = 12 };
        if (!correctAnswer)
        {
            var comparison = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
            var selected = StudyDetail(U("Kids.Quiz.YourChoice", "Your choice", "Senin seçimin"),
                selectedWord?.Word ?? U("Kids.Quiz.Unrecorded", "This choice wasn't saved.", "Bu seçim kaydedilmemiş."), 24, brushes.Foreground);
            if (selectedWord is not null) selected.Children.Add(StudyText(LocalizedPart(selectedWord.Definition), 24, brushes.Foreground, selectable: true));
            comparison.Children.Add(selected);
            var correct = StudyDetail(U("Kids.Quiz.AnswerWord", "Here is the word:", "İşte kelime:"), answer.Word, 24, brushes.Foreground);
            correct.Children.Add(StudyText(LocalizedPart(answer.Definition), 24, brushes.Foreground, selectable: true));
            comparison.Children.Add(correct);
            ConfigureStudyChoices(comparison, 2, 230);
            details.Children.Add(comparison);
        }
        if (!string.IsNullOrWhiteSpace(answer.Example)) details.Children.Add(StudyDetail(T("Cards.Example"), answer.Example, 20, brushes.Foreground));
        if (details.Children.Count > 0)
        {
            var lookAtAnswer = StudyContentDialogButton(
                U("Kids.Quiz.AnswerDetails", "Look at the answer", "Yanıta bakalım"), details, "quiz.AnswerDetails",
                U("Kids.Quiz.AnswerDetailsHelp", "Open the correct answer, your choice and the example sentence.", "Doğru yanıtı, seçimini ve örnek cümleyi aç."));
            // Keep secondary styling, but match Continue's full-width touch target footprint.
            lookAtAnswer.HorizontalAlignment = HorizontalAlignment.Stretch;
            lookAtAnswer.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            feedback.Children.Add(lookAtAnswer);
        }
        PageContent.Children.Add(StudySurface(feedback, 14, brushes.Background, brushes.Foreground));
    }

    private async Task AnswerQuizAsync(string selected)
    {
        var session = SelectedQuizSession();
        if (session is null || session.Index >= session.WordKeys.Count) return;
        var key = session.WordKeys[session.Index];
        var options = QuizOptions(session.Index);
        if (session.Answers.ContainsKey(key) || options is null || !options.WordKeys.Contains(selected)) return;
        await MutateStudyAsync(() =>
        {
            var correct = key == selected;
            if (!LearningEngine.RecordQuizResponse(_progress, session, key, correct, Today)) return;
            options.Answers[selected] = correct;
            options.Index = options.WordKeys.Count;
        });
        RequestSessionFocus("quiz");
        RenderQuiz();
    }

    private async Task ContinueQuizAsync()
    {
        var session = SelectedQuizSession();
        if (session is null || session.Index >= session.WordKeys.Count || !session.Answers.ContainsKey(session.WordKeys[session.Index])) return;
        await MutateStudyAsync(() => session.Index++);
        RequestSessionFocus("quiz");
        RenderQuiz();
    }

    private async void OnStudyKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_studyBusy || _navigationBusy || _dialogOpen || _activeGame is not null || e.KeyStatus.WasKeyDown) return;
        var controlDown = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (_currentPage == "cards" && controlDown && e.Key == VirtualKey.F)
        {
            e.Handled = true;
            if (_cardsSearchBox is not null && _cardsSearchBox.IsLoaded && _cardsSearchBox.IsEnabled && IsWithin(_cardsSearchBox, PageContent))
            {
                FocusTarget(_cardsSearchBox, "cards.Search");
            }
            else
            {
                RequestUiFocus("cards.Search", "cards");
                RenderCards();
            }
            return;
        }
        // Native Click may already have rebuilt the page before this event bubbles.
        // Inspect its original source too, not just the possibly changed focus target.
        if (e.Key is VirtualKey.Space or VirtualKey.Enter)
            for (var source = e.OriginalSource as DependencyObject; source is not null; source = VisualTreeHelper.GetParent(source))
                if (source is Microsoft.UI.Xaml.Controls.Primitives.ButtonBase) return;
        // Leave all editing, combo, slider, navigation and modifier shortcuts to WinUI.
        for (DependencyObject? focus = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
             focus is not null; focus = VisualTreeHelper.GetParent(focus))
        {
            if (focus is TextBox or PasswordBox or RichEditBox or AutoSuggestBox or ComboBox or Slider or NumberBox or NavigationViewItem) return;
            // Native button activation owns Space/Enter; never also run a custom action.
            if (focus is Microsoft.UI.Xaml.Controls.Primitives.ButtonBase && e.Key is VirtualKey.Space or VirtualKey.Enter) return;
        }
        foreach (var modifier in new[] { VirtualKey.Control, VirtualKey.Menu, VirtualKey.Shift })
            if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(modifier).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) return;
        var number = (int)e.Key - (int)VirtualKey.Number1;
        if (number is < 0 or > 3) number = (int)e.Key - (int)VirtualKey.NumberPad1;
        if (_currentPage == "cards")
        {
            if (e.Key == VirtualKey.Space && !_cardRevealed && Session("cards") is { } cards && cards.Index < cards.WordKeys.Count && !cards.Answers.ContainsKey(cards.WordKeys[cards.Index])) { e.Handled = true; RevealCard(); }
            else if (e.Key == VirtualKey.U && Session("cards") is { } current && current.Index < current.WordKeys.Count)
            {
                e.Handled = true;
                var key = current.WordKeys[current.Index];
                if (await MutateStudyAsync(() => Toggle(_progress.KnownWords, key)))
                {
                    RequestUiFocus("cards.WordList");
                    RenderCards();
                }
            }
            else if (e.Key is VirtualKey.Left or VirtualKey.Right) { e.Handled = true; await MoveCardAsync(e.Key == VirtualKey.Left ? -1 : 1); }
            else if (e.Key == VirtualKey.Enter && _cardRevealed) { e.Handled = true; await NextCardAsync(); }
        }
        else if (_currentPage == "quiz")
        {
            if (number >= 0 && number < _quizChoiceButtons.Count && _quizChoiceButtons[number].IsEnabled)
            {
                e.Handled = true;
                var session = SelectedQuizSession();
                var options = session is null ? null : QuizOptions(session.Index);
                if (options is not null) await AnswerQuizAsync(options.WordKeys[number]);
            }
            else if (e.Key == VirtualKey.Enter && _quizContinue?.IsEnabled == true) { e.Handled = true; await ContinueQuizAsync(); }
        }
        else if (_currentPage == "phrasal-verbs")
        {
            if (number >= 0 && number < _phrasalOptionButtons.Count && _phrasalOptionButtons[number].IsEnabled)
            {
                e.Handled = true;
                AnswerPhrasalOption(number);
            }
            else if (e.Key == VirtualKey.H && _phrasalHintButton?.IsEnabled == true)
            {
                e.Handled = true;
                ShowPhrasalHint();
            }
            else if (e.Key == VirtualKey.Enter && _phrasalNextButton?.IsEnabled == true)
            {
                e.Handled = true;
                AdvancePhrasalChallenge();
            }
        }
    }

    private async Task<bool> ConfirmAsync(string title, string message, string? confirmText = null)
    {
        if (_dialogOpen) return false;
        var focus = FocusManager.GetFocusedElement(XamlRoot) as Control;
        var page = _currentPage;
        var context = StudyContext;
        _dialogOpen = true;
        var closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dialogClosed = closed;
        var timer = _gameTimer;
        var wasRunning = timer?.IsEnabled == true;
        timer?.Stop();
        var game = _activeGame;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot, RequestedTheme = RequestedTheme,
                Title = title,
                Content = PagedTextContent(message, "dialog.Confirmation"),
                PrimaryButtonText = confirmText ?? U("Dialog.Confirm", "Continue", "Devam"),
                CloseButtonText = U("Dialog.Stay", "Stay / Cancel", "Kal / İptal"),
                DefaultButton = ContentDialogButton.Close,
            };
            AutomationProperties.SetAutomationId(dialog, "shell.Confirmation");
            ConfigureReadingDialog(dialog);
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        catch (Exception ex) { ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error); return false; }
        finally
        {
            _dialogOpen = false;
            _dialogClosed = null;
            if (wasRunning && !_storageBlocked && _activeGame == game && ReferenceEquals(timer, _gameTimer)) timer?.Start();
            RestoreDialogFocus(focus, page, context);
            closed.TrySetResult(true);
        }
    }

    private async Task<bool> ConfirmLeaveGameAsync()
    {
        if (_dialogOpen || _studyBusy) return false;
        if (_activeGame is null) return true;
        if (!await ConfirmAsync(U("Game.Leave", "Leave this game?", "Oyundan çıkılsın mı?"),
            U("Game.LeaveHint", "The current game cannot be resumed. Recorded learning progress is kept. Stay to continue playing.", "Geçerli oyun sürdürülemez. Kaydedilen öğrenme ilerlemesi korunur. Oynamaya devam etmek için Kal'ı seçin."))) return false;
        StopGameTimer();
        _activeGame = null;
        return true;
    }

    private void RestoreNavigationSelection()
    {
        _syncingSettings = true;
        Navigation.SelectedItem = Navigation.MenuItems.Concat(Navigation.FooterMenuItems)
            .OfType<NavigationViewItem>().FirstOrDefault(item => item.Tag as string == _currentPage);
        _syncingSettings = false;
    }

    private async Task ChangeQuickSettingAsync(string kind, string value)
    {
        if (_syncingSettings) return;
        if (_navigationBusy || _studyBusy || _dialogOpen) { SyncQuickSettingsBar(); return; }
        _navigationBusy = true;
        try
        {
            if (!await ConfirmLeaveGameAsync()) { SyncQuickSettingsBar(); return; }
            var previous = JsonSerializer.Serialize(_settings);
            var oldLanguage = _settings.StudyLanguage;
            var oldLevel = _settings.Level;
            try
            {
                if (kind == "ui") _settings.UiLanguage = value;
                else if (kind == "study")
                {
                    LearningEngine.ChangeStudySelection(_settings, value);
                }
                else LearningEngine.ChangeStudySelection(_settings, _settings.StudyLanguage, value);
                await _storage.SaveSettingsAsync(_settings);
            }
            catch (ImportReloadRequiredException) { throw; }
            catch { _settings = JsonSerializer.Deserialize<UserSettings>(previous)!; throw; }
            _cardUndo = null;
            _revealedCardKey = null;
            if (oldLanguage != _settings.StudyLanguage || oldLevel != _settings.Level) await ReloadWordsAsync();
            if (IsPhrasalPage) await EnsurePhrasalWordsAsync();
            ApplyNavigationLanguage();
            RenderCurrentPage();
        }
        catch (ImportReloadRequiredException ex) { BlockStorageUntilRestart(ex); }
        catch (Exception ex) { ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error); SyncQuickSettingsBar(); }
        finally { _navigationBusy = false; }
    }
}