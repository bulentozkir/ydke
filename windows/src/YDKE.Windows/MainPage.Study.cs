using System.Globalization;
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
    private readonly List<Button> _quizChoiceButtons = [];
    private Button? _quizContinue;
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
        var session = Session(mode);
        var id = session is null ? $"{mode}.Start" : session.Index >= session.WordKeys.Count
            ? LearningEngine.MissedKeys(session).Count > 0 ? $"{mode}.RetryMissed" : $"{mode}.Start"
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
        QuickUiLanguage.IsEnabled = !busy && !LoadingRing.IsActive;
        QuickStudyLanguage.IsEnabled = !busy && !LoadingRing.IsActive;
        QuickLevel.IsEnabled = !busy && !LoadingRing.IsActive;
        LocalStatusButton.IsEnabled = !CloudUiUnavailable;
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

    private IEnumerable<VocabularyEntry> DueAndNew() => LearningEngine.DueAndNew(_words, _progress, Today);

    private async Task StartStudySessionAsync(string mode, IEnumerable<VocabularyEntry>? subset = null, bool isRetry = false)
    {
        if (_studyBusy || _navigationBusy || _dialogOpen) return;
        if (mode == "quiz" && _words.Select(word => word.Word).Distinct().Take(2).Count() < 2)
        {
            ShowNotice(T("Common.Error"), T("Words.Empty"), InfoBarSeverity.Warning);
            return;
        }
        var existing = Session(mode);
        if (existing is not null && existing.Index < existing.WordKeys.Count &&
            !await ConfirmAsync(U("Session.Replace", "Replace the unfinished session?", "Bitmemiş oturum değiştirilsin mi?"),
                U("Session.ReplaceHint", "Recorded answers and reviews stay in your history; the session queue will be replaced.", "Kaydedilen yanıtlar ve tekrarlar geçmişte kalır; oturum sırası değiştirilir."))) return;
        var entries = (subset ?? DueAndNew()).Where(word => word.LanguageCode == _settings.StudyLanguage && word.Level == _settings.Level)
            .DistinctBy(word => word.Key).Take(mode == "cards" ? 20 : 8).ToArray();
        if (await MutateStudyAsync(() =>
        {
            _progress.Sessions[SessionKey(mode)] = LearningEngine.CreateSession(entries.Select(word => word.Key), isRetry);
            if (mode == "quiz")
            {
                // Companion sessions use the existing validated model: WordKeys stores the
                // exact option order and Answers stores the selected option. No random order
                // is reconstructed on resume, nor any selected wrong answer guessed.
                foreach (var key in _progress.Sessions.Keys.Where(key => key.StartsWith(SessionKey("quiz-options-") , StringComparison.Ordinal)).ToArray())
                    _progress.Sessions.Remove(key);
                for (var index = 0; index < entries.Length; index++)
                {
                    var answer = entries[index];
                    var choices = _words.Where(word => word.Key != answer.Key && word.Word != answer.Word)
                        .DistinctBy(word => word.Word).OrderBy(_ => _random.Next()).Take(3)
                        .Append(answer).OrderBy(_ => _random.Next()).Select(word => word.Key).ToList();
                    _progress.Sessions[SessionKey($"quiz-options-{index}")] = new StudySessionState { WordKeys = choices };
                }
            }
            else if (entries.Length > 0) _progress.CardPositions[StudyContext] = entries[0].Key;
        }))
        {
            _revealedCardKey = null;
            _cardRevealed = false;
            RequestSessionFocus(mode);
            if (_currentPage != mode) NavigateTo(mode, mode == "cards" ? CardsItem : QuizItem);
            else RenderCurrentPage();
        }
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

    private static Border StudyLabel(string text)
    {
        var heading = StudyText(text, 17, AppearancePalette.Current.ButtonTintForegroundBrush, emphasis: true);
        AutomationProperties.SetHeadingLevel(heading, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level3);
        AutomationProperties.SetName(heading, text);
        return new Border
        {
            Child = heading,
            Padding = new Thickness(9, 4, 9, 4),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(AppearancePalette.Current.ButtonTintBrush.Color) { Opacity = 0.72 },
            BorderBrush = AppearancePalette.Current.ButtonBorderBrush,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
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
        var surface = StudySurface(content, 14);
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
        button.Flyout = flyout;
        AutomationProperties.SetHelpText(button, T("Shell.ShowDetails"));
        FocusTarget(button, id);
        Fit();
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
        button.Padding = new Thickness(10, 8, 10, 8);
        button.CornerRadius = new CornerRadius(12);
        button.Background = palette.BoxBrush;
        button.Foreground = palette.BoxForegroundBrush;
        button.BorderBrush = palette.BorderBrush;
        button.BorderThickness = new Thickness(1);
        foreach (var state in new[] { "PointerOver", "Pressed", "Disabled" })
        {
            button.Resources[$"ButtonBackground{state}"] = palette.BoxBrush;
            button.Resources[$"ButtonForeground{state}"] = palette.BoxForegroundBrush;
            button.Resources[$"ButtonBorderBrush{state}"] = state == "Disabled" ? palette.BorderBrush : palette.BoxForegroundBrush;
        }
        return button;
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
            button.Content = StudyText(label, 18);
            AutomationProperties.SetName(button, label);
            AutomationProperties.SetHelpText(button, hint);
            ToolTipService.SetToolTip(button, hint);
        }
        button = CompactStudyAction(known ? KnownButton(entry, Refresh) : FavoriteButton(entry, Refresh));
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
        var content = new Grid { ColumnSpacing = 12 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition());
        var badge = new Border
        {
            Child = StudyText(number.ToString(CultureInfo.CurrentCulture), 18, background, emphasis: true),
            Background = foreground,
            Padding = new Thickness(7, 3, 7, 3),
            CornerRadius = new CornerRadius(6),
            VerticalAlignment = VerticalAlignment.Top,
        };
        AutomationProperties.SetAccessibilityView(badge, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        content.Children.Add(badge);
        var copy = new StackPanel { Spacing = 4 };
        copy.Children.Add(label);
        if (!string.IsNullOrWhiteSpace(description)) copy.Children.Add(StudyText(description, 18, foreground));
        Grid.SetColumn(copy, 1);
        content.Children.Add(copy);
        return content;
    }

    private static Button StudyOptionButton(UIElement content, string name)
    {
        var button = CompactStudyAction(SecondaryButton(name, ""));
        button.Content = content;
        button.Padding = new Thickness(12);
        button.CornerRadius = new CornerRadius(14);
        button.MinHeight = 56;
        AutomationProperties.SetName(button, name);
        button.IsTabStop = true;
        button.UseSystemFocusVisuals = true;
        return button;
    }

    // Keep four ratings in balanced 4/2/1 columns, never a stranded 3+1 row.
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

    private Button SessionStartButton(string mode, bool restart = false, bool secondary = false)
    {
        var label = restart ? U("Kids.Session.New", "Practice more", "Biraz daha çalış")
            : U("Kids.Session.Start", "Let's start", "Hadi başlayalım");
        var start = secondary ? CompactStudyAction(SecondaryButton(label, "")) : ReadableStudyAction(AccentButton(label, ""));
        AutomationProperties.SetHelpText(start, mode == "cards"
            ? U("Kids.Cards.StartHint", "Look at a word. Think, then show its meaning.", "Kelimeye bak. Düşün, sonra anlamını aç.")
            : U("Kids.Quiz.StartHint", "Read the meaning. Choose a word. Continue when you're ready.", "Anlamı oku. Bir kelime seç. Hazır olunca devam et."));
        start.HorizontalAlignment = HorizontalAlignment.Stretch;
        start.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        FocusTarget(start, $"{mode}.Start");
        start.Click += async (_, _) => await StartStudySessionAsync(mode);
        return start;
    }

    private void AddSessionStart(string mode)
    {
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(StudyText(mode == "cards"
            ? U("Kids.Cards.StartTitle", "Think of the meaning", "Anlamını düşün")
            : U("Kids.Quiz.StartTitle", "Find the word", "Kelimeyi bul"), 24, emphasis: true));
        content.Children.Add(StudyText(mode == "cards"
            ? U("Kids.Cards.StartHint", "Look at a word. Think, then show its meaning.", "Kelimeye bak. Düşün, sonra anlamını aç.")
            : U("Kids.Quiz.StartHint", "Read the meaning. Choose a word. Continue when you're ready.", "Anlamı oku. Bir kelime seç. Hazır olunca devam et."), 18));
        var start = SessionStartButton(mode);
        start.HorizontalAlignment = HorizontalAlignment.Left;
        content.Children.Add(start);
        PageContent.Children.Add(StudySurface(content));
    }

    private void AddSessionProgress(StudySessionState session, string mode)
    {
        var content = new StackPanel { Spacing = 4 };
        var label = mode == "cards" ? U("Kids.Cards.Count", "Cards done", "Bitirdiğin kartlar")
            : U("Kids.Quiz.Count", "Questions done", "Bitirdiğin sorular");
        content.Children.Add(StudyText($"{label}: {session.Answers.Count} / {session.WordKeys.Count}", 18, AppearancePalette.Current.BackgroundForegroundBrush));
        var progress = new ProgressBar { Minimum = 0, Maximum = Math.Max(1, session.WordKeys.Count), Value = session.Answers.Count };
        AutomationProperties.SetName(progress, label);
        content.Children.Add(progress);
        if (session.IsRetry) content.Children.Add(StudyText(U("Kids.Session.RetryHint", "Let's try these words again.", "Bu kelimeleri bir daha deneyelim."), 18, AppearancePalette.Current.BackgroundForegroundBrush));
        PageContent.Children.Add(content);
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

    private void RenderCards()
    {
        StopSpeechPlayback();
        PageContent.Children.Clear();
        AddPageHeader(T("Cards.Title"), U("Kids.Cards.Subtitle", "Think, look, then choose.", "Düşün, bak, sonra seç."));
        var session = Session("cards");
        if (session is null) { AddSessionStart("cards"); return; }
        AddSessionProgress(session, "cards");
        if (session.WordKeys.Any(key => !_words.Any(word => word.Key == key))) { AddMissingSessionNotice("cards"); return; }
        if (session.Index >= session.WordKeys.Count)
        {
            AddMissedSessionSummary("cards", session);
            return;
        }
        var entry = _words.First(word => word.Key == session.WordKeys[session.Index]);
        if (_revealedCardKey != entry.Key) { _cardRevealed = false; _revealedCardKey = entry.Key; }
        var rated = session.Answers.ContainsKey(entry.Key);
        var palette = AppearancePalette.Current;
        var content = new StackPanel { Spacing = 12 };
        var context = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        context.Children.Add(StudyText(string.Format(CultureInfo.CurrentCulture,
            U("Cards.Position", "Card {0} of {1}", "Kart {0} / {1}"), session.Index + 1, session.WordKeys.Count), 18));
        var step = StudyText(rated ? U("Kids.Cards.StepRated", "Ready for the next card?", "Sonraki karta hazır mısın?")
            : _cardRevealed ? U("Kids.Cards.StepRate", "Now choose how it felt.", "Şimdi sana nasıl geldiğini seç.")
            : U("Kids.Cards.StepRecall", "Think. What does it mean?", "Düşün. Anlamı ne?"), 18, emphasis: true);
        StudyLive(step, "cards.Step");
        context.Children.Add(step);
        ConfigureResponsiveGrid(context, 2, Math.Max(230, Font(230)));
        content.Children.Add(context);
        var word = new StackPanel { Spacing = 4 };
        word.Children.Add(StudyText(entry.Word, 40, emphasis: true, selectable: true));
        var listen = CompactStudyAction(SecondaryButton(T("Cards.Listen"), ""));
        FocusTarget(listen, "cards.Listen");
        listen.Click += async (_, _) => await PlayWordAsync(entry.Word, listen);
        content.Children.Add(StudyWordRow(word, listen));
        if (_cardRevealed || rated)
        {
            content.Children.Add(StudyDetail(T("Cards.Meaning"), LocalizedPart(entry.Definition), 24));
            if (!string.IsNullOrWhiteSpace(entry.Example)) content.Children.Add(StudyDetail(T("Cards.Example"), entry.Example, 20));
        }
        var reveal = ReadableStudyAction(AccentButton(U("Kids.Cards.Reveal", "Show meaning", "Anlamı göster"), ""));
        AutomationProperties.SetHelpText(reveal, U("Kids.Cards.ClickHelp", "Click Show meaning. Then choose how it felt.", "Anlamı göster'e tıkla. Sonra sana nasıl geldiğini seç."));
        reveal.IsEnabled = !_cardRevealed && !rated;
        reveal.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        FocusTarget(reveal, "cards.Reveal");
        reveal.Click += (_, _) => RevealCard();
        if (!_cardRevealed && !rated) content.Children.Add(reveal);
        PageContent.Children.Add(StudySurface(content));
        var ratings = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        string[] labels = [U("Kids.Rating.Again", "Help me", "Yardım et"), U("Kids.Rating.Hard", "Hard", "Zor"), U("Kids.Rating.Good", "I knew it", "Biliyordum"), U("Kids.Rating.Easy", "Easy!", "Kolay!")];
        string[] descriptions =
        [
            U("Kids.Rating.AgainHelp", "Try this one again", "Bunu tekrar dene"),
            U("Kids.Rating.HardHelp", "I needed time", "Biraz düşündüm"),
            U("Kids.Rating.GoodHelp", "I remembered", "Hatırladım"),
            U("Kids.Rating.EasyHelp", "I knew it quickly", "Hemen bildim"),
        ];
        for (var index = 0; index < labels.Length; index++)
        {
            var rating = (RecallRating)index;
            var button = StudyOptionButton(StudyOptionContent(index + 1, StudyText(labels[index], 18, emphasis: true),
                descriptions[index], palette.BoxBrush, palette.BoxForegroundBrush), $"{index + 1}: {labels[index]}");
            button.IsEnabled = _cardRevealed && !rated;
            AutomationProperties.SetHelpText(button, descriptions[index]);
            AutomationProperties.SetAcceleratorKey(button, (index + 1).ToString(CultureInfo.InvariantCulture));
            FocusTarget(button, $"cards.Rating.{rating}");
            button.Click += async (_, _) => await RateCardAsync(rating);
            ratings.Children.Add(button);
        }
        ConfigureStudyChoices(ratings, 4, 190);
        // Keep the first step focused; rated cards still expose their disabled ratings.
        if (_cardRevealed || rated) PageContent.Children.Add(ratings);
        var navigation = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        var previous = CompactStudyAction(SecondaryButton(T("Cards.Previous"), ""));
        previous.IsEnabled = session.Index > 0;
        FocusTarget(previous, "cards.Previous");
        previous.Click += async (_, _) => await MoveCardAsync(-1);
        navigation.Children.Add(previous);
        navigation.Children.Add(BuildUndoButton());
        var next = rated ? ReadableStudyAction(AccentButton(T("Cards.Next"), "")) : CompactStudyAction(SecondaryButton(T("Cards.Next"), ""));
        next.HorizontalAlignment = HorizontalAlignment.Stretch;
        next.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        next.IsEnabled = rated;
        FocusTarget(next, "cards.Next");
        next.Click += async (_, _) => await MoveCardAsync(1);
        navigation.Children.Add(next);
        var tools = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        tools.Children.Add(StudyMarkButton(entry, known: false));
        tools.Children.Add(StudyMarkButton(entry, known: true));
        ConfigureResponsiveGrid(tools, 2, Math.Max(180, Font(180)));
        var list = new StackPanel { Spacing = 12 };
        list.Children.Add(tools);
        if (!string.IsNullOrWhiteSpace(entry.PartOfSpeech)) list.Children.Add(StudyText(entry.PartOfSpeech, 18));
        AddCardsHintButton(list, navigation);
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
        if (missed.Length > 0)
        {
            var retry = ReadableStudyAction(AccentButton(U("Kids.Session.Retry", "Try these words again", "Bu kelimeleri tekrar dene"), ""));
            retry.HorizontalAlignment = HorizontalAlignment.Stretch;
            retry.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            FocusTarget(retry, $"{mode}.RetryMissed");
            retry.Click += async (_, _) => await StartStudySessionAsync(mode, missed, isRetry: true);
            actions.Children.Add(retry);
        }
        actions.Children.Add(SessionStartButton(mode, restart: true, secondary: missed.Length > 0));
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
        RequestUiFocus("cards.Rating.Again");
        RenderCards();
    }

    private async Task RateCardAsync(RecallRating rating)
    {
        var session = Session("cards");
        if (!_cardRevealed || session is null || session.Index >= session.WordKeys.Count) return;
        var key = session.WordKeys[session.Index];
        if (session.Answers.ContainsKey(key)) return;
        var before = JsonSerializer.Serialize(_progress);
        if (await MutateStudyAsync(() =>
        {
            LearningEngine.RecordCardReview(_progress, key, rating, Today);
            session.Answers[key] = rating != RecallRating.Again;
            // Advance to the first unanswered item, not past a skipped item.
            session.Index = LearningEngine.NextUnansweredIndex(session);
            if (session.Index < session.WordKeys.Count) _progress.CardPositions[StudyContext] = session.WordKeys[session.Index];
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
        }, keepUndo: true);
        _cardRevealed = false;
        RequestSessionFocus("cards");
        RenderCards();
    }

    private Button BuildUndoButton()
    {
        var undo = CompactStudyAction(SecondaryButton(U("Kids.Cards.Undo", "Undo my choice", "Seçimimi geri al"), ""));
        undo.IsEnabled = _cardUndo is not null && _cardUndoContext == StudyContext;
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

    private void AddCardsHintButton(UIElement wordList, Grid actions)
    {
        var help = new StackPanel { Spacing = 12 };
        help.Children.Add(StudyText(U("Kids.Cards.ClickHelp", "Click Show meaning. Then choose how it felt.", "Anlamı göster'e tıkla. Sonra sana nasıl geldiğini seç."), 18));
        help.Children.Add(StudyText(U("Kids.Cards.Shortcuts", "Space: show meaning · 1–4: choose · ←/→: back/next · U: mark as known", "Boşluk: anlamı göster · 1–4: seç · ←/→: geri/ileri · U: biliyorum işareti"), 18));
        help.Children.Add(StudyText(U("Kids.Cards.UndoHelp", "Chose by mistake? Undo brings back your last card. Use it before another learning action or leaving this page.", "Yanlışlıkla mı seçtin? Geri al, son kartını geri getirir. Başka bir öğrenme işlemi yapmadan veya bu sayfadan ayrılmadan kullan."), 18));
        help.Children.Add(StudyText(U("Kids.Cards.AgainHelp", "Help me saves this word for tomorrow. You can also try it again after these cards.", "Yardım et, bu kelimeyi yarın için saklar. Bu kartlar bitince bir daha da deneyebilirsin."), 18));
        actions.Children.Add(StudyPopupButton(U("Kids.Cards.WordList", "My word list", "Kelime listem"), wordList, "cards.WordList"));
        actions.Children.Add(StudyPopupButton(U("Kids.Study.Help", "How to play", "Nasıl oynarım?"), help, "cards.Help"));
    }


    private void RenderQuiz()
    {
        StopSpeechPlayback();
        PageContent.Children.Clear();
        _quizChoiceButtons.Clear();
        _quizContinue = null;
        AddPageHeader(T("Quiz.Title"), U("Kids.Quiz.Subtitle", "Read, choose, then keep going.", "Oku, seç, sonra devam et."));
        var session = Session("quiz");
        if (session is null) { AddSessionStart("quiz"); return; }
        AddSessionProgress(session, "quiz");
        if (session.WordKeys.Any(key => !_words.Any(word => word.Key == key))) { AddMissingSessionNotice("quiz"); return; }
        if (session.Index >= session.WordKeys.Count)
        {
            AddMissedSessionSummary("quiz", session);
            return;
        }
        var answer = _words.First(word => word.Key == session.WordKeys[session.Index]);
        var options = Session($"quiz-options-{session.Index}");
        if (options is null || options.WordKeys.Count < 2 || !options.WordKeys.Contains(answer.Key) ||
            options.WordKeys.Any(key => !_words.Any(word => word.Key == key))) { AddMissingSessionNotice("quiz"); return; }
        var answered = session.Answers.TryGetValue(answer.Key, out var correctAnswer);
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
        for (var index = 0; index < options.WordKeys.Count; index++)
        {
            var choice = _words.First(word => word.Key == options.WordKeys[index]);
            var isCorrectChoice = answered && choice.Key == answer.Key;
            var isWrongSelection = answered && choice.Key == selected && !isCorrectChoice;
            if (answered && !isCorrectChoice && !isWrongSelection) continue;
            var status = isCorrectChoice ? "✓ " + U("Kids.Quiz.CorrectChoice", "This word fits!", "Bu kelime uyuyor!")
                : isWrongSelection ? "✕ " + U("Kids.Quiz.YourChoice", "Your choice", "Senin seçimin") : null;
            var brushes = isCorrectChoice || isWrongSelection ? StudyResultBrushes(isCorrectChoice)
                : (Background: (Brush)palette.BoxBrush, Foreground: (Brush)palette.BoxForegroundBrush);
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
                var button = StudyOptionButton(content, $"{index + 1}: {choice.Word}");
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
        help.Children.Add(StudyText(U("Kids.Quiz.ClickHelp", "Click the word that fits. Read the answer, then click Continue.", "Uyan kelimeye tıkla. Yanıtı oku, sonra Devam'a tıkla."), 18));
        help.Children.Add(StudyText(U("Kids.Quiz.Shortcuts", "1–4: choose a word · Enter: continue after your answer", "1–4: kelime seç · Enter: yanıtından sonra devam et"), 18));
        if (!answered) PageContent.Children.Add(StudyPopupButton(U("Kids.Study.Help", "How to play", "Nasıl oynarım?"), help, "quiz.Help"));
    }

    private static (Brush Background, Brush Foreground) StudyResultBrushes(bool correct)
    {
        var dark = AppearancePalette.Current.Theme == ElementTheme.Dark;
        return correct
            ? (new SolidColorBrush(dark ? Color.FromArgb(255, 20, 54, 40) : Color.FromArgb(255, 236, 253, 245)),
               new SolidColorBrush(dark ? Color.FromArgb(255, 187, 247, 208) : Color.FromArgb(255, 6, 78, 59)))
            : (new SolidColorBrush(dark ? Color.FromArgb(255, 63, 45, 13) : Color.FromArgb(255, 255, 251, 235)),
               new SolidColorBrush(dark ? Color.FromArgb(255, 254, 243, 199) : Color.FromArgb(255, 113, 63, 18)));
    }

    private void AddQuizFeedback(VocabularyEntry answer, VocabularyEntry? selectedWord, bool correctAnswer, string message, Button continueButton)
    {
        var brushes = StudyResultBrushes(correctAnswer);
        var feedback = new StackPanel { Spacing = 12 };
        var status = StudyText(message, 20, brushes.Foreground, emphasis: true);
        StudyLive(status, "quiz.Feedback");
        feedback.Children.Add(status);
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
            var lookAtAnswer = StudyPopupButton(U("Kids.Quiz.AnswerDetails", "Look at the answer", "Yanıta bakalım"), details, "quiz.AnswerDetails");
            lookAtAnswer.HorizontalAlignment = HorizontalAlignment.Left;
            feedback.Children.Add(lookAtAnswer);
        }
        PageContent.Children.Add(StudySurface(feedback, 14, brushes.Background, brushes.Foreground));
    }

    private async Task AnswerQuizAsync(string selected)
    {
        var session = Session("quiz");
        if (session is null || session.Index >= session.WordKeys.Count) return;
        var key = session.WordKeys[session.Index];
        var options = Session($"quiz-options-{session.Index}");
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
        var session = Session("quiz");
        if (session is null || session.Index >= session.WordKeys.Count || !session.Answers.ContainsKey(session.WordKeys[session.Index])) return;
        await MutateStudyAsync(() => session.Index++);
        RequestSessionFocus("quiz");
        RenderQuiz();
    }

    private async void OnStudyKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_studyBusy || _navigationBusy || _dialogOpen || _activeGame is not null || e.KeyStatus.WasKeyDown) return;
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
            else if (number is >= 0 and <= 3 && _cardRevealed) { e.Handled = true; await RateCardAsync((RecallRating)number); }
        }
        else if (_currentPage == "quiz")
        {
            if (number >= 0 && number < _quizChoiceButtons.Count && _quizChoiceButtons[number].IsEnabled)
            {
                e.Handled = true;
                var session = Session("quiz");
                var options = session is null ? null : Session($"quiz-options-{session.Index}");
                if (options is not null) await AnswerQuizAsync(options.WordKeys[number]);
            }
            else if (e.Key == VirtualKey.Enter && _quizContinue?.IsEnabled == true) { e.Handled = true; await ContinueQuizAsync(); }
        }
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        if (_dialogOpen) return false;
        var focus = FocusManager.GetFocusedElement(XamlRoot) as Control;
        var page = _currentPage;
        var context = StudyContext;
        _dialogOpen = true;
        var closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dialogClosed = closed;
        SyncAccountStatus();
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
                PrimaryButtonText = U("Dialog.Confirm", "Continue", "Devam"),
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
            SyncAccountStatus();
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
        SyncAccountStatus();
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
            ApplyNavigationLanguage();
            RenderCurrentPage();
        }
        catch (ImportReloadRequiredException ex) { BlockStorageUntilRestart(ex); }
        catch (Exception ex) { ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error); SyncQuickSettingsBar(); }
        finally { _navigationBusy = false; SyncAccountStatus(); }
    }
}