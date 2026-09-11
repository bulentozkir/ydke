using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using Windows.UI;

namespace YDKE_Windows;

public sealed partial class MainPage : Page
{
    private readonly AppStorage _storage = new(AppDataPaths.CurrentFolder);
    private readonly CloudSyncCoordinator _cloud = new(credentialStore: new CloudCredentialStore(AppDataPaths.CurrentFolder));
    private readonly VocabularyRepository _repository = new();
    private readonly Random _random = new();

    private UserSettings _settings = new();
    private ProgressState _progress = new();
    private IReadOnlyList<VocabularyEntry> _words = [];
    private string _currentPage = "home";
    private bool _initialized;
    private GameSession? _activeGame;
    private DispatcherTimer? _gameTimer;
    private TextBlock? _gameTimerText;
    private ProgressBar? _gameTimerProgress;
    private ProgressBar? _gamePrimaryProgress;
    private TextBlock? _gameScoreText;
    private TextBlock? _gameStreakText;
    private TextBlock? _gameRoundText;
    private TextBlock? _gameLivesText;
    private CancellationTokenSource? _wordLoadCancellation;
    private CancellationTokenSource? _speechCancellation;
    private SpeechSynthesizer? _speechSynthesizer;
    private MediaPlayer? _speechPlayer;
    private MediaSource? _speechSource;
    private SpeechSynthesisStream? _speechStream;
    private Button? _speechButton;

    public MainPage() => InitializeComponent();

    private string T(string key) => Localizer.Get(_settings.UiLanguage, key);

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow window)
        {
            window.WindowModeChanged -= OnWindowModeChanged;
            window.WindowModeChanged += OnWindowModeChanged;
        }
        UpdateWindowModeButton();
        if (_initialized || _storageBlocked) return;
        _initialized = true;
        SetStudyBusy(true);
        try
        {
            var loaded = await _storage.LoadStateAsync();
            (_settings, _progress) = loaded;
        }
        catch (Exception ex)
        {
            BlockStorageUntilRestart(ex);
            return; // No default/mixed study UI, no game command-line startup.
        }
        finally { SetStudyBusy(false); }
        try { await _cloud.LoadCredentialsAsync(); }
        catch (Exception ex) { ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Warning); }

        if (!Localizer.UiLanguages.Any(language => language.Code == _settings.UiLanguage))
            _settings.UiLanguage = "tr";
        if (!VocabularyRepository.Languages.Any(language => language.Code == _settings.StudyLanguage))
            _settings.StudyLanguage = "en";

        ApplyAppearance();
        _ = GameCatalog.All;
        ApplyNavigationLanguage();
        QuickUiLanguage.ItemsSource = Localizer.UiLanguages;
        QuickStudyLanguage.ItemsSource = VocabularyRepository.Languages;
        ConfigureResponsiveGrid(QuickSettingsGrid, 3, 115);
        await ReloadWordsAsync();
        Navigation.SelectedItem = HomeItem;
        RenderCurrentPage();
        if (_storage.RecoveryMessage is { } recovery)
        {
            ShowNotice(U("Storage.Recovery", "Local data recovery — review before saving", "Yerel veri kurtarma — kaydetmeden önce inceleyin"), recovery, InfoBarSeverity.Warning);
            _storage.ClearRecoveryMessage();
        }

        var pageArgument = Environment.GetCommandLineArgs()
            .FirstOrDefault(argument => argument.StartsWith("--page=", StringComparison.OrdinalIgnoreCase));
        if (pageArgument is not null)
        {
            var page = pageArgument[(pageArgument.IndexOf('=') + 1)..];
            var item = page switch
            {
                "cards" => CardsItem,
                "quiz" => QuizItem,
                "words" => WordsItem,
                "simple-games" => SimpleGamesItem,
                "complex-games" => ComplexGamesItem,
                "stats" => StatsItem,
                "profile" => ProfileItem,
                "help" => HelpItem,
                "about" => AboutItem,
                _ => HomeItem,
            };
            NavigateTo(page, item);
        }

        var gameArgument = Environment.GetCommandLineArgs()
            .FirstOrDefault(argument => argument.StartsWith("--game=", StringComparison.OrdinalIgnoreCase));
        if (gameArgument is not null)
        {
            var gameId = gameArgument[(gameArgument.IndexOf('=') + 1)..];
            var game = GameCatalog.All.FirstOrDefault(item =>
                string.Equals(item.Id, gameId, StringComparison.OrdinalIgnoreCase));
            if (game is not null)
            {
                _currentPage = game.Group == GameGroup.Simple ? "simple-games" : "complex-games";
                Navigation.SelectedItem = game.Group == GameGroup.Simple ? SimpleGamesItem : ComplexGamesItem;
                StartGame(game);
            }
        }
    }

    private void OnContentScrollSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 720;
        var horizontalPadding = compact ? 16 : 28;
        ContentHost.Padding = new Thickness(horizontalPadding, 12, horizontalPadding, 12);
        QuickSettingsBar.Padding = new Thickness(horizontalPadding, 8, horizontalPadding, 8);
        PageContent.Width = Math.Max(0, Math.Min(1080, e.NewSize.Width - (horizontalPadding * 2)));
        UpdatePageFitStatus();
    }

    private void OnPageContentSizeChanged(object sender, SizeChangedEventArgs e) => UpdatePageFitStatus();

    private void UpdatePageFitStatus()
    {
        var availableWidth = Math.Max(0, ContentScroll.ViewportWidth - ContentHost.Padding.Left - ContentHost.Padding.Right);
        var availableHeight = Math.Max(0, ContentScroll.ViewportHeight - ContentHost.Padding.Top - ContentHost.Padding.Bottom);
        var horizontal = PageContent.ActualWidth <= availableWidth + 0.5;
        var vertical = PageContent.ActualHeight <= availableHeight + 0.5;
        AutomationProperties.SetHelpText(ContentScroll, FormattableString.Invariant(
            $"Page content {PageContent.ActualWidth:0.##} × {PageContent.ActualHeight:0.##}; viewport {availableWidth:0.##} × {availableHeight:0.##}; horizontal fit={horizontal}; vertical fit={vertical}"));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow window)
            window.WindowModeChanged -= OnWindowModeChanged;
        _wordLoadCancellation?.Cancel();
        StopGameTimer();
        DisposeSpeech();
    }

    internal void SetWindowActive(bool isActive)
    {
        if (!isActive) StopSpeechPlayback();
    }

    private async void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_syncingSettings || args.SelectedItemContainer?.Tag is not string tag || tag == _currentPage) return;
        if (_navigationBusy || _studyBusy || _dialogOpen || LoadingRing.IsActive) { RestoreNavigationSelection(); return; }
        _navigationBusy = true;
        SyncAccountStatus();
        try
        {
            if (!await ConfirmLeaveGameAsync()) { RestoreNavigationSelection(); return; }
            if (Navigation.DisplayMode != NavigationViewDisplayMode.Expanded) Navigation.IsPaneOpen = false;
            _cardUndo = null;
            _revealedCardKey = null;
            _currentPage = tag;
            if (!_storageBlocked) Notice.IsOpen = false;
            await EnsureWordsAsync();
            RenderCurrentPage();
        }
        catch (Exception ex) { ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error); RestoreNavigationSelection(); }
        finally { _navigationBusy = false; SyncAccountStatus(); }
    }

    private void ApplyNavigationLanguage()
    {
        HomeItem.Content = T("Nav.Home");
        CardsItem.Content = T("Nav.Cards");
        QuizItem.Content = T("Nav.Quiz");
        WordsItem.Content = T("Nav.Words");
        SimpleGamesItem.Content = T("Nav.Simple");
        ComplexGamesItem.Content = T("Nav.Complex");
        StatsItem.Content = T("Nav.Stats");
        ProfileItem.Content = T("Nav.Profile");
        HelpItem.Content = T("Nav.Help");
        AboutItem.Content = T("Nav.About");
        AutomationProperties.SetName(Navigation, U("Shell.Navigation", "Main navigation", "Ana gezinme"));
        foreach (var item in new[] { HomeItem, CardsItem, QuizItem, WordsItem, SimpleGamesItem, ComplexGamesItem, StatsItem, ProfileItem, HelpItem, AboutItem })
            AutomationProperties.SetName(item, item.Content?.ToString() ?? "");
        UpdateWindowModeButton();
    }

    private void ApplyAppearance()
    {
        AppearancePalette.SetCurrent(_settings);
        RequestedTheme = AppearancePalette.Current.Theme;
        Background = AppearancePalette.Current.BackgroundBrush;
        Foreground = AppearancePalette.Current.BackgroundForegroundBrush;
        Navigation.Background = AppearancePalette.Current.BackgroundBrush;
        Navigation.Foreground = AppearancePalette.Current.BackgroundForegroundBrush;
        Navigation.FontSize = ReadingSize(18);
        ContentHost.Background = AppearancePalette.Current.BackgroundBrush;
        QuickSettingsBar.Background = AppearancePalette.Current.BoxBrush;
        QuickSettingsBar.BorderBrush = AppearancePalette.Current.BorderBrush;
        QuickSettingsBar.BorderThickness = new Thickness(0, 0, 0, 1);
        if (App.MainWindow is YDKE_Windows.MainWindow window)
            window.ApplyAppearance(AppearancePalette.Current);
    }

    private async Task EnsureWordsAsync()
    {
        if (_words.Count == 0) await ReloadWordsAsync();
    }

    private async Task ReloadWordsAsync()
    {
        _wordLoadCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _wordLoadCancellation = cancellation;
        SetLoading(true);
        try
        {
            var language = CurrentStudyLanguage();
            if (!language.Files.ContainsKey(_settings.Level)) _settings.Level = language.Levels[0];
            var words = await _repository.LoadAsync(
                _settings.StudyLanguage,
                _settings.Level,
                cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            _words = words;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error);
            _words = [];
        }
        finally
        {
            if (ReferenceEquals(_wordLoadCancellation, cancellation))
            {
                _wordLoadCancellation = null;
                SetLoading(false);
            }
            cancellation.Dispose();
        }
    }

    private void RenderCurrentPage()
    {
        StopGameTimer();
        StopSpeechPlayback();
        ContentScroll.ChangeView(0, 0, null, disableAnimation: _settings.ReduceMotion);
        PageContent.Children.Clear();
        switch (_currentPage)
        {
            case "cards": RenderCards(); break;
            case "quiz": RenderQuiz(); break;
            case "words": RenderWords(); break;
            case "simple-games": RenderGames(GameGroup.Simple); break;
            case "complex-games": RenderGames(GameGroup.Complex); break;
            case "stats": RenderStats(); break;
            case "profile": RenderProfile(); break;
            case "help": RenderHelpPage(); break;
            case "about": RenderAboutPage(); break;
            default: RenderHome(); break;
        }
        SyncQuickSettingsBar();
    }

    private void SyncQuickSettingsBar()
    {
        _syncingSettings = true;
        QuickUiLanguage.SelectedValue = _settings.UiLanguage;
        QuickStudyLanguage.SelectedValue = _settings.StudyLanguage;
        QuickLevel.ItemsSource = CurrentStudyLanguage().Levels;
        QuickLevel.SelectedItem = _settings.Level;
        QuickUiLanguage.Header = QuickSettingLabel(T("Profile.UiLanguage"));
        QuickStudyLanguage.Header = QuickSettingLabel(T("Profile.StudyLanguage"));
        QuickLevel.Header = QuickSettingLabel(T("Profile.Level"));
        foreach (var control in new[] { QuickUiLanguage, QuickStudyLanguage, QuickLevel })
        {
            ConfigureReadingComboBox(control);
        }
        AutomationProperties.SetName(QuickUiLanguage, T("Profile.UiLanguage"));
        AutomationProperties.SetName(QuickStudyLanguage, T("Profile.StudyLanguage"));
        AutomationProperties.SetName(QuickLevel, T("Profile.Level"));
        SyncAccountStatus();
        _syncingSettings = false;
    }

    private async void OnQuickUiLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QuickUiLanguage.SelectedValue is not string code || code == _settings.UiLanguage) return;
        await ChangeQuickSettingAsync("ui", code);
    }

    private async void OnQuickStudyLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QuickStudyLanguage.SelectedValue is not string code || code == _settings.StudyLanguage) return;
        await ChangeQuickSettingAsync("study", code);
    }

    private async void OnQuickLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QuickLevel.SelectedItem is not string selected || selected == _settings.Level) return;
        await ChangeQuickSettingAsync("level", selected);
    }

    private async Task PlayWordAsync(string text, Button source)
    {
        StopSpeechPlayback();
        var cancellation = new CancellationTokenSource();
        _speechCancellation = cancellation;
        source.IsEnabled = false;
        var playbackStarted = false;

        try
        {
            _speechSynthesizer ??= new SpeechSynthesizer();
            var language = SpeechLanguage(_settings.StudyLanguage);
            var voice = SpeechSynthesizer.AllVoices.FirstOrDefault(item =>
                string.Equals(item.Language, language, StringComparison.OrdinalIgnoreCase));
            if (voice is not null) _speechSynthesizer.Voice = voice;

            var stream = await _speechSynthesizer.SynthesizeTextToStreamAsync(text).AsTask(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();

            _speechStream = stream;
            _speechSource = MediaSource.CreateFromStream(stream, stream.ContentType);
            if (_speechPlayer is null)
            {
                _speechPlayer = new MediaPlayer();
                _speechPlayer.CommandManager.IsEnabled = false;
                _speechPlayer.MediaEnded += OnSpeechPlaybackEnded;
                _speechPlayer.MediaFailed += OnSpeechPlaybackFailed;
            }
            _speechPlayer.Source = _speechSource;
            _speechButton = source;
            _speechPlayer.Play();
            playbackStarted = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            if (ReferenceEquals(_speechCancellation, cancellation))
            {
                _speechCancellation = null;
                if (!playbackStarted) source.IsEnabled = true;
            }
            cancellation.Dispose();
        }
    }

    private void OnSpeechPlaybackEnded(MediaPlayer sender, object args) =>
        DispatcherQueue.TryEnqueue(ReleaseSpeechSource);

    private void OnSpeechPlaybackFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args) =>
        DispatcherQueue.TryEnqueue(ReleaseSpeechSource);

    private void StopSpeechPlayback()
    {
        _speechCancellation?.Cancel();
        ReleaseSpeechSource();
    }

    private void ReleaseSpeechSource()
    {
        if (_speechPlayer is not null)
        {
            _speechPlayer.Pause();
            _speechPlayer.Source = null;
        }
        _speechSource?.Dispose();
        _speechSource = null;
        _speechStream?.Dispose();
        _speechStream = null;
        if (_speechButton is not null)
        {
            _speechButton.IsEnabled = true;
            _speechButton = null;
        }
    }

    private void DisposeSpeech()
    {
        StopSpeechPlayback();
        if (_speechPlayer is not null)
        {
            _speechPlayer.MediaEnded -= OnSpeechPlaybackEnded;
            _speechPlayer.MediaFailed -= OnSpeechPlaybackFailed;
            _speechPlayer.Dispose();
            _speechPlayer = null;
        }
        _speechSynthesizer?.Dispose();
        _speechSynthesizer = null;
    }

    private static string SpeechLanguage(string languageCode) => languageCode switch
    {
        "de" => "de-DE",
        "fr" => "fr-FR",
        "it" => "it-IT",
        "es" => "es-ES",
        "pt" => "pt-PT",
        "nl" => "nl-NL",
        _ => "en-US",
    };

    private void RenderGames(GameGroup group)
    {
        StopGameTimer();
        StopSpeechPlayback();
        _activeGame = null;
        if (_gameCatalogGroup != group)
        {
            _gameCatalogGroup = group;
            _gameCatalogPage = 0;
        }
        PageContent.Children.Clear();
        AddPageHeader(T(group == GameGroup.Simple ? "Games.SimpleTitle" : "Games.ComplexTitle"),
            T(group == GameGroup.Simple ? "Games.SimpleSubtitle" : "Games.ComplexSubtitle"));
        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        AddGameFilters(group, grid);
        ConfigureResponsiveGrid(grid, 2, 300);
        PageContent.Children.Add(grid);
    }

    private Button GameButton(GameDefinition game)
    {
        var palette = PaletteFor(game);
        var layout = new Grid { ColumnSpacing = 16 };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(new Border
        {
            Width = 46,
            Height = 46,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(palette.Accent),
            Child = new FontIcon { Glyph = game.Glyph, FontSize = Font(23), Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) },
        });
        var title = Heading(game.Title, 18);
        Grid.SetColumn(title, 1);
        layout.Children.Add(title);
        var best = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        best.Children.Add(new TextBlock { Text = T("Game.Best"), FontSize = ReadingSize(18), TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Right });
        best.Children.Add(Heading(GameBest(game).ToString("N0"), 18));
        Grid.SetColumn(best, 2);
        layout.Children.Add(best);
        var description = Body(GameTask(game));
        description.Margin = new Thickness(0, 10, 0, 0);
        Grid.SetRow(description, 1);
        Grid.SetColumnSpan(description, 3);
        layout.Children.Add(description);
        var button = new Button
        {
            Content = layout,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(14),
            MinHeight = 96,
            CornerRadius = new CornerRadius(20),
            Background = AppearancePalette.Current.BoxBrush,
            BorderBrush = AppearancePalette.Current.BorderBrush,
            BorderThickness = new Thickness(1),
            Foreground = AppearancePalette.Current.BoxForegroundBrush,
        };
        ApplyReadableForeground(layout, AppearancePalette.Current.BoxForegroundBrush);
        AutomationProperties.SetName(button, game.Title);
        AutomationProperties.SetAutomationId(button, "games.Item." + game.Id);
        AutomationProperties.SetHelpText(button, GameTask(game));
        button.Click += (_, _) => RenderGameDetail(game);
        return button;
    }

    private void RenderGameDetail(GameDefinition game)
    {
        StopGameTimer();
        _activeGame = null;
        PageContent.Children.Clear();
        PageContent.Children.Add(GameToolbar(
            onBack: () => RenderGames(game.Group),
            onHint: () => ShowNotice(T("Game.Hint"), GameInstructions(game), InfoBarSeverity.Informational), game));
        var hero = GameHero(game, showBest: true);
        PageContent.Children.Add(hero);
        AnimateEntrance(hero);
        var details = new StackPanel { Spacing = 8 };
        details.Children.Add(Heading(GameTask(game), 24));
        details.Children.Add(Body(U("Kids.Games.StartHint", "Choose Play. You can ask for a hint at any time.", "Oyna'yı seç. İstediğin zaman ipucu isteyebilirsin.")));
        var start = AccentButton(U("Kids.Games.Play", "Play", "Oyna"), "");
        AutomationProperties.SetAutomationId(start, "game.Start");
        start.HorizontalAlignment = HorizontalAlignment.Left;
        start.Click += (_, _) => StartGame(game);
        details.Children.Add(start);
        PageContent.Children.Add(Card(details, 22));
        PageContent.Children.Add(StudyPopupButton(U("Kids.Games.MoreHelp", "Show me how", "Nasıl oynandığını göster"), Body(GameChildInstructions(game)), "game.HelpDetails"));
    }

    private Grid GameToolbar(Action onBack, Action onHint, GameDefinition game)
    {
        var toolbar = new Grid { ColumnSpacing = 16, RowSpacing = 8 };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition());
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(Heading(game.Title, 23));
        toolbar.Children.Add(title);
        var actions = new Grid { ColumnSpacing = 8, RowSpacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var back = CompactStudyAction(SecondaryButton(T("Games.Back"), ""));
        AutomationProperties.SetAutomationId(back, "game.Back");
        back.Click += async (_, _) => { if (await ConfirmLeaveGameAsync()) onBack(); };
        actions.Children.Add(back);
        var hint = CompactStudyAction(SecondaryButton(T("Game.Hint"), "\uE82F"));
        AutomationProperties.SetAutomationId(hint, "game.Hint");
        hint.Click += (_, _) => onHint();
        ToolTipService.SetToolTip(hint, T("Game.Hint"));
        actions.Children.Add(hint);
        var rulesLabel = U("Games.HowToPlay", "How to play", "Nasıl oynanır");
        var rules = CompactStudyAction(SecondaryButton(rulesLabel, "\uE897"));
        AutomationProperties.SetAutomationId(rules, "game.Rules");
        rules.Click += async (_, _) => await ShowGameRulesAsync(game);
        actions.Children.Add(rules);
        ConfigureResponsiveGrid(actions, 3, Font(100));
        Grid.SetColumn(actions, 1);
        toolbar.Children.Add(actions);
        return toolbar;
    }

    private async Task ShowGameRulesAsync(GameDefinition game)
    {
        if (_studyBusy || _dialogOpen || _navigationBusy) return;
        var focus = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) as Control;
        var page = _currentPage;
        var context = StudyContext;
        var timer = _gameTimer;
        var wasRunning = timer?.IsEnabled == true;
        var session = _activeGame;
        var closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dialogOpen = true;
        _dialogClosed = closed;
        SyncAccountStatus();
        timer?.Stop();
        try
        {
            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(new TextBlock { Text = GameTask(game), FontSize = ReadingSize(20), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = GameChildInstructions(game), FontSize = ReadingSize(20), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = U("Kids.Games.HelpPause", "The clock is paused. Close this help when you're ready to play.", "Saat durdu. Oynamaya hazır olunca yardımı kapat."), FontSize = ReadingSize(18), TextWrapping = TextWrapping.Wrap });
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot, RequestedTheme = RequestedTheme,
                Title = game.Title + " · " + U("Games.HowToPlay", "How to play", "Nasıl oynanır"),
                Content = content,
                CloseButtonText = U("Games.CloseRules", "Close instructions", "Yönergeleri kapat"),
            };
            AutomationProperties.SetAutomationId(dialog, "game.RulesDialog");
            ConfigureReadingDialog(dialog);
            await dialog.ShowAsync();
        }
        catch (Exception ex) { ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error); }
        finally
        {
            _dialogOpen = false;
            _dialogClosed = null;
            if (wasRunning && !_storageBlocked && _activeGame == session && ReferenceEquals(timer, _gameTimer)) timer?.Start();
            SyncAccountStatus();
            RestoreDialogFocus(focus, page, context);
            closed.TrySetResult(true);
        }
    }

    private string GameTask(GameDefinition game) => game.Id switch
    {
        "hangman" => U("Games.Task.Hangman", "Choose letters to uncover the word.", "Kelimeyi bulmak için harfleri seçin."),
        "scramble" => U("Games.Task.Scramble", "Arrange the letters to match the meaning.", "Anlama uyan kelimeyi oluşturmak için harfleri sıralayın."),
        "dictation" => U("Games.Task.Dictation", "Listen, then type the word.", "Dinleyin, ardından kelimeyi yazın."),
        "listeningchoice" => U("Games.Task.Listening", "Listen, then choose the meaning.", "Dinleyin, ardından anlamını seçin."),
        "speedround" or "survival" => U("Games.Task.Choice", "Choose the word that matches the meaning.", "Anlama uyan kelimeyi seçin."),
        "truefalse" => U("Games.Task.TrueFalse", "Does this word match the definition?", "Bu kelime tanıma uyuyor mu?"),
        "wordclass" => U("Games.Task.Class", "Choose the word's part of speech.", "Kelimenin sözcük türünü seçin."),
        "memory" => U("Games.Task.Memory", "Match each word with its meaning.", "Her kelimeyi anlamıyla eşleştirin."),
        "oddoneout" => U("Games.Task.Odd", "Choose the word from a different category.", "Farklı kategorideki kelimeyi seçin."),
        "wordrace" => U("Games.Task.Typing", "Type the word that matches the meaning.", "Anlama uyan kelimeyi yazın."),
        "clozetest" => U("Games.Task.Cloze", "Complete the missing word.", "Eksik kelimeyi tamamlayın."),
        "sentencescramble" => U("Games.Task.Sentence", "Put all the tiles in sentence order.", "Tüm parçaları cümle sırasına dizin."),
        "readingcomprehension" => U("Games.Task.Reading", "Read the passage, then answer the question.", "Metni okuyun, ardından soruyu yanıtlayın."),
        "wordmorph" => U("Games.Task.Semantic", "Are these words synonyms or antonyms?", "Bu kelimeler eş anlamlı mı, zıt anlamlı mı?"),
        "bingo" => U("Games.Task.Bingo", "Find this definition on the board.", "Bu tanıma uyan kelimeyi tahtada bulun."),
        "bossrush" => U("Games.Task.Boss", "Choose the meaning of the displayed word.", "Gösterilen kelimenin anlamını seçin."),
        "codycross" => U("Games.Task.Cody", "Solve each clue to reveal the bonus code.", "Bonus kodu açmak için ipuçlarını çözün."),
        "crossword" => U("Games.Task.Crossword", "Solve Across first, then Down.", "Önce yatay, ardından dikey kelimeyi çözün."),
        "dailychallenge" or "wordguess" => U("Games.Task.Guess", "Guess the word using the letter clues.", "Harf ipuçlarını kullanarak kelimeyi tahmin edin."),
        "scrabble" => U("Games.Task.Rack", "Build a vocabulary word from these letters.", "Bu harflerden bir kelime oluşturun."),
        "categorysprint" => U("Games.Task.Category", "Name a new word in this category.", "Bu kategoriden yeni bir kelime yazın."),
        "cluedetective" => U("Games.Task.Clues", "Reveal a clue, then guess the hidden word.", "Bir ipucu açın, ardından gizli kelimeyi tahmin edin."),
        "matrix" => U("Games.Task.Matrix", "Select the hidden word in a straight line.", "Gizli kelimeyi düz bir çizgide seçin."),
        _ => GameDescription(game),
    };

    private string GameChildInstructions(GameDefinition game) => game.Id switch
    {
        "hangman" => U("Kids.How.Hangman", "Read the meaning. Pick a letter. Find the word before six misses.", "Anlamı oku. Bir harf seç. Altı hatadan önce kelimeyi bul."),
        "scramble" => U("Kids.How.Scramble", "Read the meaning. Pick the letters in order. Click a filled box to change it.", "Anlamı oku. Harfleri sırayla seç. Değiştirmek için dolu bir kutuya tıkla."),
        "dictation" or "listeningchoice" => U("Kids.How.Listen", "Click Listen. Listen again if you need to. Then write the word or choose its meaning.", "Dinle'ye tıkla. Gerekirse bir daha dinle. Sonra kelimeyi yaz veya anlamını seç."),
        "speedround" or "survival" => U("Kids.How.Choice", "Read the meaning. Click the matching word. Look at the answer, then choose Next.", "Anlamı oku. Uyan kelimeye tıkla. Yanıta bak, sonra Sonraki'ni seç."),
        "truefalse" => U("Kids.How.TrueFalse", "Read the word and meaning. Choose True if they match. Choose False if they do not.", "Kelimeyi ve anlamı oku. Uyuyorsa Doğru'yu, uymuyorsa Yanlış'ı seç."),
        "wordclass" => U("Kids.How.Class", "A noun names something. A verb tells an action. An adjective describes a thing. An adverb tells how.", "İsim bir şeyin adıdır. Fiil bir işi anlatır. Sıfat bir şeyi tanımlar. Zarf nasıl olduğunu anlatır."),
        "memory" => U("Kids.How.Memory", "Open two cards. Find a word and its meaning. Match all six pairs.", "İki kart aç. Bir kelimeyi anlamıyla eşleştir. Altı çifti de bul."),
        "oddoneout" => U("Kids.How.Odd", "Three words belong together. Click the word that does not belong.", "Üç kelime aynı grupta. Bu gruba uymayan kelimeye tıkla."),
        "wordrace" or "clozetest" => U("Kids.How.Type", "Read the clue. Write the missing word. Click Check answer when you're ready.", "İpucunu oku. Eksik kelimeyi yaz. Hazır olunca Yanıtı kontrol et'e tıkla."),
        "sentencescramble" => U("Kids.How.Sentence", "Click the word tiles to make a sentence. Undo takes the last tile back. Use every tile.", "Cümle kurmak için kelime taşlarına tıkla. Geri al son taşı geri getirir. Her taşı kullan."),
        "readingcomprehension" => U("Kids.How.Reading", "Read the story. Turn the pages with the arrows. Answer the question. You can read the story again.", "Öyküyü oku. Oklarla sayfaları çevir. Soruyu yanıtla. Öyküyü yeniden okuyabilirsin."),
        "wordmorph" => U("Kids.How.Semantic", "Synonyms mean the same or almost the same. Antonyms mean the opposite. Which pair do you see?", "Eş anlamlı kelimeler aynı veya benzer anlamlıdır. Zıt anlamlı kelimeler birbirinin tersidir. Hangi çifti görüyorsun?"),
        "bingo" => U("Kids.How.Bingo", "Read the meaning. Find the word on the board. Make a line across, down, or from corner to corner.", "Anlamı oku. Kelimeyi tahtada bul. Yatay, dikey veya köşeden köşeye bir çizgi yap."),
        "bossrush" => U("Kids.How.Boss", "Choose the meaning of each word. Each right answer takes away one heart from the character. Meet all three!", "Her kelimenin anlamını seç. Doğru yanıt karakterden bir kalp eksiltir. Üçüyle de tanış!"),
        "codycross" => U("Kids.How.Cody", "Read the clue and write the word. Each word opens a letter in the secret code. Try all five clues.", "İpucunu oku ve kelimeyi yaz. Her kelime gizli kodda bir harf açar. Beş ipucunu da dene."),
        "crossword" => U("Kids.How.Crossword", "First write the word that goes across. Then write the word that goes down. They share one box.", "Önce yatay kelimeyi yaz. Sonra dikey kelimeyi yaz. Bir kutuyu birlikte kullanırlar."),
        "wordguess" or "dailychallenge" => U("Kids.How.Guess", "Write a word. ✓ means the letter is in the right place. ~ means try another place. × means that letter is not in the word. You have six tries.", "Bir kelime yaz. ✓ harf doğru yerde demektir. ~ başka bir yeri dene demektir. × harf kelimede yok demektir. Altı deneme hakkın var."),
        "scrabble" => U("Kids.How.Rack", "Use the letters to make a word. Each tile can be used once. Write your word below.", "Harflerden bir kelime oluştur. Her taşı bir kez kullan. Kelimeni aşağıya yaz."),
        "categorysprint" => U("Kids.How.Category", "Look at the group name. Write a word from that group. Try a different word each time.", "Grubun adına bak. O gruptan bir kelime yaz. Her seferinde başka bir kelime dene."),
        "cluedetective" => U("Kids.How.Clues", "Ask for a clue. What word could it be? Write your guess. Each extra clue costs some points.", "İpucu iste. Bu hangi kelime olabilir? Tahminini yaz. Her yeni ipucu biraz puan azaltır."),
        "matrix" => U("Kids.How.Matrix", "Read the meaning. Click letters next to each other in a straight line. Click a picked letter to go back.", "Anlamı oku. Yan yana harfleri düz bir çizgide seç. Geri dönmek için seçtiğin bir harfe tıkla."),
        _ => GameTask(game),
    };

    private void StartGame(GameDefinition game)
    {
        if (_studyBusy || _dialogOpen || _completingGame is not null) return;
        if (_words.Count < 4) { ShowNotice(T("Common.Error"), T("Words.Empty"), InfoBarSeverity.Warning); return; }
        StopGameTimer();
        StopSpeechPlayback();
        _categoryFound.Clear();
        _categoryWords = [];
        _gameCompletion = new GameSaveGate();
        _gameMode = ScoreMode(game);
        _gameScoreKey = GameEngine.ScoreKey(_settings.StudyLanguage, _settings.Level, _gameMode, game.Id);
        _activeGame = new GameSession(game);
        if (game.Id == "survival") _activeGame.Lives = 1;
        if (_settings.UntimedPractice) _activeGame.SecondsRemaining = 0;
        var session = _activeGame;
        RenderGameRound(session);
        if (_activeGame == session && _gameMode == "timed") StartGameTimer(session);
    }

    private void RenderGameRound(GameSession session)
    {
        if (_activeGame != session) return;
        _gameEpoch = _gameTurn.Begin();
        _gameLocalBusy = false;
        _gameRoundPoints = 100;
        _gameRoundView = null;
        _gameFeedback = null;
        _gameHintProvider = null;
        PageContent.Children.Clear();
        PageContent.Children.Add(GameToolbar(
            onBack: () => RenderGameDetail(session.Game),
            onHint: () => ShowNotice(T("Game.Hint"), _gameHintProvider?.Invoke() ?? GameInstructions(session.Game), InfoBarSeverity.Informational), session.Game));
        var hud = GameHud(session);
        var progress = GameEngine.Progress(session, _gameMode);
        _gamePrimaryProgress = new ProgressBar
        {
            Minimum = 0,
            Maximum = progress.Maximum,
            Value = progress.Value,
            Visibility = progress.Visible ? Visibility.Visible : Visibility.Collapsed,
            Height = 5,
            CornerRadius = new CornerRadius(3),
        };
        AutomationProperties.SetName(_gamePrimaryProgress, _gameMode == "timed"
            ? U("Games.Progress.Elapsed", "Active seconds elapsed", "Geçen etkin saniye") : T("Game.Round"));
        PageContent.Children.Add(new StackPanel { Spacing = 4, Children = { hud, _gamePrimaryProgress } });

        if (_words.Count < 4) { PageContent.Children.Add(Body(T("Words.Empty"))); return; }

        // Route by stable catalog ID where multiple historical mechanics shared a renderer.
        switch (session.Game.Id)
        {
            case "dictation": case "listeningchoice": RenderAudioGame(session); return;
            case "sentencescramble": RenderSentenceGame(session); return;
            case "clozetest": RenderClozeGame(session); return;
            case "categorysprint": RenderCategoryGame(session); return;
            case "cluedetective": RenderClueGame(session); return;
            case "scrabble": RenderRackGame(session); return;
            case "crossword": RenderCrosswordGame(session); return;
            case "wordclass": RenderClassGame(session); return;
            case "oddoneout": RenderOddGame(session); return;
            case "bingo": RenderBingoGame(session); return;
            case "matrix": RenderMatrixGame(session); return;
            case "readingcomprehension": case "wordmorph": RenderLocalDataGame(session); return;
        }

        switch (session.Game.Mechanic)
        {
            case GameMechanic.Hangman:
                RenderHangmanRound(session);
                break;
            case GameMechanic.Memory:
                RenderMemoryRound(session);
                break;
            case GameMechanic.DailyGuess:
                RenderWordGuessRound(session);
                break;
            case GameMechanic.TrueFalse:
                RenderTrueFalseChallenge(session);
                break;
            case GameMechanic.LetterTiles:
                RenderScrambleRound(session);
                break;
            case GameMechanic.BossBattle:
                RenderBossRushRound(session);
                break;
            case GameMechanic.ClueWords:
                RenderCodyCrossRound(session);
                break;
            case GameMechanic.TimedTyping:
                RenderTypingChallenge(session);
                break;
            case GameMechanic.TimedChoice:
            case GameMechanic.Survival:
                RenderChoiceChallenge(session);
                break;
            default:
                GameUnavailable(session, EligibleRequirement);
                break;
        }
    }

    private void RenderTypingChallenge(GameSession session)
    {
        var entry = _words[_random.Next(_words.Count)];
        _gameHintProvider = () => WordHint(entry);
        var panel = GameSceneContent();
        AddGameQuestion(panel, LocalizedPart(entry.Definition));
        panel.Children.Add(GameCaption(entry.Category.ToUpperInvariant()));
        var input = GameInput(panel);
        SubmitGame(panel, session, async submit =>
        {
            var correct = GameAnswer(input.Text) == GameAnswer(entry.Word);
            await ResolveGameAnswerAsync(session, correct, entry.Word, submit, [entry.Key]);
        });
        AddGameScene(session.Game, panel);
        input.Focus(FocusState.Programmatic);
    }

    private void RenderTrueFalseChallenge(GameSession session)
    {
        var entry = _words[_random.Next(_words.Count)];
        var isTrue = _random.Next(2) == 0;
        var shown = isTrue ? entry : _words.FirstOrDefault(word => word.Key != entry.Key &&
            LocalizedPart(word.Definition) != LocalizedPart(entry.Definition) && GameAnswer(word.Word) != GameAnswer(entry.Word));
        if (shown is null) { GameUnavailable(session, EligibleRequirement); return; }
        // The word being defined (entry) is the real answer, never the possibly-wrong shown definition's word.
        _gameHintProvider = () => WordHint(entry);
        var panel = GameSceneContent();
        AddGameQuestion(panel, $"{entry.Word}\n{LocalizedPart(shown.Definition)}");
        var actions = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        foreach (var answer in new[] { true, false })
        {
            var button = GameChoiceButton(answer ? U("Games.True", "True", "Doğru") : U("Games.False", "False", "Yanlış"), session.Game);
            var answerIndex = answer ? 0 : 1;
            button.Tag = $"game-choice-{answerIndex}";
            SetGameChoiceMetadata(button, $"game.Choice.{answerIndex + 1}", (answerIndex + 1).ToString(CultureInfo.InvariantCulture));
            button.Click += async (_, _) =>
            {
                await ResolveGameAnswerAsync(session, answer == isTrue, entry.Word, button, [entry.Key]);
            };
            actions.Children.Add(button);
        }
        ConfigureResponsiveGrid(actions, 2, 220);
        panel.Children.Add(actions);
        AddGameScene(session.Game, panel);
    }

    private void RenderChoiceChallenge(GameSession session)
    {
        var entry = _words[_random.Next(_words.Count)];
        var choices = _words.Where(word => GameAnswer(word.Word) != GameAnswer(entry.Word) && LocalizedPart(word.Definition) != LocalizedPart(entry.Definition))
            .DistinctBy(GameEngine.Bare).OrderBy(_ => _random.Next()).Take(3)
            .Append(entry).OrderBy(_ => _random.Next()).ToArray();
        if (choices.Length < 4) { GameUnavailable(session, EligibleRequirement); return; }
        // Hint targets the correct-answer entry only; WordHint never names which button/choice it is.
        _gameHintProvider = () => WordHint(entry);
        var panel = GameSceneContent();
        AddGameQuestion(panel, LocalizedPart(entry.Definition));
        var choicesGrid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        for (var index = 0; index < choices.Length; index++)
        {
            var choice = choices[index];
            var button = GameChoiceButton($"{index + 1}   {choice.Word}", session.Game);
            button.Tag = $"game-choice-{index}";
            SetGameChoiceMetadata(button, $"game.Choice.{index + 1}", (index + 1).ToString(CultureInfo.InvariantCulture));
            button.Click += async (_, _) =>
            {
                await ResolveGameAnswerAsync(session, choice.Key == entry.Key, entry.Word, button, [entry.Key]);
            };
            choicesGrid.Children.Add(button);
        }
        ConfigureResponsiveGrid(choicesGrid, 2, 220);
        panel.Children.Add(choicesGrid);
        AddGameScene(session.Game, panel);
    }

    private void RenderHangmanRound(GameSession session)
    {
        var entry = _words.Where(word => GameAnswer(word.Word).Length is >= 4 and <= 12)
            .OrderBy(_ => _random.Next()).FirstOrDefault();
        if (entry is null) { GameUnavailable(session, EligibleRequirement); return; }
        _gameHintProvider = () => WordHint(entry!);
        var target = GameAnswer(entry.Word).ToUpperInvariant();
        var guessed = new HashSet<char>();
        var misses = 0;
        var revealed = 0;
        var panel = GameSceneContent();
        AddGameQuestion(panel, LocalizedPart(entry.Definition));
        var (figure, parts) = BuildHangmanFigure();
        var word = GamePrompt(string.Empty, 36);
        word.CharacterSpacing = 180;
        var missText = GameCaption(string.Empty);
        panel.Children.Add(word);
        panel.Children.Add(missText);
        var keyboard = new Grid { ColumnSpacing = 6, RowSpacing = 6 };
        void Refresh()
        {
            word.Text = string.Join(' ', target.Select(character => !char.IsLetter(character) || guessed.Contains(character) ? character : '_'));
            missText.Text = $"{U("Games.MissesRemaining", "Misses remaining", "Kalan hata hakkı")}: {Math.Max(0, 6 - misses)}  ·  {string.Join(' ', guessed.Order())}";
        }
        foreach (var letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ".Concat(target.Where(char.IsLetter)).Distinct())
        {
            var button = GameChoiceButton(letter.ToString(), session.Game);
            button.Tag = $"game-letter-{letter}";
            AutomationProperties.SetAutomationId(button, $"game.Letter.{letter}");
            button.MinHeight = 48;
            button.Padding = new Thickness(4);
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.CornerRadius = new CornerRadius(10);
            button.Click += async (_, _) =>
            {
                if (!GameCanAnswer(session) || guessed.Contains(letter)) return;
                button.IsEnabled = false;
                guessed.Add(letter);
                if (!target.Contains(letter)) misses++;
                while (revealed < misses) PopReveal(parts[revealed++]);
                Refresh();
                if (target.All(character => !char.IsLetter(character) || guessed.Contains(character)))
                    await ResolveGameAnswerAsync(session, true, entry.Word, button, [entry.Key]);
                else if (misses >= 6)
                    await ResolveGameAnswerAsync(session, false, entry.Word, button, [entry.Key]);
            };
            keyboard.Children.Add(button);
        }
        ConfigureResponsiveGrid(keyboard, 9, 48);
        Refresh();
        panel.Children.Add(keyboard);
        AddGameScene(session.Game, panel, figure);
    }

    // Gallows drawn once (frame) plus 6 body parts revealed one per wrong guess, in
    // the same head/body/arms/legs order as the original web game's SVG figure.
    private static (Canvas Canvas, UIElement[] Parts) BuildHangmanFigure()
    {
        var canvas = new Canvas { Width = 200, Height = 220, HorizontalAlignment = HorizontalAlignment.Center };
        var frameBrush = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
        Line Frame(double x1, double y1, double x2, double y2) => new()
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = frameBrush,
            StrokeThickness = 4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        canvas.Children.Add(Frame(20, 212, 120, 212));
        canvas.Children.Add(Frame(50, 212, 50, 18));
        canvas.Children.Add(Frame(50, 18, 132, 18));
        canvas.Children.Add(Frame(132, 18, 132, 44));

        var dangerBrush = new SolidColorBrush(Color.FromArgb(255, 190, 18, 60));
        Line Part(double x1, double y1, double x2, double y2) => new()
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = dangerBrush,
            StrokeThickness = 4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Opacity = 0,
        };
        var head = new Ellipse { Width = 32, Height = 32, Stroke = dangerBrush, StrokeThickness = 4, Opacity = 0 };
        Canvas.SetLeft(head, 116);
        Canvas.SetTop(head, 44);
        UIElement[] parts =
        [
            head,
            Part(132, 76, 132, 140),
            Part(132, 95, 107, 118),
            Part(132, 95, 157, 118),
            Part(132, 140, 112, 175),
            Part(132, 140, 152, 175),
        ];
        foreach (var part in parts) canvas.Children.Add(part);
        return (canvas, parts);
    }

    // Generic scale+fade pop-in via BackEase; reused by CodyCross's bonus-letter reveal.
    private void PopReveal(UIElement part)
    {
        if (GameMotionOff) { part.Opacity = 1; return; }
        var transform = new ScaleTransform { ScaleX = 0.3, ScaleY = 0.3 };
        part.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        part.RenderTransform = transform;
        var easing = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 };
        var storyboard = new Storyboard();
        var fade = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(280), EasingFunction = easing };
        Storyboard.SetTarget(fade, part);
        Storyboard.SetTargetProperty(fade, "Opacity");
        storyboard.Children.Add(fade);
        foreach (var property in new[] { "ScaleX", "ScaleY" })
        {
            var scale = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(280), EasingFunction = easing };
            Storyboard.SetTarget(scale, transform);
            Storyboard.SetTargetProperty(scale, property);
            storyboard.Children.Add(scale);
        }
        storyboard.Begin();
    }

    // Faithful port of the web app's `scramble-shake` keyframes (0/20/40/60/80/100% ->
    // 0/-6/6/-6/6/0 over 400ms); reused by Word Scramble and Boss Rush wrong answers.
    private void ShakeElement(FrameworkElement element)
    {
        if (GameMotionOff) return;
        var transform = new TranslateTransform();
        element.RenderTransform = transform;
        var storyboard = new Storyboard();
        var animation = new DoubleAnimationUsingKeyFrames();
        Storyboard.SetTarget(animation, transform);
        Storyboard.SetTargetProperty(animation, "X");
        TimeSpan At(double ms) => TimeSpan.FromMilliseconds(ms);
        foreach (var (time, value) in new (double, double)[] { (0, 0), (80, -6), (160, 6), (240, -6), (320, 6), (400, 0) })
            animation.KeyFrames.Add(new LinearDoubleKeyFrame { KeyTime = At(time), Value = value });
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    // Shared keyframe builder for multi-stage effects (Boss Rush's damage float/defeat).
    private static void AddKeyFrames(Storyboard storyboard, DependencyObject target, string property, params (double Time, double Value)[] frames)
    {
        var animation = new DoubleAnimationUsingKeyFrames();
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        foreach (var (time, value) in frames)
            animation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(time), Value = value });
        storyboard.Children.Add(animation);
    }

    private void RenderMemoryRound(GameSession session)
    {
        var entries = _words.DistinctBy(GameEngine.Bare).DistinctBy(word => LocalizedPart(word.Definition)).OrderBy(_ => _random.Next()).Take(6).ToArray();
        if (entries.Length < 6) { GameUnavailable(session, EligibleRequirement); return; }
        var epoch = _gameEpoch;
        // No single "current" entry with 6 pairs on board at once; hint on the first not-yet-matched pair in board order.
        var matchedKeys = new HashSet<string>();
        _gameHintProvider = () => WordHint(entries.FirstOrDefault(candidate => !matchedKeys.Contains(candidate.Key)) ?? entries[0]);
        var tiles = entries.SelectMany(entry => new[]
            {
                (Entry: entry, Text: entry.Word),
                (Entry: entry, Text: LocalizedPart(entry.Definition)),
            })
            .OrderBy(_ => _random.Next()).ToArray();
        var panel = GameSceneContent();
        AddGameQuestion(panel, U("Games.Memory.Pairs", "Match 6 pairs", "6 çifti eşleştirin"));
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        Button? firstButton = null;
        VocabularyEntry? firstEntry = null;
        var matches = 0;
        for (var index = 0; index < tiles.Length; index++)
        {
            var tile = tiles[index];
            var cardNumber = index + 1;
            var button = GameChoiceButton("✦", session.Game);
            button.Tag = cardNumber;
            SetGameChoiceMetadata(button, $"game.Card.{cardNumber}");
            AutomationProperties.SetName(button, $"{U("Games.Memory.Hidden", "Hidden card", "Gizli kart")} {cardNumber}");
            AutomationProperties.SetHelpText(button, GameInstructions(session.Game));
            button.Click += async (_, _) =>
            {
                if (!IsCurrentGameRound(session, epoch) || !GameCanAnswer(session) || _gameLocalBusy || !button.IsEnabled || ReferenceEquals(button, firstButton)) return;
                void RevealTile()
                {
                    button.Content = new TextBlock { Text = tile.Text, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
                    AutomationProperties.SetName(button, tile.Text);
                    AutomationProperties.SetItemStatus(button, U("Games.Memory.Open", "Open card", "Açık kart"));
                }
                if (firstButton is null)
                {
                    RevealTile(); // Selecting the first tile is not a scored attempt.
                    firstButton = button;
                    firstEntry = tile.Entry;
                    return;
                }
                var previous = firstButton;
                var previousEntry = firstEntry!;
                var correct = previousEntry.Key == tile.Entry.Key;
                IReadOnlyList<string> reviewedKeys = correct ? [tile.Entry.Key] : [previousEntry.Key, tile.Entry.Key];
                // Do not reveal/clear selections or count a match until the save succeeds.
                // A mismatch tests these two selected words, never all six board words.
                if (!await RecordGameSubAnswerAsync(session, correct, button, reviewedKeys) || !IsCurrentGameRound(session, epoch)) return;
                RevealTile();
                firstButton = null;
                firstEntry = null;
                if (correct)
                {
                    previous.IsEnabled = false;
                    button.IsEnabled = false;
                    matches++;
                    matchedKeys.Add(tile.Entry.Key);
                    if (matches == 6) await ResolveGameAnswerAsync(session, true, "6 / 6", button, reviewedKeys, answerAlreadyRecorded: true);
                }
                else
                {
                    if (!await PauseGameFeedbackAsync(session, U("Games.Memory.Mismatch", "Not a pair", "Eşleşmedi") + "\n" + tile.Text + "\n" + AutomationProperties.GetName(previous)) || !IsCurrentGameRound(session, epoch)) return;
                    previous.Content = "✦";
                    button.Content = "✦";
                    var previousCardNumber = previous.Tag is int number ? number : 0;
                    AutomationProperties.SetName(previous, $"{U("Games.Memory.Hidden", "Hidden card", "Gizli kart")} {previousCardNumber}");
                    AutomationProperties.SetName(button, $"{U("Games.Memory.Hidden", "Hidden card", "Gizli kart")} {cardNumber}");
                    AutomationProperties.SetItemStatus(previous, "");
                    AutomationProperties.SetItemStatus(button, "");
                }
            };
            grid.Children.Add(button);
        }
        ConfigureResponsiveGrid(grid, 4, Font(115));
        panel.Children.Add(grid);
        AddGameScene(session.Game, panel);
    }

    private void RenderWordGuessRound(GameSession session)
    {
        var pool = _words.Where(word => GameAnswer(word.Word).All(char.IsLetter) && GameAnswer(word.Word).Length is >= 4 and <= 9)
            .DistinctBy(GameEngine.Bare).OrderBy(word => word.Key, StringComparer.Ordinal).ToArray();
        if (pool.Length == 0) { GameUnavailable(session, EligibleRequirement); return; }
        var entry = pool[session.Game.Id == "dailychallenge" ? GameEngine.DailyIndex(pool.Length, StudyContext, Today) : _random.Next(pool.Length)];
        var target = GameAnswer(entry.Word).ToUpperInvariant();
        _gameHintProvider = () => WordHint(entry);
        var attempts = 0;
        var epoch = _gameEpoch;
        var panel = GameSceneContent();
        AddGameQuestion(panel, U("Games.Guess.Prompt", "Guess the hidden word", "Gizli kelimeyi tahmin edin"));
        panel.Children.Add(GameCaption($"{target.Length} · " + U("Games.Guess.Legend", "6 tries · ✓ exact · ~ elsewhere · × absent", "6 deneme · ✓ doğru yerde · ~ başka yerde · × yok")));
        var board = new StackPanel { Spacing = 7, Width = 308, HorizontalAlignment = HorizontalAlignment.Stretch };
        var input = GameInput(panel);
        input.MaxLength = target.Length;
        var attemptCount = GameCaption($"{U("Games.Tries", "Tries", "Deneme")}: 0 / 6");
        panel.Children.Add(attemptCount);
        SubmitGame(panel, session, async submit =>
        {
            if (!GameCanAnswer(session) || _gameLocalBusy) return;
            var guess = GameAnswer(input.Text).ToUpperInvariant();
            if (guess.Length != target.Length) return;
            var correct = guess == target;
            if (!await RecordGameSubAnswerAsync(session, correct, submit, [entry.Key]) || !IsCurrentGameRound(session, epoch)) return;
            attempts++;
            attemptCount.Text = $"{U("Games.Tries", "Tries", "Deneme")}: {attempts} / 6";
            board.Children.Add(WordGuessRow(guess, target));
            input.Text = string.Empty;
            if (correct) await ResolveGameAnswerAsync(session, true, entry.Word, submit, [entry.Key], answerAlreadyRecorded: true);
            else if (attempts >= 6) await ResolveGameAnswerAsync(session, false, entry.Word, submit, [entry.Key], answerAlreadyRecorded: true);
        }, canSubmit: () => GameAnswer(input.Text).Length == target.Length && GameAnswer(input.Text).All(char.IsLetter));
        AddGameScene(session.Game, panel, board);
        input.Focus(FocusState.Programmatic);
    }

    private UIElement WordGuessRow(string guess, string target)
    {
        var row = new Grid { ColumnSpacing = 3, HorizontalAlignment = HorizontalAlignment.Stretch };
        var feedback = GameEngine.LetterFeedback(guess, target);
        AutomationProperties.SetLiveSetting(row, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        for (var index = 0; index < guess.Length; index++)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var color = feedback[index] == 2
                ? Color.FromArgb(255, 4, 120, 87)
                : feedback[index] == 1 ? Color.FromArgb(255, 161, 98, 7) : Color.FromArgb(255, 71, 85, 105);
            var tile = new Border
            {
                MinHeight = ReadingSize(18) * 2.8,
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(color),
                Child = new TextBlock
                {
                    Text = guess[index] + "\n" + (feedback[index] == 2 ? "✓" : feedback[index] == 1 ? "~" : "×"),
                    FontSize = ReadingSize(18),
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            AutomationProperties.SetName(tile, $"{index + 1}: {guess[index]} — " + (feedback[index] == 2
                ? U("Games.Letter.Exact", "Correct position", "Doğru konum") : feedback[index] == 1
                ? U("Games.Letter.Present", "Present elsewhere", "Başka konumda var") : U("Games.Letter.Absent", "Absent", "Yok")));
            Grid.SetColumn(tile, index);
            row.Children.Add(tile);
        }
        return row;
    }

    // Faithful port of webapp/scramble.js: click scrambled tiles to fill answer
    // slots, click a filled slot to return its tile, auto-check when full, shake
    // + retry (up to 3 wrong) on a bad attempt.
    private void RenderScrambleRound(GameSession session)
    {
        var entry = _words.Where(word => GameAnswer(word.Word).All(char.IsLetter) && GameAnswer(word.Word).Length is >= 3 and <= 10)
            .OrderBy(_ => _random.Next()).FirstOrDefault();
        if (entry is null) { GameUnavailable(session, EligibleRequirement); return; }
        _gameHintProvider = () => WordHint(entry!);
        var target = GameAnswer(entry.Word).ToUpperInvariant();
        var scrambled = target.ToCharArray();
        var shuffleAttempts = 0;
        do
        {
            scrambled = scrambled.OrderBy(_ => _random.Next()).ToArray();
        } while (scrambled.Length > 1 && new string(scrambled) == target && ++shuffleAttempts < 8);
        var used = new bool[scrambled.Length];
        var slots = new int?[target.Length];
        var wrong = 0;
        var epoch = _gameEpoch;

        var panel = GameSceneContent();
        AddGameQuestion(panel, LocalizedPart(entry.Definition));
        var wrongCaption = GameCaption($"{target.Length} · {U("Games.Tries", "Tries", "Deneme")}: 3");
        panel.Children.Add(wrongCaption);
        var answerRow = new Grid { ColumnSpacing = 6, RowSpacing = 6 };
        var tilesRow = new Grid { ColumnSpacing = 6, RowSpacing = 6 };
        var slotButtons = new Button[target.Length];
        var tileButtons = new Button[scrambled.Length];

        void RefreshAnswer()
        {
            for (var i = 0; i < slotButtons.Length; i++)
            {
                var tileIndex = slots[i];
                var text = tileIndex is int t ? scrambled[t].ToString() : string.Empty;
                slotButtons[i].Content = text;
                slotButtons[i].IsEnabled = tileIndex is not null;
                AutomationProperties.SetName(slotButtons[i], $"{i + 1}: " + (text.Length > 0 ? text : U("Games.Tile.Empty", "Empty slot", "Boş yuva")));
            }
        }
        void RefreshTiles()
        {
            for (var i = 0; i < tileButtons.Length; i++)
                tileButtons[i].Visibility = used[i] ? Visibility.Collapsed : Visibility.Visible;
        }
        async void CheckComplete()
        {
            if (!GameCanAnswer(session) || _gameLocalBusy || Array.IndexOf(slots, null) >= 0) return;
            var attempt = new string(slots.Select(s => scrambled[s!.Value]).ToArray());
            var correct = attempt == target;
            if (!await RecordGameSubAnswerAsync(session, correct, slotButtons[0], [entry.Key]) || !IsCurrentGameRound(session, epoch)) return;
            _gameLocalBusy = true;
            if (correct)
            {
                await ResolveGameAnswerAsync(session, true, entry.Word, slotButtons[0], [entry.Key], answerAlreadyRecorded: true);
                return;
            }
            wrong++;
            ShakeElement(answerRow);
            if (wrong >= 3)
            {
                await ResolveGameAnswerAsync(session, false, entry.Word, slotButtons[0], [entry.Key], answerAlreadyRecorded: true);
                return;
            }
            if (!await PauseGameFeedbackAsync(session, U("Games.TryAgain", "Not yet — try again", "Henüz değil — tekrar deneyin") + "\n" + attempt) || !IsCurrentGameRound(session, epoch)) return;
            wrongCaption.Text = $"{target.Length} · {U("Games.Tries", "Tries", "Deneme")}: {3 - wrong}";
            Array.Clear(used);
            Array.Clear(slots);
            RefreshTiles();
            RefreshAnswer();
        }

        for (var i = 0; i < target.Length; i++)
        {
            var index = i;
            var button = GameChoiceButton(string.Empty, session.Game);
            button.MinHeight = 48;
            button.MinWidth = 48;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            SetGameChoiceMetadata(button, $"game.Slot.{i + 1}");
            AutomationProperties.SetHelpText(button, GameInstructions(session.Game));
            button.Click += (_, _) =>
            {
                if (!GameCanAnswer(session) || _gameLocalBusy) return;
                var tileIndex = slots[index];
                if (tileIndex is null) return;
                used[tileIndex.Value] = false;
                slots[index] = null;
                RefreshTiles();
                RefreshAnswer();
            };
            slotButtons[i] = button;
            answerRow.Children.Add(button);
        }
        for (var i = 0; i < scrambled.Length; i++)
        {
            var index = i;
            var button = GameChoiceButton(scrambled[i].ToString(), session.Game);
            button.MinHeight = 48;
            button.MinWidth = 48;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            SetGameChoiceMetadata(button, $"game.Tile.{i + 1}");
            AutomationProperties.SetName(button, $"{i + 1}: {scrambled[i]}");
            button.Click += (_, _) =>
            {
                if (!GameCanAnswer(session) || _gameLocalBusy || used[index]) return;
                var emptySlot = Array.IndexOf(slots, null);
                if (emptySlot < 0) return;
                used[index] = true;
                slots[emptySlot] = index;
                RefreshTiles();
                RefreshAnswer();
                CheckComplete();
            };
            tileButtons[i] = button;
            tilesRow.Children.Add(button);
        }
        RefreshAnswer();
        RefreshTiles();
        ConfigureResponsiveGrid(answerRow, target.Length, 48);
        ConfigureResponsiveGrid(tilesRow, target.Length, 48);
        panel.Children.Add(answerRow);
        panel.Children.Add(tilesRow);
        AddGameScene(session.Game, panel);
    }

    // Faithful port of webapp/bossrush.js: 3 sequential bosses at 5 HP each, a
    // 4-option "pick the definition" question per turn, floating damage numbers,
    // a hit-pulse/HP-bar-shrink on a correct answer, and a scale+rotate+fade
    // defeat animation when a boss's HP reaches 0. The whole 3-boss gauntlet is
    // ONE session round (like Memory's 6-pair board), completing only when all
    // bosses fall or hearts run out.
    private void RenderBossRushRound(GameSession session)
    {
        const int bossCount = 3;
        const int bossMaxHp = 5;
        const int startHearts = 3;
        string[] bossGlyphs = ["🐻", "🦊", "🐼"];
        var bossIndex = 0;
        var bossHp = bossMaxHp;
        var hearts = startHearts;
        var answered = false;
        var epoch = _gameEpoch;
        var askedKeys = new HashSet<string>();
        VocabularyEntry? correctEntry = null;

        const double hpBarWidth = 120;
        var panel = GameSceneContent();
        var statusPanel = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Stretch };
        var bossCaption = GameCaption($"{U("Games.Boss", "Boss", "Rakip")} {bossIndex + 1} / {bossCount}");
        statusPanel.Children.Add(bossCaption);

        var avatar = new TextBlock { Text = bossGlyphs[0], FontSize = Font(64), HorizontalAlignment = HorizontalAlignment.Center };
        var arenaOverlay = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
        arenaOverlay.Children.Add(avatar);
        statusPanel.Children.Add(arenaOverlay);

        var hpFill = new Border
        {
            Height = 12,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(255, 190, 18, 60)),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = hpBarWidth,
        };
        var hpTrack = new Border
        {
            Width = hpBarWidth,
            Height = 12,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = hpFill,
        };
        statusPanel.Children.Add(new Viewbox { Child = hpTrack, Stretch = Stretch.Uniform, MaxHeight = 12 });
        var hpText = GameCaption($"{bossHp} / {bossMaxHp}");
        statusPanel.Children.Add(hpText);
        var heartsText = GameCaption(new string('♥', hearts) + new string('♡', startHearts - hearts));
        statusPanel.Children.Add(heartsText);

        var word = AddGameQuestion(panel, string.Empty);
        var options = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        var optionButtons = new Button[4];
        var currentChoices = new VocabularyEntry[4];

        void UpdateHp()
        {
            var clamped = Math.Max(0, bossHp);
            if (GameMotionOff)
            {
                hpFill.Width = hpBarWidth * clamped / bossMaxHp;
                hpText.Text = $"{clamped} / {bossMaxHp}";
                return;
            }
            var storyboard = new Storyboard();
            var animation = new DoubleAnimation { To = hpBarWidth * clamped / bossMaxHp, Duration = TimeSpan.FromMilliseconds(250), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(animation, hpFill);
            Storyboard.SetTargetProperty(animation, "Width");
            storyboard.Children.Add(animation);
            storyboard.Begin();
            hpText.Text = $"{clamped} / {bossMaxHp}";
        }
        void UpdateHearts() => heartsText.Text = new string('♥', hearts) + new string('♡', startHearts - hearts);

        void SpawnFloatingText(string text, Color color, double size)
        {
            if (GameMotionOff) return;
            var label = new TextBlock
            {
                Text = text,
                FontSize = Font(size),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            arenaOverlay.Children.Add(label);
            var transform = new TranslateTransform();
            label.RenderTransform = transform;
            var storyboard = new Storyboard();
            AddKeyFrames(storyboard, transform, "Y", (0, 0), (600, -34));
            AddKeyFrames(storyboard, label, "Opacity", (0, 1), (600, 0));
            storyboard.Completed += (_, _) => { if (IsCurrentGameRound(session, epoch)) arenaOverlay.Children.Remove(label); };
            storyboard.Begin();
        }

        void HitAvatar()
        {
            if (GameMotionOff) return;
            var transform = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
            avatar.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            avatar.RenderTransform = transform;
            var storyboard = new Storyboard();
            foreach (var property in new[] { "ScaleX", "ScaleY" })
            {
                var animation = new DoubleAnimation { To = 0.85, Duration = TimeSpan.FromMilliseconds(110), AutoReverse = true, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                Storyboard.SetTarget(animation, transform);
                Storyboard.SetTargetProperty(animation, property);
                storyboard.Children.Add(animation);
            }
            storyboard.Begin();
        }

        async Task PlayDefeatAsync()
        {
            if (GameMotionOff) { avatar.Opacity = 0; return; }
            var transform = new CompositeTransform();
            avatar.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            avatar.RenderTransform = transform;
            var storyboard = new Storyboard();
            AddKeyFrames(storyboard, transform, "ScaleX", (0, 1), (300, 1.25), (600, 0));
            AddKeyFrames(storyboard, transform, "ScaleY", (0, 1), (300, 1.25), (600, 0));
            AddKeyFrames(storyboard, transform, "Rotation", (0, 0), (300, -4), (600, 8));
            AddKeyFrames(storyboard, avatar, "Opacity", (0, 1), (300, 1), (600, 0));
            var tcs = new TaskCompletionSource();
            storyboard.Completed += (_, _) => tcs.TrySetResult();
            storyboard.Begin();
            await tcs.Task;
        }

        VocabularyEntry PickQuestionWord()
        {
            var fresh = _words.Where(candidate => !askedKeys.Contains(candidate.Key)).ToArray();
            var usable = fresh.Length >= 4 ? fresh : _words.ToArray();
            var picked = usable[_random.Next(usable.Length)];
            askedKeys.Add(picked.Key);
            return picked;
        }

        void NextQuestion()
        {
            if (!IsCurrentGameRound(session, epoch)) return;
            answered = false;
            correctEntry = PickQuestionWord();
            // Re-set every time a new sub-question is presented so the Hint button never goes stale mid-gauntlet.
            _gameHintProvider = () => WordHint(correctEntry!);
            word.Text = correctEntry.Word;
            var choices = _words.Where(candidate => candidate.Key != correctEntry.Key).OrderBy(_ => _random.Next()).Take(3)
                .Append(correctEntry).OrderBy(_ => _random.Next()).ToArray();
            for (var i = 0; i < optionButtons.Length; i++)
            {
                currentChoices[i] = choices[i];
                var label = LocalizedPart(choices[i].Definition);
                optionButtons[i].Content = label;
                optionButtons[i].IsEnabled = true;
                AutomationProperties.SetName(optionButtons[i], label);
            }
        }

        async Task AnswerAsync(int slot, Button button)
        {
            if (answered || !IsCurrentGameRound(session, epoch) || !GameCanAnswer(session) || _gameLocalBusy || correctEntry is null) return;
            var tested = correctEntry;
            var correct = currentChoices[slot].Key == tested.Key;
            if (!await RecordGameSubAnswerAsync(session, correct, button, [tested.Key]) || !IsCurrentGameRound(session, epoch)) return;
            answered = true;
            _gameLocalBusy = true;
            foreach (var other in optionButtons) other.IsEnabled = false;
            if (correct)
            {
                bossHp--;
                SpawnFloatingText("-1", Color.FromArgb(255, 190, 18, 60), 22);
                HitAvatar();
                UpdateHp();
                if (bossHp <= 0)
                {
                    await PlayDefeatAsync();
                    if (!IsCurrentGameRound(session, epoch)) return;
                    bossIndex++;
                    if (bossIndex >= bossCount)
                    {
                        await ResolveGameAnswerAsync(session, true, tested.Word, button, [tested.Key], answerAlreadyRecorded: true);
                        return;
                    }
                    bossHp = bossMaxHp;
                    avatar.Opacity = 1;
                    avatar.RenderTransform = null;
                    avatar.Text = bossGlyphs[bossIndex % bossGlyphs.Length];
                    bossCaption.Text = $"{U("Games.Boss", "Boss", "Rakip")} {bossIndex + 1} / {bossCount}";
                    UpdateHp();
                }
            }
            else
            {
                hearts--;
                SpawnFloatingText(T("Common.Wrong"), Color.FromArgb(220, 148, 163, 184), 16);
                ShakeElement(options);
                UpdateHearts();
                if (hearts <= 0)
                {
                    await ResolveGameAnswerAsync(session, false, tested.Word, button, [tested.Key], answerAlreadyRecorded: true);
                    return;
                }
            }
            if (!IsCurrentGameRound(session, epoch)) return;
            if (!await PauseGameFeedbackAsync(session, $"{(correct ? T("Common.Correct") : T("Common.Wrong"))}\n{tested.Word} — {LocalizedPart(tested.Definition)}") || !IsCurrentGameRound(session, epoch)) return;
            NextQuestion();
        }

        for (var i = 0; i < optionButtons.Length; i++)
        {
            var slot = i;
            var button = GameChoiceButton(string.Empty, session.Game);
            button.Tag = $"game-choice-{i}";
            button.Click += async (_, _) => await AnswerAsync(slot, button);
            optionButtons[i] = button;
            options.Children.Add(button);
        }
        ConfigureResponsiveGrid(options, 2, 220);
        panel.Children.Add(options);
        NextQuestion();
        AddGameScene(session.Game, panel, statusPanel);
    }

    // Faithful port of webapp/codycross.js: 5 themed clues, each revealing one
    // "bonus" letter into a shared code strip (popped in the same way as
    // Hangman's figure) once its row is typed correctly.
    private void RenderCodyCrossRound(GameSession session)
    {
        const int groupSize = 5;
        var pool = _words.Where(word => GameAnswer(word.Word).All(char.IsLetter) && GameAnswer(word.Word).Length is >= 3 and <= 9)
            .GroupBy(word => GameAnswer(word.Word).ToUpperInvariant()).Select(group => group.First())
            .OrderBy(_ => _random.Next()).ToArray();
        if (pool.Length < groupSize) { GameUnavailable(session, EligibleRequirement); return; }
        var entries = pool.Take(groupSize).ToArray();
        var bare = entries.Select(word => GameAnswer(word.Word).ToUpperInvariant()).ToArray();
        var bonusIndex = Enumerable.Range(0, entries.Length).Select(i => i % bare[i].Length).ToArray();
        var solved = new bool[entries.Length];
        var activeRow = 0;
        var epoch = _gameEpoch;

        var panel = GameSceneContent();
        var clueProgress = GameCaption("");
        panel.Children.Add(clueProgress);
        var rowsPanel = new StackPanel { Spacing = 16 };
        panel.Children.Add(rowsPanel);

        var bonusPanel = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Stretch };
        bonusPanel.Children.Add(GameCaption(U("Games.Cody.Bonus", "Bonus code", "Bonus kod")));
        var bonusRow = new Grid { ColumnSpacing = 4, RowSpacing = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
        var bonusBoxes = new Border[entries.Length];
        var bonusText = new TextBlock[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            var text = new TextBlock { FontSize = ReadingSize(18), FontWeight = Microsoft.UI.Text.FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var box = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)),
                Child = text,
            };
            Grid.SetColumn(box, i);
            bonusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bonusRow.Children.Add(box);
            bonusBoxes[i] = box;
            bonusText[i] = text;
        }
        ConfigureResponsiveGrid(bonusRow, 5, 38);

        void RevealBonus(int row)
        {
            bonusText[row].Text = bare[row][bonusIndex[row]].ToString();
            bonusBoxes[row].BorderBrush = new SolidColorBrush(Color.FromArgb(255, 217, 119, 6));
            bonusBoxes[row].Background = new SolidColorBrush(Color.FromArgb(40, 217, 119, 6));
            PopReveal(bonusBoxes[row]);
        }

        async Task SubmitRowAsync(int row, TextBox input, Grid cellsGrid, Button submit)
        {
            if (!IsCurrentGameRound(session, epoch) || !GameCanAnswer(session) || _gameLocalBusy || solved[row] || row != activeRow) return;
            var typed = GameAnswer(input.Text).ToUpperInvariant();
            if (typed.Length == 0) return;
            var correct = typed == bare[row];
            if (!await RecordGameSubAnswerAsync(session, correct, submit, [entries[row].Key]) || !IsCurrentGameRound(session, epoch)) return;
            if (correct)
            {
                solved[row] = true;
                RevealBonus(row);
                activeRow++;
                if (activeRow >= entries.Length)
                {
                    await ResolveGameAnswerAsync(session, true, string.Join(' ', entries.Select(e => e.Word)), submit, [entries[row].Key], answerAlreadyRecorded: true);
                    return;
                }
                // Re-set every time the active row advances so the Hint button never goes stale mid-round.
                _gameHintProvider = () => WordHint(entries[Math.Min(activeRow, entries.Length - 1)]);
                if (!await PauseGameFeedbackAsync(session, T("Common.Correct") + "\n" + entries[row].Word) || !IsCurrentGameRound(session, epoch)) return;
                RenderAllRows();
            }
            else
            {
                ShakeElement(cellsGrid);
                if (!await PauseGameFeedbackAsync(session, U("Games.TryAgain", "Not yet — try again", "Henüz değil — tekrar deneyin") + "\n" + typed) || !IsCurrentGameRound(session, epoch)) return;
                input.Text = string.Empty;
            }
        }

        void RenderAllRows()
        {
            if (!IsCurrentGameRound(session, epoch)) return;
            rowsPanel.Children.Clear();
            clueProgress.Text = $"{U("Games.Clue", "Clue", "İpucu")} {activeRow + 1} / {entries.Length}";
            for (var row = 0; row < entries.Length; row++)
            {
                // The bonus strip already shows solved progress. Render only the
                // active clue so completed/future rows never grow the page.
                if (row != activeRow) continue;

                var word = bare[row];
                var solvedRow = solved[row];
                var cellsGrid = new Grid { ColumnSpacing = 6, MaxWidth = word.Length * 38, HorizontalAlignment = HorizontalAlignment.Stretch };
                var texts = new TextBlock[word.Length];
                for (var col = 0; col < word.Length; col++)
                {
                    var isBonus = col == bonusIndex[row];
                    var text = new TextBlock { FontSize = ReadingSize(18), FontWeight = Microsoft.UI.Text.FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    if (solvedRow) text.Text = word[col].ToString();
                    var box = new Border
                    {
                        Height = 32,
                        CornerRadius = new CornerRadius(6),
                        BorderThickness = new Thickness(2),
                        BorderBrush = new SolidColorBrush(solvedRow
                            ? Color.FromArgb(255, 4, 120, 87)
                            : isBonus ? Color.FromArgb(255, 217, 119, 6) : Color.FromArgb(130, 255, 255, 255)),
                        Background = solvedRow ? new SolidColorBrush(Color.FromArgb(40, 4, 120, 87)) : null,
                        Child = text,
                    };
                    Grid.SetColumn(box, col);
                    cellsGrid.ColumnDefinitions.Add(new ColumnDefinition());
                    cellsGrid.Children.Add(box);
                    texts[col] = text;
                }

                var rowStack = new StackPanel { Spacing = 8 };
                AddGameQuestion(rowStack, LocalizedPart(entries[row].Definition));
                rowStack.Children.Add(cellsGrid);

                if (row == activeRow && !solvedRow)
                {
                    var capturedRow = row;
                    var input = new TextBox
                    {
                        MaxLength = word.Length,
                        FontSize = ReadingSize(20),
                        MinHeight = 48, MinWidth = 48,
                        Header = GameCaption(T("Game.TypeAnswer")),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        PlaceholderText = T("Game.TypeAnswer"),
                    };
                    AutomationProperties.SetName(input, T("Game.TypeAnswer"));
                    AutomationProperties.SetAutomationId(input, "game.Answer");
                    input.TextChanged += (_, _) =>
                    {
                        var typed = input.Text.ToUpperInvariant();
                        for (var col = 0; col < texts.Length; col++)
                            texts[col].Text = col < typed.Length ? typed[col].ToString() : string.Empty;
                    };
                    rowStack.Children.Add(input);
                    SubmitGame(rowStack, session, submit => SubmitRowAsync(capturedRow, input, cellsGrid, submit));
                    input.Focus(FocusState.Programmatic);
                }
                rowsPanel.Children.Add(rowStack);
            }
        }

        _gameHintProvider = () => WordHint(entries[Math.Min(activeRow, entries.Length - 1)]);
        RenderAllRows();
        bonusPanel.Children.Add(bonusRow);
        AddGameScene(session.Game, panel, bonusPanel);
    }

    private async Task ResolveGameAnswerAsync(GameSession session, bool correct, string feedback, Control source,
        IReadOnlyList<string> reviewedKeys, bool answerAlreadyRecorded = false)
    {
        if (!source.IsLoaded || !IsWithin(source, PageContent) || !GameCanAnswer(session) || !_gameTurn.TryClose(_gameEpoch)) return;
        _resolvingGame = session;
        source.IsEnabled = false;
        var epoch = _gameEpoch;
        try
        {
            // Boards record atomic sub-answers before advancing. Their final feedback
            // awards round points only, not a second objective answer or word review.
            if (!answerAlreadyRecorded && !await PersistGameAnswerAsync(session, epoch, correct, reviewedKeys)) return;
            if (!IsCurrentGameRound(session, epoch)) return;
            if (correct)
            {
                session.Streak++;
                session.Score = Math.Min(LearningEngine.MaxCounter, session.Score + _gameRoundPoints +
                    (session.Game.Id is "cluedetective" or "scrabble" ? 0 : Math.Min(session.Streak, 10) * 15));
            }
            else { session.Streak = 0; session.Lives--; }
            AnimatePulse(source, correct);
            var message = $"{(correct ? T("Common.Correct") : T("Common.Wrong"))}\n{feedback}\n{T("Game.Score")}: {session.Score:N0}";
            if (!await PauseGameFeedbackAsync(session, message)) return;
            if (!IsCurrentGameRound(session, epoch)) return;
            session.Round++;
            if (session.Lives <= 0 || (GameEngine.RoundLimit(session, _gameMode) is { } rounds && session.Round > rounds) ||
                (session.Game.Id == "categorysprint" && _categoryFound.Count == _categoryWords.Length))
                await CompleteGameAsync(session);
            else RenderGameRound(session);
        }
        finally { if (_resolvingGame == session) _resolvingGame = null; }
    }

    private async Task CompleteGameAsync(GameSession session)
    {
        if (_activeGame != session || _completingGame == session || _gameCompletion.Committed) return;
        _completingGame = session;
        _gameTurn.TryClose(_gameEpoch);
        StopGameTimer();
        var epoch = _gameEpoch;
        var key = _gameScoreKey;
        var completion = _gameCompletion;
        try
        {
            // Completion and best score share one snapshot/save. A failed save rolls
            // both back; the same per-session gate is retried, not a new completion.
            while (IsCurrentGameRound(session, epoch))
            {
                if (await completion.SaveAsync(() => MutateStudyAsync(() =>
                {
                    LearningEngine.RegisterGameCompleted(_progress);
                    if (session.Score > _progress.GameBestScores.GetValueOrDefault(key)) _progress.GameBestScores[key] = session.Score;
                }), () => IsCurrentGameRound(session, epoch))) break;
                if (!IsCurrentGameRound(session, epoch) || !await PauseGameFeedbackAsync(session,
                    U("Games.CompletionRetry", "Game completion and best score were not saved. Continue retries; leaving keeps recorded answers but does not count this completion.", "Oyun tamamlanması ve en iyi puan kaydedilmedi. Devam tekrar dener; çıkmak kaydedilen yanıtları korur ancak bu tamamlanmayı saymaz."))) return;
            }
            if (_dialogClosed is { } pending) await pending.Task;
            if (!completion.Committed || !IsCurrentGameRound(session, epoch)) return;
            _activeGame = null;
            RenderGameSummary(session);
        }
        finally { if (_completingGame == session) _completingGame = null; }
    }

    private UIElement GameHud(GameSession session)
    {
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 6 };
        AutomationProperties.SetLiveSetting(grid, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        _gameScoreText = Heading(session.Score.ToString("N0"), 20);
        _gameStreakText = Heading(session.Streak.ToString(), 20);
        _gameRoundText = Heading("", 20);
        _gameLivesText = null;
        AddHudCell(grid, 0, T("Game.Score"), _gameScoreText, null, "");
        AddHudCell(grid, 1, T("Game.Streak"), _gameStreakText, null, "");
        var rounds = GameEngine.RoundLimit(session, _gameMode);
        _gameRoundText.Text = rounds.HasValue ? $"{session.Round}/{rounds.Value}" : session.Round.ToString();
        AddHudCell(grid, 2, T("Game.Round"), _gameRoundText, null, "");
        if (_gameMode == "timed")
        {
            _gameTimerText = Heading(session.SecondsRemaining.ToString(), 20);
            _gameTimerProgress = new ProgressBar { Minimum = 0, Maximum = GameEngine.TimedSeconds, Value = session.SecondsRemaining, Height = 3 };
            AddHudCell(grid, 3, T("Game.Time"), _gameTimerText, _gameTimerProgress, "");
        }
        else
        {
            _gameLivesText = Heading(new string('♥', Math.Max(0, session.Lives)), 20);
            AddHudCell(grid, 3, T("Game.Lives"), _gameLivesText, null, "");
        }
        AutomationProperties.SetAutomationId(_gameScoreText, "game.Score");
        AutomationProperties.SetAutomationId(_gameStreakText, "game.Streak");
        AutomationProperties.SetAutomationId(_gameRoundText, "game.Round");
        ConfigureResponsiveGrid(grid, 4, Font(110));
        return grid;
    }

    private void SyncGameHud(GameSession session)
    {
        if (_activeGame != session) return;
        if (_gameScoreText is not null) _gameScoreText.Text = session.Score.ToString("N0");
        if (_gameStreakText is not null) _gameStreakText.Text = session.Streak.ToString();
        if (_gameLivesText is not null) _gameLivesText.Text = new string('♥', Math.Max(0, session.Lives));
    }

    private void RenderGameSummary(GameSession session)
    {
        PageContent.Children.Clear();
        PageContent.Children.Add(GameToolbar(() => RenderGames(session.Game.Group),
            () => ShowNotice(T("Game.Hint"), GameInstructions(session.Game), InfoBarSeverity.Informational), session.Game));
        var summary = GameSceneContent();
        summary.Children.Add(GamePrompt("✓ " + T("Game.Finished"), 28));
        summary.Children.Add(GamePrompt(session.Score.ToString("N0"), 44));
        summary.Children.Add(GameCaption(T("Game.Score")));
        summary.Children.Add(GameCaption(GameBestLabel(session.Game)));
        var replay = GameActionButton(T("Game.PlayAgain"), "", session.Game);
        AutomationProperties.SetAutomationId(replay, "game.PlayAgain");
        replay.Click += (_, _) => StartGame(session.Game);
        summary.Children.Add(replay);
        var back = GameChoiceButton(T("Games.Back"), session.Game);
        back.HorizontalContentAlignment = HorizontalAlignment.Center;
        back.Click += (_, _) => RenderGames(session.Game.Group);
        summary.Children.Add(back);
        AddGameScene(session.Game, summary);
        replay.Loaded += (_, _) => replay.Focus(FocusState.Programmatic);
    }

    private TextBlock GameFeedbackText(StackPanel feedback, string message)
    {
        var firstLine = message.Split('\n', 2)[0].Trim();
        var correct = firstLine == U("Kids.Games.GotIt", "You got it!", "Bildin!");
        var incorrect = firstLine == U("Kids.Games.TryAgain", "Good try! Let's learn this one.", "Güzel deneme! Bunu birlikte öğrenelim.");
        var accent = new SolidColorBrush(correct ? Color.FromArgb(255, 134, 239, 172) : incorrect
            ? Color.FromArgb(255, 253, 164, 175) : Color.FromArgb(255, 147, 197, 253));
        var heading = GamePrompt((correct ? "✓ " : incorrect ? "↻ " : "ⓘ ") + U("Kids.Games.LookAnswer", "Look at the answer", "Yanıta bak"), 24);
        heading.TextAlignment = TextAlignment.Left;
        heading.Foreground = accent;
        AutomationProperties.SetHeadingLevel(heading, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
        var text = GamePrompt(message, 20);
        text.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
        text.TextAlignment = TextAlignment.Left;
        AutomationProperties.SetAutomationId(text, "game.Feedback");
        AutomationProperties.SetName(text, message);
        var copy = new StackPanel { Spacing = 12, Children = { heading, text } };
        var card = new Border
        {
            Child = copy, BorderBrush = accent, BorderThickness = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(14, 8, 8, 8), Background = new SolidColorBrush(Color.FromArgb(255, 15, 23, 42)),
        };
        feedback.Children.Add(card);
        feedback.Children.Add(GameCaption(U("Kids.Games.NextHint", "Ready? Choose Next.", "Hazır mısın? Sonraki'ni seç.")));
        return text;
    }

    private string FriendlyGameFeedback(string message)
    {
        var parts = message.Split('\n', 2);
        if (parts[0] == T("Common.Correct")) parts[0] = U("Kids.Games.GotIt", "You got it!", "Bildin!");
        else if (parts[0] == T("Common.Wrong") || parts[0] == U("Games.TryAgain", "Not yet — try again", "Henüz değil — tekrar deneyin"))
            parts[0] = U("Kids.Games.TryAgain", "Good try! Let's learn this one.", "Güzel deneme! Bunu birlikte öğrenelim.");
        return string.Join('\n', parts);
    }

    private static void AddHudCell(Grid grid, int column, string label, string value, string glyph) =>
        AddHudCell(grid, column, label, Heading(value, 20), null, glyph);

    private static void AddHudCell(Grid grid, int column, string label, UIElement value, ProgressBar? progress, string glyph)
    {
        var copy = new StackPanel { Spacing = 3 };
        var labelRow = new Grid { ColumnSpacing = 6 };
        labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        labelRow.ColumnDefinitions.Add(new ColumnDefinition());
        labelRow.Children.Add(new FontIcon { Glyph = glyph, FontSize = Font(13) });
        var labelText = new TextBlock { Text = label, FontSize = ReadingSize(18), TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(labelText, 1);
        labelRow.Children.Add(labelText);
        copy.Children.Add(labelRow);
        copy.Children.Add(value);
        if (progress is not null) copy.Children.Add(progress);
        var card = Card(copy, 9);
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
    }

    private UIElement GameHero(GameDefinition game, bool showBest)
    {
        var palette = PaletteFor(game);
        var grid = new Grid { MinHeight = 170 };
        var content = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(28) };
        content.Children.Add(new FontIcon { Glyph = game.Glyph, FontSize = Font(44), Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), HorizontalAlignment = HorizontalAlignment.Left });
        content.Children.Add(new TextBlock { Text = game.Title, FontSize = Font(32), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), TextWrapping = TextWrapping.Wrap });
        if (showBest) content.Children.Add(new TextBlock
        {
            Text = GameBestLabel(game),
            FontSize = ReadingSize(18),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
        });
        grid.Children.Add(content);
        return new Border { Child = grid, CornerRadius = new CornerRadius(8), Background = Gradient(palette) };
    }

    // Visual always stays LEFT; never stacks above the interactive content.
    // `visual` defaults to the game's glyph
    // so every game gets a consistent left-side anchor even without a bespoke one.
    private void AddGameScene(GameDefinition game, UIElement content, UIElement? visual = null)
    {
        var layout = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        var interactiveBoard = visual is not null && game.Id is "matrix" or "wordguess" or "dailychallenge" or "crossword";
        var visualHost = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = interactiveBoard ? "interactive-board" : null,
        };
        if (interactiveBoard)
        {
            visualHost.MinWidth = Math.Max(308, (visual as FrameworkElement)?.MinWidth ?? 0);
            visualHost.Children.Add(visual!);
            layout.MinWidth = visualHost.MinWidth + 300;
        }
        else if (visual is StackPanel) visualHost.Children.Add(visual);
        else visualHost.Children.Add(new Viewbox { Child = visual ?? DefaultGameVisual(game), Stretch = Stretch.Uniform, MaxHeight = 300, HorizontalAlignment = HorizontalAlignment.Stretch });
        layout.Children.Add(visualHost);
        var right = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(content);
        var feedback = GameSceneContent();
        right.Children.Add(feedback);
        if (_activeGame is not null)
        {
            _gameRoundView = (FrameworkElement)content;
            _gameFeedback = feedback;
            layout.KeyDown += GameSceneKeyDown;
        }
        var contentCard = new Border
        {
            Child = right,
            Background = new SolidColorBrush(Color.FromArgb(255, 15, 23, 42)),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetAutomationId(contentCard, "game.ContentCard");
        layout.Children.Add(contentCard);
        ConfigureGameLayout(layout, visualHost, contentCard);

        var grid = new Grid { MinHeight = 280 };
        grid.Children.Add(layout);
        var arena = new Border
        {
            Child = grid,
            CornerRadius = new CornerRadius(18),
            Background = Gradient(PaletteFor(game)),
            Padding = new Thickness(8),
        };
        PageContent.Children.Add(arena);
        AnimateEntrance(arena);
    }

    private static UIElement DefaultGameVisual(GameDefinition game) => new Border
    {
        Width = 140,
        Height = 140,
        CornerRadius = new CornerRadius(70),
        Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
        Child = new FontIcon { Glyph = game.Glyph, FontSize = Font(56), Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) },
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    // Visual (figure/avatar/decoration) always sits to the LEFT of the interactive
    // content -- never above it -- so neither pushes the other below the viewport.
    // `visual` defaults to the game's glyph so every game gets a consistent left-side
    // anchor even without a bespoke one.
    private static void ConfigureGameLayout(Grid grid, FrameworkElement visual, FrameworkElement content)
    {
        var lastWidth = -1.0;

        void Reflow()
        {
            var width = grid.ActualWidth;
            var visualWidth = visual.Tag as string == "interactive-board" ? Math.Max(308, visual.MinWidth)
                : width <= 0 ? 200 : width < 760 ? 120 : Math.Clamp(width * 0.23, 120, 220);
            if (Math.Abs(visualWidth - lastWidth) < 0.5) return;
            lastWidth = visualWidth;
            grid.ColumnDefinitions.Clear();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(visualWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(visual, 0);
            Grid.SetRow(visual, 0);
            Grid.SetColumn(content, 1);
            Grid.SetRow(content, 0);
        }

        grid.Loaded += (_, _) => Reflow();
        grid.SizeChanged += (_, _) => Reflow();
        Reflow();
    }

    private static StackPanel GameSceneContent() => new()
    {
        Spacing = 12,
        MaxWidth = 760,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private TextBlock AddGameQuestion(Panel panel, string text)
    {
        var prompt = new TextBlock
        {
            Text = text,
            FontSize = Font(26),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 15, 23, 42)),
            TextAlignment = TextAlignment.Left,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
            TextLineBounds = TextLineBounds.Full,
            LineHeight = Font(36),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsTextSelectionEnabled = true,
        };
        AutomationProperties.SetAutomationId(prompt, "game.Question");
        AutomationProperties.SetName(prompt, text);
        AutomationProperties.SetHelpText(prompt, U("Kids.Accessibility.ReadQuestion", "Read the question, then use the control below.", "Soruyu oku, sonra aşağıdaki kontrolü kullan."));
        AutomationProperties.SetHeadingLevel(prompt, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
        Live(prompt);
        var copy = new StackPanel { Spacing = 5 };
        copy.Children.Add(new TextBlock
        {
            Text = U("Games.Question", "Question", "Soru"),
            FontSize = ReadingSize(18),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 51, 65, 85)),
        });
        copy.Children.Add(prompt);
        var card = new Border
        {
            Child = copy,
            Background = new SolidColorBrush(Color.FromArgb(255, 248, 250, 252)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 12, 16, 14),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetAutomationId(card, "game.QuestionCard");
        panel.Children.Add(card);
        return prompt;
    }

    private static TextBlock GamePrompt(string text, double size) => new()
    {
        Text = text,
        FontSize = ReadingSize(size),
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        TextLineBounds = TextLineBounds.Full,
        LineHeight = ReadingSize(size) * 1.4,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private static TextBlock GameCaption(string text) => new()
    {
        Text = text,
        FontSize = ReadingSize(20),
        FontWeight = Microsoft.UI.Text.FontWeights.Normal,
        Foreground = new SolidColorBrush(Color.FromArgb(255, 241, 245, 249)),
        LineHeight = ReadingSize(20) * 1.4,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private Button GameChoiceButton(string text, GameDefinition game, bool isAction = false)
    {
        var button = new Button
        {
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            MinHeight = 56,
            MinWidth = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(16, 12, 16, 12),
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 100, 116, 139)),
            BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            FontSize = ReadingSize(20),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            IsTabStop = true,
            UseSystemFocusVisuals = true,
        };
        button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(255, 51, 65, 85));
        button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Color.FromArgb(255, 71, 85, 105));
        button.Resources["ButtonForegroundPointerOver"] = button.Foreground;
        button.Resources["ButtonForegroundPressed"] = button.Foreground;
        button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Microsoft.UI.Colors.White);
        button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Microsoft.UI.Colors.White);
        AutomationProperties.SetName(button, text);
        if (!isAction) AutomationProperties.SetHelpText(button, GameTask(game));
        return button;
    }

    private static void SetGameChoiceMetadata(Button button, string automationId, string? accelerator = null)
    {
        AutomationProperties.SetAutomationId(button, automationId);
        if (!string.IsNullOrWhiteSpace(accelerator))
        {
            AutomationProperties.SetAcceleratorKey(button, accelerator);
            ToolTipService.SetToolTip(button, accelerator);
        }
    }

    private Button GameActionButton(string text, string glyph, GameDefinition game)
    {
        var button = GameChoiceButton(text, game, isAction: true);
        button.Content = ButtonContent(text, glyph);
        button.Background = AppearancePalette.Current.ButtonBrush;
        button.Foreground = AppearancePalette.Current.ButtonForegroundBrush;
        button.BorderBrush = AppearancePalette.Current.ButtonBorderBrush;
        button.Resources["ButtonBackgroundPointerOver"] = button.Background;
        button.Resources["ButtonBackgroundPressed"] = button.Background;
        button.Resources["ButtonForegroundPointerOver"] = button.Foreground;
        button.Resources["ButtonForegroundPressed"] = button.Foreground;
        button.HorizontalAlignment = HorizontalAlignment.Center;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.MaxWidth = 320;
        return button;
    }

    private void StartGameTimer(GameSession session)
    {
        _gameTimer?.Stop();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _gameTimer = timer;
        timer.Tick += async (_, _) =>
        {
            if (_activeGame != session || !ReferenceEquals(_gameTimer, timer)) { timer.Stop(); return; }
            if (!_gameTurn.CanTick(_gameEpoch, !GameCanAnswer(session) || _gameLocalBusy)) return;
            if (App.MainWindow is YDKE_Windows.MainWindow { IsWindowMinimized: true }) return;
            session.SecondsRemaining = Math.Max(0, session.SecondsRemaining - 1);
            if (_gameTimerText is not null) _gameTimerText.Text = session.SecondsRemaining.ToString();
            if (_gameTimerProgress is not null) _gameTimerProgress.Value = session.SecondsRemaining;
            if (_gamePrimaryProgress is not null) _gamePrimaryProgress.Value = GameEngine.Progress(session, _gameMode).Value;
            if (session.SecondsRemaining <= 0) await CompleteGameAsync(session);
        };
        timer.Start();
    }

    private void StopGameTimer()
    {
        _gameTimer?.Stop();
        _gameTimer = null;
        _gameTimerText = null;
        _gameTimerProgress = null;
        _gamePrimaryProgress = null;
    }

    private void AnimateEntrance(UIElement element)
    {
        if (GameMotionOff) return;
        element.Opacity = 0;
        var transform = new TranslateTransform { Y = 18 };
        element.RenderTransform = transform;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var storyboard = new Storyboard();
        var fade = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(320), EasingFunction = easing };
        Storyboard.SetTarget(fade, element);
        Storyboard.SetTargetProperty(fade, "Opacity");
        var slide = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(380), EasingFunction = easing };
        Storyboard.SetTarget(slide, transform);
        Storyboard.SetTargetProperty(slide, "Y");
        storyboard.Children.Add(fade);
        storyboard.Children.Add(slide);
        storyboard.Begin();
    }

    private void AnimatePulse(FrameworkElement element, bool correct)
    {
        if (GameMotionOff) return;
        var transform = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
        element.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        element.RenderTransform = transform;
        if (element is Control control)
        {
            control.Background = new SolidColorBrush(correct
                ? Color.FromArgb(255, 4, 120, 87)
                : Color.FromArgb(255, 190, 18, 60));
            control.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        }
        var storyboard = new Storyboard();
        foreach (var property in new[] { "ScaleX", "ScaleY" })
        {
            var pulse = new DoubleAnimation
            {
                To = 1.05,
                Duration = TimeSpan.FromMilliseconds(150),
                AutoReverse = true,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(pulse, transform);
            Storyboard.SetTargetProperty(pulse, property);
            storyboard.Children.Add(pulse);
        }
        storyboard.Begin();
    }

    private static LinearGradientBrush Gradient(GamePalette palette) => new()
    {
        StartPoint = new Windows.Foundation.Point(0, 0),
        EndPoint = new Windows.Foundation.Point(1, 1),
        GradientStops =
        {
            new GradientStop { Color = palette.Start, Offset = 0 },
            new GradientStop { Color = palette.End, Offset = 1 },
        },
    };

    private static GamePalette PaletteFor(GameDefinition game)
    {
        var palettes = new[]
        {
            new GamePalette(Color.FromArgb(255, 7, 89, 133), Color.FromArgb(255, 30, 58, 138), Color.FromArgb(255, 3, 105, 161)),
            new GamePalette(Color.FromArgb(255, 6, 95, 70), Color.FromArgb(255, 19, 78, 74), Color.FromArgb(255, 4, 120, 87)),
            new GamePalette(Color.FromArgb(255, 159, 18, 57), Color.FromArgb(255, 154, 52, 18), Color.FromArgb(255, 190, 18, 60)),
            new GamePalette(Color.FromArgb(255, 55, 48, 163), Color.FromArgb(255, 21, 94, 117), Color.FromArgb(255, 29, 78, 216)),
            new GamePalette(Color.FromArgb(255, 120, 53, 15), Color.FromArgb(255, 124, 45, 18), Color.FromArgb(255, 146, 64, 14)),
        };
        var index = Math.Abs(game.Id.Aggregate(0, (value, character) => value + character)) % palettes.Length;
        return palettes[index];
    }

    private sealed record GamePalette(Color Start, Color End, Color Accent);

    private void RenderStats()
    {
        AddLearningStats();
    }

    private void RenderProfile()
    {
        RenderSettingsPage(MarkedKnownLabel);
    }

    private void NavigateTo(string page, NavigationViewItem item)
    {
        Navigation.SelectedItem = item;
    }

    private void AddPageHeader(string title, string subtitle)
    {
        var header = new Grid { ColumnSpacing = 20, RowSpacing = 2, Margin = new Thickness(0, 2, 0, 6) };
        var heading = new TextBlock
        {
            Text = title,
            FontSize = Font(28),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
        };
        AutomationProperties.SetHeadingLevel(heading, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level1);
        AutomationProperties.SetAutomationId(heading, "page.Title");
        header.Children.Add(heading);
        var hint = new TextBlock
        {
            Text = subtitle,
            FontSize = ReadingSize(18),
            Foreground = AppearancePalette.Current.BackgroundForegroundBrush,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetAutomationId(hint, "page.Subtitle");
        header.Children.Add(hint);
        ConfigureResponsiveGrid(header, 2, 340);
        PageContent.Children.Add(header);
    }

    private static Border Card(UIElement child, double padding)
    {
        ApplyReadableForeground(child, AppearancePalette.Current.BoxForegroundBrush);
        return new Border
        {
            Child = child,
            Padding = new Thickness(padding),
            CornerRadius = new CornerRadius(20),
            Background = AppearancePalette.Current.BoxBrush,
            BorderBrush = new SolidColorBrush(AppearancePalette.Current.BorderBrush.Color) { Opacity = 0.5 },
            BorderThickness = new Thickness(1),
        };
    }

    private static TextBlock Heading(string text, double size) => new()
    {
        Text = text,
        FontSize = ReadingSize(size),
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = AppearancePalette.Current.BackgroundForegroundBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    private static TextBlock Body(string text) => new()
    {
        Text = text,
        FontSize = ReadingSize(18),
        LineHeight = ReadingSize(18) * 1.4,
        Foreground = AppearancePalette.Current.BackgroundForegroundBrush,
        TextWrapping = TextWrapping.Wrap,
        TextTrimming = TextTrimming.None,
    };

    private static TextBlock SecondaryBody(string text, double size, bool italic = false) => new()
    {
        Text = text,
        FontSize = Font(size),
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        FontStyle = italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
        Foreground = new SolidColorBrush(AppearancePalette.Current.BackgroundForegroundBrush.Color)
        {
            Opacity = 0.92,
        },
        TextWrapping = TextWrapping.Wrap,
    };

    private static Border RevealCallout(string title, string text, double size, bool compact = false, bool italic = false)
    {
        var body = SecondaryBody(text, size, italic);
        body.TextAlignment = TextAlignment.Center;
        body.Margin = new Thickness(0, 1, 0, 0);
        body.Foreground = new SolidColorBrush(AppearancePalette.Current.BackgroundForegroundBrush.Color)
        {
            Opacity = 0.88,
        };

        var label = new TextBlock
        {
            Text = title,
            FontSize = Font(compact ? 10 : 11),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(AppearancePalette.Current.ButtonTintForegroundBrush.Color)
            {
                Opacity = 0.78,
            },
            TextAlignment = TextAlignment.Center,
        };

        var borderBrush = new SolidColorBrush(AppearancePalette.Current.ButtonBorderBrush.Color)
        {
            Opacity = 0.35,
        };

        return new Border
        {
            Padding = new Thickness(compact ? 12 : 16, compact ? 10 : 12, compact ? 12 : 16, compact ? 12 : 14),
            Margin = new Thickness(0, compact ? 2 : 4, 0, 0),
            CornerRadius = new CornerRadius(14),
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(AppearancePalette.Current.ButtonTintBrush.Color)
            {
                Opacity = 0.08,
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = new StackPanel
            {
                Spacing = compact ? 3 : 5,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Children = { label, body },
            },
        };
    }

    private static TextBlock QuickSettingLabel(string text) => new()
    {
        Text = text,
        FontSize = ReadingSize(18),
        Foreground = AppearancePalette.Current.BoxForegroundBrush,
    };

    private static Button AccentButton(string text, string glyph)
    {
        var button = new Button
        {
            Content = ButtonContent(text, glyph),
            MinHeight = 48,
            MinWidth = 48,
            Padding = new Thickness(18, 12, 18, 12),
            CornerRadius = new CornerRadius(20),
            Background = AppearancePalette.Current.ButtonBrush,
            BorderThickness = new Thickness(0),
            Foreground = AppearancePalette.Current.ButtonForegroundBrush,
            FontSize = ReadingSize(18),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            IsTabStop = true,
            UseSystemFocusVisuals = true,
        };
        AutomationProperties.SetName(button, text);
        return button;
    }

    private static Button SecondaryButton(string text, string glyph)
    {
        var button = new Button
        {
            Content = ButtonContent(text, glyph),
            MinHeight = 48,
            MinWidth = 48,
            Padding = new Thickness(16, 12, 16, 12),
            CornerRadius = new CornerRadius(20),
            Background = AppearancePalette.Current.ButtonTintBrush,
            BorderThickness = new Thickness(0),
            Foreground = AppearancePalette.Current.ButtonTintForegroundBrush,
            FontSize = ReadingSize(18),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            IsTabStop = true,
            UseSystemFocusVisuals = true,
        };
        AutomationProperties.SetName(button, text);
        return button;
    }

    private static Button ChoiceButton(string text) => new()
    {
        Content = text,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Left,
        Padding = new Thickness(18, 14, 18, 14),
        CornerRadius = new CornerRadius(20),
        Background = AppearancePalette.Current.ButtonBrush,
        BorderThickness = new Thickness(0),
        Foreground = AppearancePalette.Current.ButtonForegroundBrush,
        FontSize = Font(15),
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        IsTabStop = true,
        UseSystemFocusVisuals = true,
    };

    private static Grid ButtonContent(string text, string glyph)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new FontIcon { Glyph = glyph, FontSize = Font(16), VerticalAlignment = VerticalAlignment.Center });
        var label = new TextBlock
        {
            Text = text,
            FontSize = ReadingSize(18),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);
        return grid;
    }

    private static UIElement SettingHeader(string glyph, string title, string hint)
    {
        var grid = new Grid { ColumnSpacing = 14 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = string.IsNullOrEmpty(glyph) ? new GridLength(0) : new GridLength(36) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        if (!string.IsNullOrEmpty(glyph)) grid.Children.Add(new FontIcon { Glyph = glyph, FontSize = Font(20), VerticalAlignment = VerticalAlignment.Top });
        else grid.ColumnSpacing = 0;
        var copy = new StackPanel { Spacing = 3 };
        copy.Children.Add(Heading(title, 17));
        copy.Children.Add(Body(hint));
        Grid.SetColumn(copy, 1);
        grid.Children.Add(copy);
        return grid;
    }

    private static UIElement ChipRow(VocabularyEntry entry)
    {
        var row = new Grid { ColumnSpacing = 8, RowSpacing = 8, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var value in new[] { entry.LanguageCode.ToUpperInvariant(), entry.Level, entry.PartOfSpeech })
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            row.Children.Add(new Border
            {
                Child = new TextBlock
                {
                    Text = value,
                    FontSize = Font(12),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = AppearancePalette.Current.ButtonTintForegroundBrush,
                },
                Padding = new Thickness(10, 5, 10, 5),
                CornerRadius = new CornerRadius(14),
                Background = AppearancePalette.Current.ButtonTintBrush,
            });
        }
        ConfigureResponsiveGrid(row, 3, 90);
        return row;
    }

    private static (UIElement Row, ColorPicker Picker) AppearanceColorSetting(
        string title,
        string hint,
        Color color)
    {
        var picker = new ColorPicker
        {
            Color = color,
            IsAlphaEnabled = false,
            MinWidth = 300,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var swatch = new Button
        {
            Width = 52,
            Height = 48,
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(color),
            BorderBrush = AppearancePalette.Current.BorderBrush,
            BorderThickness = new Thickness(2),
            Flyout = new Flyout { Content = picker },
        };
        AutomationProperties.SetName(swatch, title);
        ToolTipService.SetToolTip(swatch, title);
        picker.ColorChanged += (_, args) => swatch.Background = new SolidColorBrush(args.NewColor);

        var row = new Grid { ColumnSpacing = 16 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(SettingHeader("", title, hint));
        Grid.SetColumn(swatch, 1);
        row.Children.Add(swatch);
        return (row, picker);
    }

    private static void ConfigureResponsiveGrid(
        Grid grid,
        int maximumColumns,
        double minimumColumnWidth,
        GridLength? rowHeight = null)
    {
        var currentColumns = 0;

        void Reflow()
        {
            var width = grid.ActualWidth;
            var columns = width <= 0
                ? maximumColumns
                : Math.Clamp(
                    (int)Math.Floor((width + grid.ColumnSpacing) / (minimumColumnWidth + grid.ColumnSpacing)),
                    1,
                    maximumColumns);
            var rows = (grid.Children.Count + columns - 1) / columns;
            if (columns == currentColumns && grid.RowDefinitions.Count == rows) return;

            currentColumns = columns;
            grid.ColumnDefinitions.Clear();
            grid.RowDefinitions.Clear();
            for (var column = 0; column < columns; column++)
                grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (var row = 0; row < rows; row++)
                grid.RowDefinitions.Add(new RowDefinition { Height = rowHeight ?? GridLength.Auto });

            for (var index = 0; index < grid.Children.Count; index++)
            {
                if (grid.Children[index] is not FrameworkElement child) continue;
                Grid.SetColumn(child, index % columns);
                Grid.SetRow(child, index / columns);
            }
        }

        grid.Loaded += (_, _) => Reflow();
        grid.SizeChanged += (_, _) => Reflow();
        Reflow();
    }

    private static void AddStat(Grid grid, int column, string label, string value, string glyph)
    {
        var content = new StackPanel { Spacing = 5 };
        content.Children.Add(new FontIcon { Glyph = glyph, FontSize = Font(18), HorizontalAlignment = HorizontalAlignment.Left });
        content.Children.Add(Heading(value, 24));
        content.Children.Add(Body(label));
        var card = Card(content, 14);
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
    }

    private string GameDescription(GameDefinition game) => ExperienceStrings.GameDescription(_settings.UiLanguage, game);

    private string LocalizedPart(string value)
    {
        var separator = value.LastIndexOf(" - ", StringComparison.Ordinal);
        if (separator < 0) return value;
        return _settings.UiLanguage == "tr" ? value[(separator + 3)..] : value[..separator];
    }

    private StudyLanguage CurrentStudyLanguage() =>
        VocabularyRepository.Languages.First(language => language.Code == _settings.StudyLanguage);

    private static void Toggle(HashSet<string> values, string value) { if (!values.Add(value)) values.Remove(value); }

    private static double Font(double size) => Math.Round(size * AppearancePalette.Current.FontScale, 1);

    private static double ReadingSize(double size) => Math.Max(18, Font(size));

    private static void ApplyReadableForeground(UIElement element, Brush foreground)
    {
        if (element is Button) return;
        if (element is TextBlock text) text.Foreground = foreground;
        else if (element is FontIcon icon) icon.Foreground = foreground;
        else if (element is Control control) control.Foreground = foreground;

        if (element is Panel panel)
            foreach (var child in panel.Children) ApplyReadableForeground(child, foreground);
        else if (element is Border { Child: UIElement child })
            ApplyReadableForeground(child, foreground);
    }

    private void SetLoading(bool loading)
    {
        LoadingRing.IsActive = loading;
        LoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        ContentScroll.IsEnabled = !loading && !_studyBusy;
        PageContent.IsHitTestVisible = !loading && !_studyBusy;
        QuickUiLanguage.IsEnabled = QuickStudyLanguage.IsEnabled = QuickLevel.IsEnabled = !loading && !_studyBusy;
        SyncAccountStatus();
    }

    private void ShowNotice(string title, string message, InfoBarSeverity severity)
    {
        Notice.Title = title;
        var detailed = message.Length > 240 || message.Contains('\n');
        Notice.Message = detailed ? "" : message;
        Notice.Content = !detailed ? null : StudyPopupButton(
            U("Shell.ShowDetails", "Show details", "Ayrıntıları göster"),
            PagedTextContent(message, "notice.Details"), "notice.ShowDetails");
        Notice.Severity = severity;
        AutomationProperties.SetName(Notice, $"{title}. {message}");
        Notice.IsOpen = true;
    }
}
