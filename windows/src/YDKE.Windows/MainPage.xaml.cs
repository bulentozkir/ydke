using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
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

        if (!Localizer.UiLanguages.Any(language => language.Code == _settings.UiLanguage))
            _settings.UiLanguage = UserSettings.DefaultUiLanguage;
        if (!VocabularyRepository.Languages.Any(language => language.Code == _settings.StudyLanguage))
            _settings.StudyLanguage = "en";

        ApplyAppearance();
        _ = GameCatalog.All;
        ApplyNavigationLanguage();
        QuickStudyLanguage.ItemsSource = VocabularyRepository.Languages;
        ConfigureResponsiveGrid(QuickSettingsGrid, 2, 170);
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
        RefreshQuizExamLayout();
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
        try
        {
            if (!await ConfirmLeaveGameAsync()) { RestoreNavigationSelection(); return; }
            if (Navigation.DisplayMode != NavigationViewDisplayMode.Expanded) Navigation.IsPaneOpen = false;
            _cardUndo = null;
            _revealedCardKey = null;
            ResetQuizSelection();
            _currentPage = tag;
            if (!_storageBlocked) Notice.IsOpen = false;
            await EnsureWordsAsync();
            RenderCurrentPage();
        }
        catch (Exception ex) { ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error); RestoreNavigationSelection(); }
        finally { _navigationBusy = false; }
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
        ApplyQuickSettingsVisuals();
        if (App.MainWindow is YDKE_Windows.MainWindow window)
            window.ApplyAppearance(AppearancePalette.Current);
    }

    private async Task EnsureWordsAsync()
    {
        if (_words.Count == 0) await ReloadWordsAsync();
    }

    private async Task ReloadWordsAsync()
    {
        ResetQuizSelection();
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
        var longAboutPage = _currentPage is "about" or "help";
        ContentScroll.VerticalScrollMode = longAboutPage ? ScrollMode.Auto : ScrollMode.Disabled;
        ContentScroll.VerticalScrollBarVisibility = longAboutPage ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
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
        QuickStudyLanguage.SelectedValue = _settings.StudyLanguage;
        QuickLevel.ItemsSource = CurrentStudyLanguage().Levels;
        QuickLevel.SelectedItem = _settings.Level;
        QuickStudyLanguage.Header = QuickSettingLabel(T("Profile.StudyLanguage"));
        QuickLevel.Header = QuickSettingLabel(T("Profile.Level"));
        foreach (var control in new[] { QuickStudyLanguage, QuickLevel })
        {
            ConfigureReadingComboBox(control);
        }
        AutomationProperties.SetName(QuickStudyLanguage, T("Profile.StudyLanguage"));
        AutomationProperties.SetName(QuickLevel, T("Profile.Level"));
        ApplyQuickSettingsVisuals();
        _syncingSettings = false;
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
            var voice = StudyVoiceFor(_settings.StudyLanguage);
            if (voice is null)
            {
                ShowNotice(T("Common.Error"), T("Games.Require.Voice"), InfoBarSeverity.Warning);
                return;
            }
            _speechSynthesizer ??= new SpeechSynthesizer();
            _speechSynthesizer.Voice = voice;

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

    private static string[] SpeechLanguagePreferences(string languageCode) => languageCode switch
    {
        "de" => ["de-DE", "de-AT", "de-CH"],
        "fr" => ["fr-FR", "fr-CA", "fr-BE", "fr-CH"],
        "it" => ["it-IT", "it-CH"],
        "es" => ["es-ES", "es-MX", "es-US", "es-AR", "es-CO", "es-CL"],
        "pt" => ["pt-PT", "pt-BR"],
        "nl" => ["nl-NL", "nl-BE"],
        _ => ["en-US", "en-GB", "en-AU", "en-CA", "en-IN"],
    };

    private static VoiceInformation? StudyVoiceFor(string languageCode)
    {
        var preferred = SpeechLanguagePreferences(languageCode);
        var primaryPrefix = SpeechLanguage(languageCode)[..2] + "-";
        var ranked = SpeechSynthesizer.AllVoices
            .Select(voice => (Voice: voice, Rank: SpeechVoiceRank(voice, preferred, primaryPrefix)))
            .Where(item => item.Rank < int.MaxValue)
            .OrderBy(item => item.Rank)
            .ThenBy(item => item.Voice.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => item.Voice)
            .FirstOrDefault();
        return ranked;
    }

    private static int SpeechVoiceRank(VoiceInformation voice, IReadOnlyList<string> preferred, string primaryPrefix)
    {
        for (var index = 0; index < preferred.Count; index++)
            if (voice.Language.Equals(preferred[index], StringComparison.OrdinalIgnoreCase)) return index;
        return voice.Language.StartsWith(primaryPrefix, StringComparison.OrdinalIgnoreCase)
            ? preferred.Count + 10
            : int.MaxValue;
    }

    private void RenderGames(GameGroup group)
    {
        StopGameTimer();
        StopSpeechPlayback();
        _activeGame = null;
        PageContent.Children.Clear();
        AddPageHeader(T(group == GameGroup.Simple ? "Games.SimpleTitle" : "Games.ComplexTitle"),
            T(group == GameGroup.Simple ? "Games.SimpleSubtitle" : "Games.ComplexSubtitle"));
        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        var reflowCatalog = ConfigureResponsiveGrid(grid, 4, 180);
        AddGameFilters(group, grid, reflowCatalog);
        PageContent.Children.Add(grid);
    }

    private int TimedSecondsSetting => Math.Clamp(_settings.TimerSeconds, UserSettings.MinTimerSeconds, UserSettings.MaxTimerSeconds);

    private string TimedSecondsLabel(int seconds) => string.Format(CultureInfo.CurrentCulture,
        U("Games.Timer.Seconds", "{0}s", "{0} sn"), seconds);

    private Border? GameTimerBadge(GameDefinition game, double size = 14)
    {
        if (!GameEngine.HasClock(game)) return null;
        var background = Color.FromArgb(255, 194, 65, 12);
        var foreground = AppearancePalette.EnsureTextContrast(background, Microsoft.UI.Colors.White);
        var border = AppearancePalette.EnsureBoundaryContrast(background, Color.FromArgb(255, 120, 53, 15));
        var text = new TextBlock
        {
            Text = string.Format(CultureInfo.CurrentCulture,
                U("Games.Timer.Badge", "Timer {0}", "Süre {0}"), TimedSecondsLabel(TimedSecondsSetting)),
            FontSize = ReadingSize(size),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.NoWrap,
            Foreground = new SolidColorBrush(foreground),
        };
        return new Border
        {
            Background = new SolidColorBrush(background),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = text,
            HighContrastAdjustment = ElementHighContrastAdjustment.Auto,
        };
    }

    private Button GameButton(GameDefinition game)
    {
        var palette = PaletteFor(game);
        var iconForeground = AppearancePalette.EnsureTextContrast(palette.Accent, Microsoft.UI.Colors.White);
        var layout = new Grid { ColumnSpacing = 16 };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(new Border
        {
            Width = 46,
            Height = 46,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(palette.Accent),
            Child = DecorativeIcon(game.Glyph, Font(23), new SolidColorBrush(iconForeground)),
        });
        var title = Heading(game.Title, 18);
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        titleRow.Children.Add(title);
        if (GameTimerBadge(game) is { } timedBadge) titleRow.Children.Add(timedBadge);
        Grid.SetColumn(titleRow, 1);
        layout.Children.Add(titleRow);
        var best = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        best.Children.Add(new TextBlock { Text = T("Game.Best"), FontSize = ReadingSize(18), TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Right });
        best.Children.Add(Heading(GameBest(game).ToString("N0"), 18));
        Grid.SetColumn(best, 2);
        layout.Children.Add(best);
        var button = new Button
        {
            Content = layout,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10),
            MinHeight = 84,
            CornerRadius = new CornerRadius(16),
            Background = AppearancePalette.Current.BoxBrush,
            BorderBrush = AppearancePalette.Current.BorderBrush,
            BorderThickness = new Thickness(1),
            Foreground = AppearancePalette.Current.BoxForegroundBrush,
        };
        ApplyAccessibleButtonVisuals(button, AppearancePalette.Current.Box,
            AppearancePalette.Current.BoxForeground, AppearancePalette.Current.Border);
        ApplyReadableForeground(layout, AppearancePalette.Current.BoxForegroundBrush);
        AutomationProperties.SetName(button, game.Title);
        AutomationProperties.SetAutomationId(button, "games.Item." + game.Id);
        AutomationProperties.SetHelpText(button, GameTask(game));
        ToolTipService.SetToolTip(button, GameTask(game));
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
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        titleRow.Children.Add(Heading(game.Title, 23));
        if (GameTimerBadge(game, 13) is { } timedBadge) titleRow.Children.Add(timedBadge);
        title.Children.Add(titleRow);
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
            RestoreDialogFocus(focus, page, context);
            closed.TrySetResult(true);
        }
    }

    private string GameTask(GameDefinition game) => game.Id switch
    {
        "hangman" => U("Games.Task.Hangman", "Choose letters to uncover the word.", "Kelimeyi bulmak için harfleri seçin."),
        "scramble" => U("Games.Task.Scramble", "Arrange the letters to match the meaning.", "Anlama uyan kelimeyi oluşturmak için harfleri sıralayın."),
        "dictation" => U("Games.Task.Dictation", "Listen, then choose the word you hear.", "Dinleyin, ardından duyduğunuz kelimeyi seçin."),
        "listeningchoice" => U("Games.Task.Listening", "Listen, then choose the meaning.", "Dinleyin, ardından anlamını seçin."),
        "speedround" or "survival" => U("Games.Task.Choice", "Choose the word that matches the meaning.", "Anlama uyan kelimeyi seçin."),
        "truefalse" => U("Games.Task.TrueFalse", "Does this word match the definition?", "Bu kelime tanıma uyuyor mu?"),
        "wordclass" => U("Games.Task.Class", "Choose the word's part of speech.", "Kelimenin sözcük türünü seçin."),
        "memory" => U("Games.Task.Memory", "Match each word with its meaning.", "Her kelimeyi anlamıyla eşleştirin."),
        "oddoneout" => U("Games.Task.Odd", "Choose the word from a different category.", "Farklı kategorideki kelimeyi seçin."),
        "wordrace" => U("Games.Task.Typing", "Choose the word that matches the meaning.", "Anlama uyan kelimeyi seçin."),
        "clozetest" => U("Games.Task.Cloze", "Choose the missing word from four options.", "Eksik kelimeyi dört seçenekten seçin."),
        "sentencescramble" => U("Games.Task.Sentence", "Put all the tiles in sentence order.", "Tüm parçaları cümle sırasına dizin."),
        "readingcomprehension" => U("Games.Task.Reading", "Read the passage, then answer the question.", "Metni okuyun, ardından soruyu yanıtlayın."),
        "wordmorph" => U("Games.Task.Semantic", "Are these words synonyms or antonyms?", "Bu kelimeler eş anlamlı mı, zıt anlamlı mı?"),
        "bingo" => U("Games.Task.Bingo", "Find this definition on the 4×4 board.", "Bu tanıma uyan kelimeyi 4×4 tahtada bulun."),
        "bossrush" => U("Games.Task.Boss", "Choose the meaning of the displayed word.", "Gösterilen kelimenin anlamını seçin."),
        "codycross" => U("Games.Task.Cody", "Choose each clue's word to reveal the bonus code.", "Bonus kodu açmak için her ipucunun kelimesini seçin."),
        "crossword" => U("Games.Task.Crossword", "Choose Across first, then Down.", "Önce yatayı, ardından dikeyi seçin."),
        "dailychallenge" or "wordguess" => U("Games.Task.Guess", "Choose a guess using the letter clues.", "Harf ipuçlarını kullanarak bir tahmin seçin."),
        "scrabble" => U("Games.Task.Rack", "Build a vocabulary word by selecting these letters.", "Bu harfleri seçerek bir kelime oluşturun."),
        "categorysprint" => U("Games.Task.Category", "Choose a new word in this category.", "Bu kategoriden yeni bir kelime seçin."),
        "cluedetective" => U("Games.Task.Clues", "Reveal clues, then choose the hidden word.", "İpuçlarını açın, ardından gizli kelimeyi seçin."),
        "matrix" => U("Games.Task.Matrix", "Select the hidden word in a straight line.", "Gizli kelimeyi düz bir çizgide seçin."),
        _ => GameDescription(game),
    };

    private string GameChildInstructions(GameDefinition game) => game.Id switch
    {
        "hangman" => U("Kids.How.Hangman", "Read the meaning. Pick a letter. Find the word before six misses.", "Anlamı oku. Bir harf seç. Altı hatadan önce kelimeyi bul."),
        "scramble" => U("Kids.How.Scramble", "Read the meaning. Pick the letters in order. Click a filled box to change it.", "Anlamı oku. Harfleri sırayla seç. Değiştirmek için dolu bir kutuya tıkla."),
        "dictation" or "listeningchoice" => U("Kids.How.Listen", "Click Listen. Listen again if you need to. Then choose the word or its meaning.", "Dinle'ye tıkla. Gerekirse bir daha dinle. Sonra kelimeyi veya anlamını seç."),
        "speedround" or "survival" => U("Kids.How.Choice", "Read the meaning. Click the matching word. Look at the answer, then choose Next.", "Anlamı oku. Uyan kelimeye tıkla. Yanıta bak, sonra Sonraki'ni seç."),
        "truefalse" => U("Kids.How.TrueFalse", "Read the word and meaning. Choose True if they match. Choose False if they do not.", "Kelimeyi ve anlamı oku. Uyuyorsa Doğru'yu, uymuyorsa Yanlış'ı seç."),
        "wordclass" => U("Kids.How.Class", "A noun names something. A verb tells an action. An adjective describes a thing. An adverb tells how.", "İsim bir şeyin adıdır. Fiil bir işi anlatır. Sıfat bir şeyi tanımlar. Zarf nasıl olduğunu anlatır."),
        "memory" => U("Kids.How.Memory", "Open two cards. Find a word and its meaning. Match all six pairs.", "İki kart aç. Bir kelimeyi anlamıyla eşleştir. Altı çifti de bul."),
        "oddoneout" => U("Kids.How.Odd", "Three words belong together. Click the word that does not belong.", "Üç kelime aynı grupta. Bu gruba uymayan kelimeye tıkla."),
        "wordrace" => U("Kids.How.Type", "Read the clue. Tap the matching word. Then choose Next.", "İpucunu oku. Uyan kelimeye dokun. Sonra Sonraki'ni seç."),
        "clozetest" => U("Kids.How.Cloze", "Read the sentence. Choose the missing word from four options.", "Cümleyi oku. Dört seçenekten eksik kelimeyi seç."),
        "sentencescramble" => U("Kids.How.Sentence", "Click the word tiles to make a sentence. Undo takes the last tile back. Use every tile.", "Cümle kurmak için kelime taşlarına tıkla. Geri al son taşı geri getirir. Her taşı kullan."),
        "readingcomprehension" => U("Kids.How.Reading", "Read the story. Turn the pages with the arrows. Answer the question. You can read the story again.", "Öyküyü oku. Oklarla sayfaları çevir. Soruyu yanıtla. Öyküyü yeniden okuyabilirsin."),
        "wordmorph" => U("Kids.How.Semantic", "Synonyms mean the same or almost the same. Antonyms mean the opposite. Which pair do you see?", "Eş anlamlı kelimeler aynı veya benzer anlamlıdır. Zıt anlamlı kelimeler birbirinin tersidir. Hangi çifti görüyorsun?"),
        "bingo" => U("Kids.How.Bingo", "Read the meaning. Find the word on the board. Make a line across, down, or from corner to corner.", "Anlamı oku. Kelimeyi tahtada bul. Yatay, dikey veya köşeden köşeye bir çizgi yap."),
        "bossrush" => U("Kids.How.Boss", "Choose the meaning of each word. Each right answer takes away one heart from the character. Meet all three!", "Her kelimenin anlamını seç. Doğru yanıt karakterden bir kalp eksiltir. Üçüyle de tanış!"),
        "codycross" => U("Kids.How.Cody", "Read the clue and tap the right word. Each right word opens one bonus letter. Solve all five clues.", "İpucunu oku ve doğru kelimeye dokun. Her doğru kelime bir bonus harf açar. Beş ipucunu da çöz."),
        "crossword" => U("Kids.How.Crossword", "First choose the word that goes across. Then choose the word that goes down. They share one box.", "Önce yatay giden kelimeyi seç. Sonra dikey giden kelimeyi seç. Bir kutuyu birlikte kullanırlar."),
        "wordguess" or "dailychallenge" => U("Kids.How.Guess", "Pick a word each turn. ✓ means right place. ~ means the letter is in another place. × means that letter is not in the word. You have six tries.", "Her tur bir kelime seç. ✓ doğru yer demektir. ~ harf başka bir yerde demektir. × bu harf kelimede yok demektir. Altı denemen var."),
        "scrabble" => U("Kids.How.Rack", "Tap letters to build a word. Each tile can be used once. Undo or clear, then check your word.", "Bir kelime oluşturmak için harflere dokun. Her taşı bir kez kullanabilirsin. Geri al ya da temizle, sonra kelimeni kontrol et."),
        "categorysprint" => U("Kids.How.Category", "Look at the group name and clue. Tap the word that belongs to that group.", "Grup adını ve ipucunu incele. O gruba ait kelimeye dokun."),
        "cluedetective" => U("Kids.How.Clues", "Ask for clues one by one. Then tap the hidden word. Each extra clue costs some points.", "İpuçlarını tek tek aç. Sonra gizli kelimeye dokun. Her ek ipucu biraz puan götürür."),
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
        if (_gameMode == "timed")
        {
            _activeGame.TimedLimitSeconds = TimedSecondsSetting;
            _activeGame.SecondsRemaining = _activeGame.TimedLimitSeconds;
        }
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
        var choices = _words.Where(word => GameAnswer(word.Word) != GameAnswer(entry.Word) &&
                LocalizedPart(word.Definition) != LocalizedPart(entry.Definition))
            .DistinctBy(GameEngine.Bare).OrderBy(_ => _random.Next()).Take(3)
            .Append(entry).OrderBy(_ => _random.Next()).ToArray();
        if (choices.Length < 4) { GameUnavailable(session, EligibleRequirement); return; }
        _gameHintProvider = () => WordHint(entry);
        var panel = GameSceneContent();
        AddGameQuestion(panel, LocalizedPart(entry.Definition), () => WordExample(entry));
        panel.Children.Add(GameCaption(entry.Category.ToUpperInvariant()));
        GameChoices(panel, session, choices.Select(word => word.Word).ToArray(), (index, button) =>
            ResolveGameAnswerAsync(session, choices[index].Key == entry.Key,
                FeedbackWithAnswer(entry.Word, choices[index].Word), button, [entry.Key]));
        AddGameScene(session.Game, panel);
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
        AddGameQuestion(panel, $"{entry.Word}\n{LocalizedPart(shown.Definition)}", () => WordExample(entry));
        var actions = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        foreach (var answer in new[] { true, false })
        {
            var button = GameChoiceButton(answer ? U("Games.True", "True", "Doğru") : U("Games.False", "False", "Yanlış"), session.Game);
            var answerIndex = answer ? 0 : 1;
            button.Tag = $"game-choice-{answerIndex}";
            SetGameChoiceMetadata(button, $"game.Choice.{answerIndex + 1}", (answerIndex + 1).ToString(CultureInfo.InvariantCulture));
            button.Click += async (_, _) =>
            {
                await ResolveGameAnswerAsync(session, answer == isTrue,
                    FeedbackTrueFalse(entry, MeaningWithTurkish(shown.Definition), isTrue, answer), button, [entry.Key]);
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
        AddGameQuestion(panel, LocalizedPart(entry.Definition), () => WordExample(entry));
        var choicesGrid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        for (var index = 0; index < choices.Length; index++)
        {
            var choice = choices[index];
            var button = GameChoiceButton($"{index + 1}   {choice.Word}", session.Game);
            button.Tag = $"game-choice-{index}";
            SetGameChoiceMetadata(button, $"game.Choice.{index + 1}", (index + 1).ToString(CultureInfo.InvariantCulture));
            button.Click += async (_, _) =>
            {
                await ResolveGameAnswerAsync(session, choice.Key == entry.Key, FeedbackWithAnswer(entry.Word, choice.Word), button, [entry.Key]);
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
        AddGameQuestion(panel, LocalizedPart(entry.Definition), () => WordExample(entry));
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
            button.Content = letter.ToString();
            button.Tag = $"game-letter-{letter}";
            AutomationProperties.SetAutomationId(button, $"game.Letter.{letter}");
            button.MinHeight = 40;
            button.MinWidth = 36;
            button.Padding = new Thickness(0, 6, 0, 6);
            button.FontSize = ReadingSize(18);
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
        ConfigureResponsiveGrid(keyboard, 13, 32);
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
        var detailEntry = entries[0];
        AddGameQuestion(panel, U("Games.Memory.Pairs", "Match 6 pairs", "6 çifti eşleştirin"), () => WordExample(detailEntry));
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        Button? firstButton = null;
        VocabularyEntry? firstEntry = null;
        var pairBusy = false;
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
                if (!IsCurrentGameRound(session, epoch) || !GameCanAnswer(session) || _gameLocalBusy || pairBusy || !button.IsEnabled || ReferenceEquals(button, firstButton)) return;
                void RevealTile()
                {
                    detailEntry = tile.Entry;
                    button.Content = new TextBlock
                    {
                        Text = tile.Text,
                        FontSize = ReadingSize(18),
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = button.Foreground,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
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
                    pairBusy = true;
                    try
                    {
                        await Task.Delay(GameMotionOff ? 160 : 900);
                        if (!IsCurrentGameRound(session, epoch) || !GameCanAnswer(session)) return;
                        previous.Content = "✦";
                        button.Content = "✦";
                        var previousCardNumber = previous.Tag is int number ? number : 0;
                        AutomationProperties.SetName(previous, $"{U("Games.Memory.Hidden", "Hidden card", "Gizli kart")} {previousCardNumber}");
                        AutomationProperties.SetName(button, $"{U("Games.Memory.Hidden", "Hidden card", "Gizli kart")} {cardNumber}");
                        AutomationProperties.SetItemStatus(previous, "");
                        AutomationProperties.SetItemStatus(button, "");
                    }
                    finally { pairBusy = false; }
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
        var guessed = new HashSet<string>(StringComparer.Ordinal);
        var panel = GameSceneContent();
        panel.Spacing = 5;
        AddGameQuestion(panel, U("Games.Guess.Prompt", "Guess the hidden word", "Gizli kelimeyi tahmin edin"), () => WordExample(entry));
        panel.Children.Add(new TextBlock
        {
            Text = $"{target.Length} · " + U("Games.Guess.Legend", "6 tries · ✓ exact · ~ elsewhere · × absent", "6 deneme · ✓ doğru yerde · ~ başka yerde · × yok"),
            FontSize = ReadingSize(18),
            FontWeight = Microsoft.UI.Text.FontWeights.Normal,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 241, 245, 249)),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            LineHeight = ReadingSize(18) * 1.3,
        });
        var board = new StackPanel { Spacing = 7, Width = 308, HorizontalAlignment = HorizontalAlignment.Stretch };
        var attemptCount = GameCaption($"{U("Games.Tries", "Tries", "Deneme")}: 0 / 6");
        panel.Children.Add(attemptCount);
        var choicesHost = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(choicesHost);

        void CompactChoiceHost(StackPanel host)
        {
            if (host.Children.OfType<Grid>().FirstOrDefault() is not Grid grid) return;
            grid.ColumnSpacing = 6;
            grid.RowSpacing = 6;
            foreach (var button in grid.Children.OfType<Button>())
            {
                button.MinHeight = 44;
                button.Padding = new Thickness(10, 6, 10, 6);
                button.FontSize = ReadingSize(18);
            }
        }

        VocabularyEntry[] BuildChoices()
        {
            var sameLength = pool.Where(word => GameAnswer(word.Word).Length == target.Length).ToArray();
            if (sameLength.Length < 4) return [];
            var distractors = sameLength.Where(word => word.Key != entry.Key &&
                    !guessed.Contains(GameAnswer(word.Word).ToUpperInvariant()))
                .OrderBy(_ => _random.Next()).Take(3).ToArray();
            if (distractors.Length < 3)
                distractors = sameLength.Where(word => word.Key != entry.Key)
                    .OrderBy(_ => _random.Next()).Take(3).ToArray();
            if (distractors.Length < 3) return [];
            return distractors.Append(entry).OrderBy(_ => _random.Next()).ToArray();
        }

        void RenderChoices()
        {
            if (!IsCurrentGameRound(session, epoch)) return;
            var roundChoices = BuildChoices();
            if (roundChoices.Length < 4) { GameUnavailable(session, EligibleRequirement); return; }
            var answers = GameSceneContent();
            GameChoices(answers, session, roundChoices.Select(word => word.Word).ToArray(), async (index, button) =>
            {
                if (!IsCurrentGameRound(session, epoch) || !GameCanAnswer(session) || _gameLocalBusy) return;
                var guess = GameAnswer(roundChoices[index].Word).ToUpperInvariant();
                var correct = guess == target;
                if (!await RecordGameSubAnswerAsync(session, correct, button, [entry.Key]) || !IsCurrentGameRound(session, epoch)) return;
                guessed.Add(guess);
                attempts++;
                attemptCount.Text = $"{U("Games.Tries", "Tries", "Deneme")}: {attempts} / 6";
                board.Children.Add(WordGuessRow(guess, target));
                if (correct)
                {
                    await ResolveGameAnswerAsync(session, true, entry.Word, button, [entry.Key], answerAlreadyRecorded: true);
                    return;
                }
                if (attempts >= 6)
                {
                    await ResolveGameAnswerAsync(session, false, entry.Word, button, [entry.Key], answerAlreadyRecorded: true);
                    return;
                }
                RenderChoices();
            });
            CompactChoiceHost(answers);
            choicesHost.Content = answers;
        }

        RenderChoices();
        AddGameScene(session.Game, panel, board);
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
            var foreground = AppearancePalette.EnsureTextContrast(color, Microsoft.UI.Colors.White);
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
                    Foreground = new SolidColorBrush(foreground),
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
        AddGameQuestion(panel, LocalizedPart(entry.Definition), () => WordExample(entry));
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
        var questionPool = _words.Where(candidate =>
                !string.IsNullOrWhiteSpace(LocalizedPart(candidate.Definition)))
            .DistinctBy(candidate => candidate.Key)
            .ToArray();
        if (questionPool.Length < 4) { GameUnavailable(session, EligibleRequirement); return; }

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

        var word = AddGameQuestion(panel, string.Empty, () => correctEntry is { } current ? WordExample(current) : null);
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

        VocabularyEntry[] CandidateOrder()
        {
            var fresh = questionPool.Where(candidate => !askedKeys.Contains(candidate.Key))
                .OrderBy(_ => _random.Next())
                .ToArray();
            return fresh.Length >= 4 ? fresh : questionPool.OrderBy(_ => _random.Next()).ToArray();
        }

        VocabularyEntry[] BuildChoices(VocabularyEntry answer)
        {
            var answerDefinition = LocalizedPart(answer.Definition);
            var distractors = questionPool
                .Where(candidate => candidate.Key != answer.Key &&
                    !string.Equals(LocalizedPart(candidate.Definition), answerDefinition, StringComparison.OrdinalIgnoreCase))
                .DistinctBy(candidate => LocalizedPart(candidate.Definition), StringComparer.OrdinalIgnoreCase)
                .OrderBy(_ => _random.Next())
                .Take(3)
                .ToArray();
            if (distractors.Length < 3) return [];
            return distractors.Append(answer).OrderBy(_ => _random.Next()).ToArray();
        }

        bool TryPrepareQuestion(out VocabularyEntry answer, out VocabularyEntry[] choices)
        {
            foreach (var candidate in CandidateOrder())
            {
                var prepared = BuildChoices(candidate);
                if (prepared.Length == 4)
                {
                    askedKeys.Add(candidate.Key);
                    answer = candidate;
                    choices = prepared;
                    return true;
                }
            }
            answer = questionPool[0];
            choices = [];
            return false;
        }

        void NextQuestion()
        {
            if (!IsCurrentGameRound(session, epoch)) return;
            if (!TryPrepareQuestion(out var picked, out var choices))
            {
                GameUnavailable(session, EligibleRequirement);
                return;
            }
            answered = false;
            correctEntry = picked;
            // Re-set every time a new sub-question is presented so the Hint button never goes stale mid-gauntlet.
            _gameHintProvider = () => WordHint(correctEntry!);
            word.Text = correctEntry.Word;
            for (var i = 0; i < optionButtons.Length; i++)
            {
                currentChoices[i] = choices[i];
                var label = LocalizedPart(choices[i].Definition);
                optionButtons[i].Content = ButtonContent(label, "\uE72A");
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
            if (!await PauseGameFeedbackAsync(session, $"{(correct ? T("Common.Correct") : T("Common.Wrong"))}\n{tested.Word} — {MeaningWithTurkish(tested.Definition)}") || !IsCurrentGameRound(session, epoch)) return;
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
    // Hangman's figure) once its row is solved correctly.
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
        var bonusBackground = Color.FromArgb(255, 30, 41, 59);
        var bonusForeground = AppearancePalette.EnsureTextContrast(bonusBackground, Microsoft.UI.Colors.White);
        var bonusBorder = AppearancePalette.EnsureBoundaryContrast(bonusBackground, Color.FromArgb(255, 148, 163, 184));
        var bonusBoxes = new Border[entries.Length];
        var bonusText = new TextBlock[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            var text = new TextBlock
            {
                FontSize = ReadingSize(18),
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(bonusForeground),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var box = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(bonusBorder),
                Background = new SolidColorBrush(bonusBackground),
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

        string[] BuildRowChoices(int row)
        {
            var targetWord = bare[row];
            var distractors = pool.Where(candidate =>
                    !string.Equals(GameAnswer(candidate.Word).ToUpperInvariant(), targetWord, StringComparison.Ordinal) &&
                    GameAnswer(candidate.Word).Length == targetWord.Length)
                .Select(candidate => candidate.Word)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(_ => _random.Next())
                .Take(3)
                .ToArray();
            if (distractors.Length < 3)
                distractors = pool.Where(candidate =>
                        !string.Equals(GameAnswer(candidate.Word).ToUpperInvariant(), targetWord, StringComparison.Ordinal))
                    .Select(candidate => candidate.Word)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(_ => _random.Next())
                    .Take(3)
                    .ToArray();
            if (distractors.Length < 3) return [];
            return distractors.Append(entries[row].Word).OrderBy(_ => _random.Next()).ToArray();
        }

        async Task SubmitRowAsync(int row, string selectedWord, Grid cellsGrid, Button submit)
        {
            if (!IsCurrentGameRound(session, epoch) || !GameCanAnswer(session) || _gameLocalBusy || solved[row] || row != activeRow) return;
            var selected = GameAnswer(selectedWord).ToUpperInvariant();
            var correct = selected == bare[row];
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
                if (!await PauseGameFeedbackAsync(session, U("Games.TryAgain", "Not yet — try again", "Henüz değil — tekrar deneyin") + "\n" + selectedWord) || !IsCurrentGameRound(session, epoch)) return;
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
                for (var col = 0; col < word.Length; col++)
                {
                    var isBonus = col == bonusIndex[row];
                    var cellBackground = solvedRow ? Color.FromArgb(255, 4, 120, 87) : Color.FromArgb(255, 30, 41, 59);
                    var text = new TextBlock
                    {
                        FontSize = ReadingSize(18),
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(AppearancePalette.EnsureTextContrast(cellBackground, Microsoft.UI.Colors.White)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    if (solvedRow) text.Text = word[col].ToString();
                    var box = new Border
                    {
                        Height = 32,
                        CornerRadius = new CornerRadius(6),
                        BorderThickness = new Thickness(2),
                        BorderBrush = new SolidColorBrush(solvedRow
                            ? AppearancePalette.EnsureBoundaryContrast(cellBackground, Color.FromArgb(255, 167, 243, 208))
                            : isBonus
                                ? AppearancePalette.EnsureBoundaryContrast(cellBackground, Color.FromArgb(255, 217, 119, 6))
                                : AppearancePalette.EnsureBoundaryContrast(cellBackground, Color.FromArgb(255, 148, 163, 184))),
                        Background = new SolidColorBrush(cellBackground),
                        Child = text,
                    };
                    Grid.SetColumn(box, col);
                    cellsGrid.ColumnDefinitions.Add(new ColumnDefinition());
                    cellsGrid.Children.Add(box);
                }

                var capturedRow = row;
                var rowStack = new StackPanel { Spacing = 8 };
                AddGameQuestion(rowStack, LocalizedPart(entries[capturedRow].Definition), () => WordExample(entries[capturedRow]));
                rowStack.Children.Add(cellsGrid);

                if (row == activeRow && !solvedRow)
                {
                    var rowChoices = BuildRowChoices(capturedRow);
                    if (rowChoices.Length < 4) { GameUnavailable(session, EligibleRequirement); return; }
                    GameChoices(rowStack, session, rowChoices, async (index, submit) => await SubmitRowAsync(capturedRow, rowChoices[index], cellsGrid, submit));
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
            else
            {
                session.Streak = 0;
                // Timed modes are defined as 60-second runs; wrong answers should
                // not end them early via hidden lives.
                if (_gameMode != "timed") session.Lives--;
            }
            AnimatePulse(source, correct);
            var keyLine = reviewedKeys.Count > 0 ? $"\n@gameKey:{reviewedKeys[0]}" : "";
            var message = $"{(correct ? T("Common.Correct") : T("Common.Wrong"))}\n{feedback}{keyLine}\n{T("Game.Score")}: {session.Score:N0}";
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
            var timedMaximum = session.TimedLimitSeconds > 0 ? session.TimedLimitSeconds : GameEngine.TimedSeconds;
            _gameTimerText = Heading(session.SecondsRemaining.ToString(), 20);
            _gameTimerProgress = new ProgressBar
            {
                Minimum = 0,
                Maximum = timedMaximum,
                Value = Math.Clamp(session.SecondsRemaining, 0, timedMaximum),
                Height = 3,
            };
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
        var parsed = ParseGameFeedbackMessage(message);
        var firstLine = parsed.Status;
        var correct = firstLine == U("Kids.Games.GotIt", "You got it!", "Bildin!");
        var incorrect = firstLine == U("Kids.Games.TryAgain", "Good try! Let's learn this one.", "Güzel deneme! Bunu birlikte öğrenelim.");

        var brushes = StudyResultBrushes(correct);
        var background = (brushes.Background as SolidColorBrush)?.Color ?? Color.FromArgb(255, 15, 23, 42);
        var bodyColor = EnsureStrongTextContrast(background,
            (brushes.Foreground as SolidColorBrush)?.Color ?? Microsoft.UI.Colors.White);
        var preferredAccent = correct ? Color.FromArgb(255, 134, 239, 172)
            : incorrect ? Color.FromArgb(255, 253, 186, 116)
            : Color.FromArgb(255, 125, 211, 252);
        var accentColor = AppearancePalette.EnsureTextContrast(background, preferredAccent);
        var labelColor = EnsureStrongTextContrast(background, preferredAccent);
        var borderColor = AppearancePalette.EnsureBoundaryContrast(background, accentColor);
        var accent = new SolidColorBrush(accentColor);
        var bodyBrush = new SolidColorBrush(bodyColor);
        var labelBrush = new SolidColorBrush(labelColor);

        var heading = GamePrompt((correct ? "✓ " : incorrect ? "↻ " : "ⓘ ") + U("Kids.Games.LookAnswer", "Look at the answer", "Yanıta bak"), 24);
        heading.TextAlignment = TextAlignment.Left;
        heading.Foreground = accent;
        AutomationProperties.SetHeadingLevel(heading, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);

        var text = GamePrompt(firstLine, 22);
        text.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        text.Foreground = bodyBrush;
        text.TextAlignment = TextAlignment.Left;
        AutomationProperties.SetAutomationId(text, "game.Feedback");
        var readableMessage = string.Join('\n', new[] { parsed.Status }
            .Concat(parsed.DetailLines)
            .Concat(parsed.HasScoreLine ? [$"{T("Game.Score")}: {parsed.Score}"] : []));
        AutomationProperties.SetName(text, readableMessage);

        var summary = new StackPanel { Spacing = 8 };
        void AddSummaryRow(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var row = new Grid { ColumnSpacing = 10, RowSpacing = 4 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var key = StudyText(label + ":", 20, labelBrush, emphasis: true);
            key.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
            key.VerticalAlignment = VerticalAlignment.Top;
            var content = StudyText(value.Trim(), 20, bodyBrush, selectable: true);
            Grid.SetColumn(content, 1);
            row.Children.Add(key);
            row.Children.Add(content);
            summary.Children.Add(row);
        }

        if (parsed.HasScoreLine)
        {
            var entry = ResolveFeedbackEntry(parsed);
            var meaning = entry is null ? parsed.Meaning : MeaningWithTurkish(entry.Definition);
            var example = entry is null ? parsed.Example : ExampleWithTurkish(entry.Example);

            AddSummaryRow(U("Games.Feedback.CorrectAnswer", "Correct answer", "Doğru yanıt"), parsed.CorrectAnswer);
            AddSummaryRow(U("Games.Feedback.YourAnswer", "Your answer", "Yanıtın"), parsed.YourAnswer);
            AddSummaryRow(U("Games.Feedback.Statement", "Shown statement", "Gösterilen ifade"), parsed.Statement);
            AddSummaryRow(T("Cards.Meaning"), !string.IsNullOrWhiteSpace(meaning)
                ? meaning
                : U("Games.Example.DefinitionUnavailable", "No definition is available for this round.", "Bu tur için tanım yok."));
            AddSummaryRow(T("Cards.Example"), !string.IsNullOrWhiteSpace(example)
                ? example
                : U("Games.Example.Unavailable", "No example is available for this round.", "Bu tur için örnek yok."));
            AddSummaryRow(T("Game.Score"), parsed.Score);

            foreach (var extra in parsed.Extras)
                AddSummaryRow(U("Games.Feedback.Statement", "Shown statement", "Gösterilen ifade"), extra);
        }
        else
        {
            foreach (var line in parsed.DetailLines)
            {
                var detail = StudyText(line, 20, bodyBrush, selectable: true);
                detail.TextAlignment = TextAlignment.Left;
                summary.Children.Add(detail);
            }
        }

        var copy = new StackPanel { Spacing = 12, Children = { heading, text, summary } };
        var card = new Border
        {
            Child = copy,
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(2),
            Padding = new Thickness(14, 8, 8, 8),
            Background = new SolidColorBrush(background),
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

    private sealed record GameFeedbackParse(
        string Status,
        string CorrectAnswer,
        string YourAnswer,
        string Statement,
        string Meaning,
        string Example,
        string Score,
        string FeedbackKey,
        string[] DetailLines,
        string[] Extras,
        bool HasScoreLine);

    private GameFeedbackParse ParseGameFeedbackMessage(string message)
    {
        var allLines = message.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        if (allLines.Length == 0)
            return new GameFeedbackParse("", "", "", "", "", "", "", "", [], [], false);

        var status = allLines[0];
        var scoreLabel = T("Game.Score");
        var score = "";
        var feedbackKey = "";
        var details = new List<string>();
        foreach (var line in allLines.Skip(1))
        {
            if (line.StartsWith("@gameKey:", StringComparison.Ordinal))
            {
                feedbackKey = line["@gameKey:".Length..].Trim();
                continue;
            }
            if (TryFeedbackLabelValue(line, scoreLabel, out var scoreValue))
            {
                score = scoreValue;
                continue;
            }
            details.Add(line);
        }

        var correctLabel = U("Games.Feedback.CorrectAnswer", "Correct answer", "Doğru yanıt");
        var yourLabel = U("Games.Feedback.YourAnswer", "Your answer", "Yanıtın");
        var statementLabel = U("Games.Feedback.Statement", "Shown statement", "Gösterilen ifade");
        var meaningLabel = T("Cards.Meaning");
        var exampleLabel = T("Cards.Example");

        var correct = "";
        var your = "";
        var statement = "";
        var meaning = "";
        var example = "";
        var extras = new List<string>();
        foreach (var line in details)
        {
            if (TryFeedbackLabelValue(line, correctLabel, out var correctValue))
            {
                if (correct.Length == 0) correct = correctValue;
                continue;
            }
            if (TryFeedbackLabelValue(line, yourLabel, out var yourValue) ||
                TryFeedbackLabelValue(line, U("Quiz.Selected", "Your answer", "Yanıtınız"), out yourValue))
            {
                if (your.Length == 0) your = yourValue;
                continue;
            }
            if (TryFeedbackLabelValue(line, statementLabel, out var statementValue))
            {
                if (statement.Length == 0) statement = statementValue;
                continue;
            }
            if (TryFeedbackLabelValue(line, meaningLabel, out var meaningValue))
            {
                if (meaning.Length == 0) meaning = meaningValue;
                continue;
            }
            if (TryFeedbackLabelValue(line, exampleLabel, out var exampleValue))
            {
                if (example.Length == 0) example = exampleValue;
                continue;
            }
            extras.Add(line);
        }

        if (correct.Length == 0 && extras.Count > 0)
        {
            correct = extras[0];
            extras.RemoveAt(0);
        }
        if (meaning.Length == 0 && extras.Count > 0)
        {
            meaning = extras[0];
            extras.RemoveAt(0);
        }
        if (example.Length == 0 && extras.Count > 0)
        {
            example = extras[0];
            extras.RemoveAt(0);
        }

        return new GameFeedbackParse(status, correct, your, statement, meaning, example, score, feedbackKey,
            details.ToArray(), extras.ToArray(), score.Length > 0);
    }

    private static bool TryFeedbackLabelValue(string line, string label, out string value)
    {
        var prefix = label + ":";
        if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = line[prefix.Length..].Trim();
            return true;
        }
        value = "";
        return false;
    }

    private VocabularyEntry? ResolveFeedbackEntry(GameFeedbackParse parsed)
    {
        if (!string.IsNullOrWhiteSpace(parsed.FeedbackKey))
        {
            var byKey = _words.FirstOrDefault(word =>
                string.Equals(word.Key, parsed.FeedbackKey, StringComparison.Ordinal));
            if (byKey is not null) return byKey;
        }

        var probes = new[] { parsed.CorrectAnswer, parsed.YourAnswer, parsed.Statement }
            .Concat(parsed.DetailLines)
            .Concat(parsed.Extras)
            .Where(value => !string.IsNullOrWhiteSpace(value));
        foreach (var probe in probes)
        {
            var token = FeedbackLookupToken(probe);
            if (token.Length == 0) continue;
            var exact = _words.FirstOrDefault(word =>
                string.Equals(word.Word, token, StringComparison.OrdinalIgnoreCase));
            if (exact is not null) return exact;
            var normalized = GameAnswer(token);
            if (normalized.Length == 0) continue;
            var normalizedMatch = _words.FirstOrDefault(word =>
                string.Equals(GameAnswer(word.Word), normalized, StringComparison.Ordinal));
            if (normalizedMatch is not null) return normalizedMatch;
        }
        return null;
    }

    private static string FeedbackLookupToken(string value)
    {
        var token = value.Trim();
        foreach (var separator in new[] { " — ", " ↔ ", " · " })
        {
            var index = token.IndexOf(separator, StringComparison.Ordinal);
            if (index > 0) token = token[..index].Trim();
        }
        token = token.Trim('✓', '✕', '↻', 'ⓘ', ' ');
        return token;
    }

    private static void AddHudCell(Grid grid, int column, string label, string value, string glyph) =>
        AddHudCell(grid, column, label, Heading(value, 20), null, glyph);

    private static void AddHudCell(Grid grid, int column, string label, UIElement value, ProgressBar? progress, string glyph)
    {
        var copy = new StackPanel { Spacing = 3 };
        var labelRow = new Grid { ColumnSpacing = 6 };
        labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        labelRow.ColumnDefinitions.Add(new ColumnDefinition());
        labelRow.Children.Add(DecorativeIcon(glyph, Font(13)));
        var labelText = new TextBlock { Text = label, FontSize = ReadingSize(18), TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetName(labelText, label);
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
        var icon = DecorativeIcon(game.Glyph, Font(44), new SolidColorBrush(Microsoft.UI.Colors.White));
        icon.HorizontalAlignment = HorizontalAlignment.Left;
        content.Children.Add(icon);
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

        var grid = new Grid { MinHeight = 240 };
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
        Child = DecorativeIcon(game.Glyph, Font(56), new SolidColorBrush(Microsoft.UI.Colors.White)),
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

    private sealed record GameQuestionExample(string Definition, string Example);

    // Game help content should keep the source + Turkish pair whenever present.
    private static string MeaningWithTurkish(string value) => value.Trim();

    // Bulb help must preserve the Turkish translation part even when UI language is not Turkish.
    private static string ExampleWithTurkish(string value) => value.Trim();

    private GameQuestionExample WordExample(VocabularyEntry entry) => new(
        $"{U("Games.Example.Word", "Word", "Kelime")}: {entry.Word}\n{LocalizedPart(entry.Definition)}",
        LocalizedPart(entry.Example));

    private TextBlock AddGameQuestion(Panel panel, string text, Func<GameQuestionExample?>? exampleProvider = null, string? automationSuffix = null)
    {
        var questionId = automationSuffix is null ? "game.Question" : $"game.Question.{automationSuffix}";
        var exampleId = automationSuffix is null ? "game.QuestionExample" : $"game.QuestionExample.{automationSuffix}";
        var cardId = automationSuffix is null ? "game.QuestionCard" : $"game.QuestionCard.{automationSuffix}";
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
        AutomationProperties.SetAutomationId(prompt, questionId);
        AutomationProperties.SetName(prompt, text);
        AutomationProperties.SetHelpText(prompt, U("Kids.Accessibility.ReadQuestion", "Read the question, then use the control below.", "Soruyu oku, sonra aşağıdaki kontrolü kullan."));
        AutomationProperties.SetHeadingLevel(prompt, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
        Live(prompt);
        var copy = new StackPanel { Spacing = 3 };
        copy.Children.Add(StudyLabel(U("Games.Question", "Question", "Soru")));
        var questionRow = new Grid { ColumnSpacing = 8 };
        questionRow.ColumnDefinitions.Add(new ColumnDefinition());
        if (exampleProvider is not null) questionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (exampleProvider is not null)
        {
            var exampleButton = new Button
            {
                Content = DecorativeIcon("\uE82F", Font(22), AppearancePalette.Current.ButtonTintForegroundBrush),
                MinWidth = 48,
                MinHeight = 48,
                Padding = new Thickness(10, 8, 10, 8),
                CornerRadius = new CornerRadius(12),
                Background = AppearancePalette.Current.ButtonTintBrush,
                Foreground = AppearancePalette.Current.ButtonTintForegroundBrush,
                BorderBrush = AppearancePalette.Current.ButtonBorderBrush,
                BorderThickness = new Thickness(1),
                IsTabStop = true,
                UseSystemFocusVisuals = true,
                HighContrastAdjustment = ElementHighContrastAdjustment.Auto,
            };
            ApplyAccessibleButtonVisuals(exampleButton, AppearancePalette.Current.ButtonTint,
                AppearancePalette.Current.ButtonTintForeground, AppearancePalette.Current.ButtonBorder);
            AutomationProperties.SetAutomationId(exampleButton, exampleId);
            var helpDetails = U("Games.Example.Help", "Help me with word definition and example", "Kelime tanımı ve örneği için yardım et");
            AutomationProperties.SetName(exampleButton, helpDetails);
            AutomationProperties.SetHelpText(exampleButton, helpDetails);
            ToolTipService.SetToolTip(exampleButton, helpDetails);
            exampleButton.Click += async (_, _) => await ShowGameQuestionExampleAsync(exampleButton, exampleProvider, exampleId);
            Grid.SetColumn(exampleButton, 1);
            questionRow.Children.Add(exampleButton);
        }
        Grid.SetColumn(prompt, 0);
        questionRow.Children.Add(prompt);
        copy.Children.Add(questionRow);
        var card = new Border
        {
            Child = copy,
            Background = new SolidColorBrush(Color.FromArgb(255, 248, 250, 252)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 5, 14, 5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetAutomationId(card, cardId);
        panel.Children.Add(card);
        return prompt;
    }

    private async Task ShowGameQuestionExampleAsync(Button source, Func<GameQuestionExample?> exampleProvider, string exampleId)
    {
        if (!IsLoaded || _studyBusy || !source.IsLoaded || !IsWithin(source, PageContent)) return;
        // A native dialog leaves the visual tree just before its awaited ShowAsync
        // continuation clears _dialogOpen. Honor a bulb click in that short close
        // transition instead of silently dropping it.
        if (_dialogClosed is { } pending) await pending.Task;
        for (var retry = 0; retry < 8 && _navigationBusy; retry++) await Task.Delay(25);
        if (!IsLoaded || _studyBusy || _dialogOpen || _navigationBusy || !source.IsLoaded || !source.IsEnabled || !IsWithin(source, PageContent)) return;
        var page = _currentPage;
        var context = StudyContext;
        var timer = _gameTimer;
        var wasRunning = timer?.IsEnabled == true;
        var game = _activeGame;
        var closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dialogOpen = true;
        _dialogClosed = closed;
        source.IsEnabled = false;
        timer?.Stop();
        try
        {
            var details = exampleProvider();
            var definition = details?.Definition?.Trim() ?? "";
            var example = details?.Example?.Trim() ?? "";
            if (definition.Length == 0) definition = U("Games.Example.DefinitionUnavailable", "No definition is available for this round.", "Bu tur için tanım yok.");
            if (example.Length == 0) example = U("Games.Example.Unavailable", "No example is available for this round.", "Bu tur için örnek yok.");
            var title = U("Games.Example.DialogTitle", "Definition and example", "Tanım ve örnek");
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                RequestedTheme = RequestedTheme,
                Title = title,
                Content = GameQuestionExampleContent(definition, example, exampleId),
                CloseButtonText = U("Games.Example.Close", "Close definition and example", "Tanımı ve örneği kapat"),
                DefaultButton = ContentDialogButton.Close,
            };
            AutomationProperties.SetAutomationId(dialog, exampleId + ".Dialog");
            AutomationProperties.SetName(dialog, title);
            ConfigureReadingDialog(dialog);
            await dialog.ShowAsync();
        }
        catch (Exception ex) { ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error); }
        finally
        {
            _dialogOpen = false;
            _dialogClosed = null;
            if (wasRunning && !_storageBlocked && _activeGame == game && ReferenceEquals(timer, _gameTimer)) timer?.Start();
            if (source.IsLoaded && IsWithin(source, PageContent)) source.IsEnabled = true;
            RestoreDialogFocus(source, page, context);
            closed.TrySetResult(true);
        }
    }

    private UIElement GameQuestionExampleContent(string definition, string example, string exampleId)
    {
        const int pageLength = 96;
        var definitionPages = GameQuestionExamplePages(definition, pageLength);
        var examplePages = GameQuestionExamplePages(example, pageLength);
        var pageCount = Math.Max(definitionPages.Count, examplePages.Count);
        var page = 0;
        var definitionLabel = U("Games.Example.Definition", "Definition", "Tanım");
        var exampleLabel = U("Games.Example.Title", "Example", "Örnek");
        var definitionText = StudyText("", 18, selectable: true);
        var exampleText = StudyText("", 18, selectable: true);
        AutomationProperties.SetAutomationId(definitionText, exampleId + ".Definition");
        AutomationProperties.SetAutomationId(exampleText, exampleId + ".Example");
        var definitionPanel = new StackPanel { Spacing = 8, Children = { StudyLabel(definitionLabel), definitionText } };
        var examplePanel = new StackPanel { Spacing = 8, Children = { StudyLabel(exampleLabel), exampleText } };
        var dividerBrush = new SolidColorBrush(AppearancePalette.EnsureBoundaryContrast(AppearancePalette.Current.Box, AppearancePalette.Current.Border));
        var divider = new Border
        {
            Width = 2,
            MinWidth = 2,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2),
            Background = dividerBrush,
            HighContrastAdjustment = ElementHighContrastAdjustment.Auto,
        };
        AutomationProperties.SetAccessibilityView(divider, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        var details = new Grid { ColumnSpacing = 16, Children = { definitionPanel, divider, examplePanel } };
        details.ColumnDefinitions.Add(new ColumnDefinition());
        details.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        details.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(divider, 1);
        Grid.SetColumn(examplePanel, 2);
        var status = StudyText("", 18, emphasis: true);
        AutomationProperties.SetAutomationId(status, exampleId + ".Page");
        Live(status);
        var previous = CompactStudyAction(SecondaryButton(U("Shell.PreviousPage", "Previous page", "Önceki sayfa"), ""));
        var next = CompactStudyAction(SecondaryButton(U("Shell.NextPage", "Next page", "Sonraki sayfa"), ""));
        AutomationProperties.SetAutomationId(previous, exampleId + ".Previous");
        AutomationProperties.SetAutomationId(next, exampleId + ".Next");
        void Refresh()
        {
            definitionText.Text = definitionPages[Math.Min(page, definitionPages.Count - 1)];
            exampleText.Text = examplePages[Math.Min(page, examplePages.Count - 1)];
            AutomationProperties.SetName(definitionText, $"{definitionLabel}: {definitionText.Text}");
            AutomationProperties.SetName(exampleText, $"{exampleLabel}: {exampleText.Text}");
            status.Text = $"{page + 1} / {pageCount}";
            previous.IsEnabled = page > 0;
            next.IsEnabled = page + 1 < pageCount;
        }
        previous.Click += (_, _) => { if (page > 0) { page--; Refresh(); Announce(status, status.Text); } };
        next.Click += (_, _) => { if (page + 1 < pageCount) { page++; Refresh(); Announce(status, status.Text); } };
        var navigation = new Grid { ColumnSpacing = 8, RowSpacing = 8, Children = { previous, next } };
        ConfigureResponsiveGrid(navigation, 2, 150);
        if (pageCount == 1)
        {
            status.Visibility = Visibility.Collapsed;
            navigation.Visibility = Visibility.Collapsed;
        }
        Refresh();
        return new StackPanel { Spacing = 12, Children = { status, details, navigation } };
    }

    private static IReadOnlyList<string> GameQuestionExamplePages(string text, int pageLength)
    {
        var pages = new List<string>();
        var remaining = text.Trim();
        while (remaining.Length > pageLength)
        {
            var split = remaining.LastIndexOfAny([' ', '\n'], pageLength);
            if (split < pageLength / 2) split = pageLength;
            pages.Add(remaining[..split].Trim());
            remaining = remaining[split..].TrimStart();
        }
        if (remaining.Length > 0 || pages.Count == 0) pages.Add(remaining);
        return pages;
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
            Content = ButtonContent(text, "\uE72A"),
            MinHeight = 56,
            MinWidth = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Stretch,
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
        ApplyAccessibleButtonVisuals(button, Color.FromArgb(255, 30, 41, 59), Microsoft.UI.Colors.White,
            Color.FromArgb(255, 100, 116, 139));
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
        var palette = AppearancePalette.Current;
        ApplyAccessibleButtonVisuals(button, palette.Button, palette.ButtonForeground, palette.ButtonBorder);
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
            var background = correct
                ? Color.FromArgb(255, 4, 120, 87)
                : Color.FromArgb(255, 190, 18, 60);
            control.Background = new SolidColorBrush(background);
            control.Foreground = new SolidColorBrush(AppearancePalette.EnsureTextContrast(background, Microsoft.UI.Colors.White));
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
        AutomationProperties.SetName(heading, title);
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
        AutomationProperties.SetName(hint, subtitle);
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
            BorderBrush = AppearancePalette.Current.BorderBrush,
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

    private static TextBlock QuickSettingLabel(string text)
    {
        var palette = AppearancePalette.Current;
        var label = new TextBlock
        {
            Text = text,
            FontSize = ReadingSize(18),
            Foreground = new SolidColorBrush(AppearancePalette.EnsureTextContrast(palette.Box, palette.BoxForeground)),
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
        };
        AutomationProperties.SetName(label, text);
        return label;
    }

    private void ApplyQuickSettingsVisuals()
    {
        var palette = AppearancePalette.Current;
        var toolbarBackground = palette.Box;
        var toolbarForeground = AppearancePalette.EnsureTextContrast(toolbarBackground, palette.BoxForeground);
        var toolbarBorder = AppearancePalette.EnsureBoundaryContrast(toolbarBackground, palette.Border);
        foreach (var control in new[] { QuickStudyLanguage, QuickLevel })
        {
            control.Background = new SolidColorBrush(toolbarBackground);
            control.Foreground = new SolidColorBrush(toolbarForeground);
            control.BorderBrush = new SolidColorBrush(toolbarBorder);
            control.BorderThickness = new Thickness(1);
            control.HighContrastAdjustment = ElementHighContrastAdjustment.Auto;
            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(Control.FontSizeProperty, ReadingSize(18)));
            itemStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 48d));
            itemStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 48d));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(toolbarForeground)));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(toolbarBackground)));
            control.ItemContainerStyle = itemStyle;
            foreach (var state in new[] { "", "PointerOver", "Pressed", "Focused", "Disabled" })
            {
                control.Resources[$"ComboBoxBackground{state}"] = control.Background;
                control.Resources[$"ComboBoxForeground{state}"] = control.Foreground;
                control.Resources[$"ComboBoxBorderBrush{state}"] = control.BorderBrush;
            }
        }

        var buttonBackground = palette.Button;
        ApplyAccessibleButtonVisuals(WindowModeButton, buttonBackground, palette.ButtonForeground, palette.ButtonBorder);
        WindowModeText.Foreground = WindowModeButton.Foreground;
        WindowModeIcon.Foreground = WindowModeButton.Foreground;
    }

    private static Button AccentButton(string text, string glyph)
    {
        var palette = AppearancePalette.Current;
        var background = AppearancePalette.EnsureFillContrast(Microsoft.UI.Colors.White, palette.Button, 4.5);
        var foreground = AppearancePalette.EnsureTextContrast(background, Microsoft.UI.Colors.White);
        var border = AppearancePalette.EnsureBoundaryContrast(background, palette.ButtonBorder);
        var button = new Button
        {
            Content = ButtonContent(text, glyph),
            MinHeight = 48,
            MinWidth = 48,
            Padding = new Thickness(18, 12, 18, 12),
            CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(background),
            BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(foreground),
            FontSize = ReadingSize(18),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            IsTabStop = true,
            UseSystemFocusVisuals = true,
        };
        ApplyAccessibleButtonVisuals(button, background, foreground, border);
        AutomationProperties.SetName(button, text);
        return button;
    }

    private static Button SecondaryButton(string text, string glyph)
    {
        var palette = AppearancePalette.Current;
        var background = AppearancePalette.EnsureFillContrast(palette.Background, palette.Box, 3.5);
        var foreground = AppearancePalette.EnsureTextContrast(background, palette.BoxForeground);
        var border = AppearancePalette.EnsureBoundaryContrast(background, palette.Border);
        var button = new Button
        {
            Content = ButtonContent(text, glyph),
            MinHeight = 48,
            MinWidth = 48,
            Padding = new Thickness(16, 12, 16, 12),
            CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(background),
            BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(foreground),
            FontSize = ReadingSize(18),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            IsTabStop = true,
            UseSystemFocusVisuals = true,
        };
        ApplyAccessibleButtonVisuals(button, background, foreground, border);
        AutomationProperties.SetName(button, text);
        return button;
    }

    private static void ApplyAccessibleButtonVisuals(Button button, Color background, Color preferredForeground, Color preferredBorder)
    {
        var foreground = AppearancePalette.EnsureTextContrast(background, preferredForeground);
        var border = AppearancePalette.EnsureBoundaryContrast(background, preferredBorder);
        var backgroundBrush = new SolidColorBrush(background);
        var foregroundBrush = new SolidColorBrush(foreground);
        var borderBrush = new SolidColorBrush(border);
        button.Background = backgroundBrush;
        button.Foreground = foregroundBrush;
        button.BorderBrush = borderBrush;
        button.BorderThickness = new Thickness(1);
        button.HighContrastAdjustment = ElementHighContrastAdjustment.Auto;
        button.UseSystemFocusVisuals = true;
        if (button.Content is UIElement content) ApplyButtonIconForeground(content, foregroundBrush);
        foreach (var state in new[] { "PointerOver", "Pressed", "Focused", "Disabled" })
        {
            SetButtonResource(button, $"ButtonBackground{state}", backgroundBrush);
            SetButtonResource(button, $"ButtonForeground{state}", foregroundBrush);
            SetButtonResource(button, $"ButtonBorderBrush{state}", borderBrush);
        }
    }

    private static void SetButtonResource(Button button, string key, object value)
    {
        button.Resources.Remove(key);
        button.Resources[key] = value;
    }

    private static void ApplyButtonIconForeground(UIElement element, Brush foreground)
    {
        if (element is FontIcon icon) icon.Foreground = foreground;
        if (element is Panel panel)
            foreach (var child in panel.Children) ApplyButtonIconForeground(child, foreground);
        else if (element is Border { Child: UIElement child }) ApplyButtonIconForeground(child, foreground);
        else if (element is ContentControl { Content: UIElement content }) ApplyButtonIconForeground(content, foreground);
    }

    private static Grid ButtonContent(string text, string glyph)
    {
        glyph = string.IsNullOrWhiteSpace(glyph) ? ButtonGlyphForText(text) : glyph;
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var icon = DecorativeIcon(glyph, Font(16));
        icon.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(icon);
        var label = new TextBlock
        {
            Text = text,
            FontSize = ReadingSize(18),
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
            LineHeight = ReadingSize(18) * 1.35,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);
        return grid;
    }

    private static string ButtonGlyphForText(string text)
    {
        var value = text.Trim().ToLowerInvariant();
        if (value.Contains("listen", StringComparison.Ordinal) || value.Contains("dinle", StringComparison.Ordinal) ||
            value.Contains("anhör", StringComparison.Ordinal) || value.Contains("écout", StringComparison.Ordinal) ||
            value.Contains("escuch", StringComparison.Ordinal) || value.Contains("ouvir", StringComparison.Ordinal) ||
            value.Contains("luister", StringComparison.Ordinal)) return "";
        if (value.Contains("previous", StringComparison.Ordinal) || value.Contains("önceki", StringComparison.Ordinal) ||
            value.Contains("zurück", StringComparison.Ordinal) || value.Contains("précéd", StringComparison.Ordinal) ||
            value.Contains("anterior", StringComparison.Ordinal) || value.Contains("vorige", StringComparison.Ordinal)) return "";
        if (value.Contains("next", StringComparison.Ordinal) || value.Contains("sonraki", StringComparison.Ordinal) ||
            value.Contains("weiter", StringComparison.Ordinal) || value.Contains("suivant", StringComparison.Ordinal) ||
            value.Contains("siguiente", StringComparison.Ordinal) || value.Contains("seguinte", StringComparison.Ordinal) ||
            value.Contains("volgende", StringComparison.Ordinal)) return "";
        if (value.Contains("undo", StringComparison.Ordinal) || value.Contains("geri al", StringComparison.Ordinal) ||
            value.Contains("rückgängig", StringComparison.Ordinal) || value.Contains("annuler", StringComparison.Ordinal) ||
            value.Contains("deshacer", StringComparison.Ordinal) || value.Contains("anular", StringComparison.Ordinal) ||
            value.Contains("ongedaan", StringComparison.Ordinal)) return "";
        if (value.Contains("repeat", StringComparison.Ordinal) || value.Contains("tekrar", StringComparison.Ordinal) ||
            value.Contains("wiederholen", StringComparison.Ordinal) || value.Contains("réessayer", StringComparison.Ordinal) ||
            value.Contains("repetir", StringComparison.Ordinal) || value.Contains("opnieuw", StringComparison.Ordinal)) return "\uE72C";
        if (value.Contains("how to play", StringComparison.Ordinal) || value.Contains("nasıl oyn", StringComparison.Ordinal) ||
            value.Contains("spielanleitung", StringComparison.Ordinal) || value.Contains("comment jouer", StringComparison.Ordinal) ||
            value.Contains("cómo jugar", StringComparison.Ordinal) || value.Contains("como jogar", StringComparison.Ordinal) ||
            value.Contains("hoe te spelen", StringComparison.Ordinal)) return "\uE897";
        if (value.Contains("help", StringComparison.Ordinal) || value.Contains("yardım", StringComparison.Ordinal) ||
            value.Contains("hilfe", StringComparison.Ordinal) || value.Contains("aide", StringComparison.Ordinal) ||
            value.Contains("ayuda", StringComparison.Ordinal) || value.Contains("ajuda", StringComparison.Ordinal)) return "\uE897";
        if (value.Contains("setting", StringComparison.Ordinal) || value.Contains("ayar", StringComparison.Ordinal) ||
            value.Contains("einstellung", StringComparison.Ordinal) || value.Contains("régl", StringComparison.Ordinal) ||
            value.Contains("ajust", StringComparison.Ordinal) || value.Contains("defini", StringComparison.Ordinal) ||
            value.Contains("instelling", StringComparison.Ordinal)) return "";
        if (value.Contains("save", StringComparison.Ordinal) || value.Contains("kaydet", StringComparison.Ordinal) ||
            value.Contains("speicher", StringComparison.Ordinal) || value.Contains("enregistr", StringComparison.Ordinal) ||
            value.Contains("guardar", StringComparison.Ordinal) || value.Contains("guardar", StringComparison.Ordinal) ||
            value.Contains("opslaan", StringComparison.Ordinal)) return "\uE73E";
        if (value.Contains("export", StringComparison.Ordinal) || value.Contains("dışa", StringComparison.Ordinal) ||
            value.Contains("exporter", StringComparison.Ordinal) || value.Contains("exportar", StringComparison.Ordinal)) return "\uE950";
        if (value.Contains("import", StringComparison.Ordinal) || value.Contains("içe", StringComparison.Ordinal) ||
            value.Contains("öffnen", StringComparison.Ordinal) || value.Contains("ouvrir", StringComparison.Ordinal) ||
            value.Contains("abrir", StringComparison.Ordinal) || value.Contains("open", StringComparison.Ordinal)) return "\uE8B5";
        if (value.Contains("clear", StringComparison.Ordinal) || value.Contains("temizle", StringComparison.Ordinal) ||
            value.Contains("reset", StringComparison.Ordinal) || value.Contains("sıfır", StringComparison.Ordinal) ||
            value.Contains("zurücksetzen", StringComparison.Ordinal) || value.Contains("effacer", StringComparison.Ordinal) ||
            value.Contains("borrar", StringComparison.Ordinal) || value.Contains("limpar", StringComparison.Ordinal) ||
            value.Contains("wissen", StringComparison.Ordinal)) return "\uE894";
        if (value.Contains("play", StringComparison.Ordinal) || value.Contains("oyna", StringComparison.Ordinal) ||
            value.Contains("spielen", StringComparison.Ordinal) || value.Contains("jouer", StringComparison.Ordinal) ||
            value.Contains("jugar", StringComparison.Ordinal) || value.Contains("jogar", StringComparison.Ordinal) ||
            value.Contains("spelen", StringComparison.Ordinal)) return "\uE768";
        if (value.Contains("back", StringComparison.Ordinal) || value.Contains("geri", StringComparison.Ordinal) ||
            value.Contains("retour", StringComparison.Ordinal) || value.Contains("voltar", StringComparison.Ordinal) ||
            value.Contains("terug", StringComparison.Ordinal)) return "";
        if (value.Contains("word", StringComparison.Ordinal) || value.Contains("kelime", StringComparison.Ordinal) ||
            value.Contains("wörter", StringComparison.Ordinal) || value.Contains("mots", StringComparison.Ordinal) ||
            value.Contains("palabra", StringComparison.Ordinal) || value.Contains("palavras", StringComparison.Ordinal) ||
            value.Contains("woorden", StringComparison.Ordinal)) return "\uE82D";
        if (value.Contains("favorite", StringComparison.Ordinal) || value.Contains("favori", StringComparison.Ordinal) ||
            value.Contains("favorit", StringComparison.Ordinal)) return "★";
        if (value.Contains("known", StringComparison.Ordinal) || value.Contains("bili", StringComparison.Ordinal) ||
            value.Contains("bekannt", StringComparison.Ordinal) || value.Contains("connu", StringComparison.Ordinal) ||
            value.Contains("conoc", StringComparison.Ordinal) || value.Contains("conhec", StringComparison.Ordinal) ||
            value.Contains("bekend", StringComparison.Ordinal)) return "\uE73E";
        if (value.Contains("delete", StringComparison.Ordinal) || value.Contains("sil", StringComparison.Ordinal) ||
            value.Contains("löschen", StringComparison.Ordinal) || value.Contains("supprimer", StringComparison.Ordinal) ||
            value.Contains("eliminar", StringComparison.Ordinal) || value.Contains("apagar", StringComparison.Ordinal) ||
            value.Contains("verwijder", StringComparison.Ordinal)) return "\uE74D";
        if (value.Contains("use saved", StringComparison.Ordinal) || value.Contains("kayıtlı", StringComparison.Ordinal) ||
            value.Contains("gespeichert", StringComparison.Ordinal) || value.Contains("enregistr", StringComparison.Ordinal) ||
            value.Contains("guardad", StringComparison.Ordinal) || value.Contains("opgeslagen", StringComparison.Ordinal)) return "\uE73E";
        if (value.Contains("keep", StringComparison.Ordinal) || value.Contains("koru", StringComparison.Ordinal) ||
            value.Contains("behalten", StringComparison.Ordinal) || value.Contains("garder", StringComparison.Ordinal) ||
            value.Contains("conservar", StringComparison.Ordinal) || value.Contains("manter", StringComparison.Ordinal) ||
            value.Contains("behoud", StringComparison.Ordinal)) return "\uE73E";
        if (value.Contains("dark", StringComparison.Ordinal) || value.Contains("koyu", StringComparison.Ordinal) ||
            value.Contains("dunkel", StringComparison.Ordinal) || value.Contains("sombre", StringComparison.Ordinal) ||
            value.Contains("oscuro", StringComparison.Ordinal) || value.Contains("escuro", StringComparison.Ordinal) ||
            value.Contains("donker", StringComparison.Ordinal)) return "\uE708";
        if (value.Contains("light", StringComparison.Ordinal) || value.Contains("açık", StringComparison.Ordinal) ||
            value.Contains("hell", StringComparison.Ordinal) || value.Contains("clair", StringComparison.Ordinal) ||
            value.Contains("claro", StringComparison.Ordinal) || value.Contains("licht", StringComparison.Ordinal)) return "\uE706";
        if (value.Contains("read", StringComparison.Ordinal) || value.Contains("oku", StringComparison.Ordinal) ||
            value.Contains("lesen", StringComparison.Ordinal) || value.Contains("lire", StringComparison.Ordinal) ||
            value.Contains("leer", StringComparison.Ordinal)) return "";
        return "\uE72A";
    }

    private static UIElement SettingHeader(string glyph, string title, string hint)
    {
        var grid = new Grid { ColumnSpacing = 14 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = string.IsNullOrEmpty(glyph) ? new GridLength(0) : new GridLength(36) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        if (!string.IsNullOrEmpty(glyph))
        {
            var icon = DecorativeIcon(glyph, Font(20));
            icon.VerticalAlignment = VerticalAlignment.Top;
            grid.Children.Add(icon);
        }
        else grid.ColumnSpacing = 0;
        var copy = new StackPanel { Spacing = 3 };
        copy.Children.Add(Heading(title, 17));
        copy.Children.Add(Body(hint));
        Grid.SetColumn(copy, 1);
        grid.Children.Add(copy);
        return grid;
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
            BorderThickness = new Thickness(2),
            Flyout = new Flyout { Content = picker },
        };
        var palette = AppearancePalette.Current;
        ApplyAccessibleButtonVisuals(swatch, color,
            AppearancePalette.EnsureTextContrast(color, palette.BoxForeground), palette.Border);
        swatch.BorderThickness = new Thickness(2);
        AutomationProperties.SetName(swatch, title);
        AutomationProperties.SetHelpText(swatch, hint);
        ToolTipService.SetToolTip(swatch, $"{title}. {hint}");
        picker.ColorChanged += (_, args) =>
        {
            ApplyAccessibleButtonVisuals(swatch, args.NewColor,
                AppearancePalette.EnsureTextContrast(args.NewColor, palette.BoxForeground), palette.Border);
            swatch.BorderThickness = new Thickness(2);
        };

        var row = new Grid { ColumnSpacing = 16 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(SettingHeader("", title, hint));
        Grid.SetColumn(swatch, 1);
        row.Children.Add(swatch);
        return (row, picker);
    }

    private static Action ConfigureResponsiveGrid(
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
        return Reflow;
    }

    private static void AddStat(Grid grid, int column, string label, string value, string glyph)
    {
        var content = new StackPanel { Spacing = 5 };
        var icon = DecorativeIcon(glyph, Font(18));
        icon.HorizontalAlignment = HorizontalAlignment.Left;
        content.Children.Add(icon);
        var labelText = Body(label);
        AutomationProperties.SetName(labelText, label);
        AutomationProperties.SetHeadingLevel(labelText, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level3);
        content.Children.Add(labelText);
        var valueText = Heading(value, 24);
        AutomationProperties.SetName(valueText, $"{label}: {value}");
        content.Children.Add(valueText);
        var card = Card(content, 14);
        card.HighContrastAdjustment = ElementHighContrastAdjustment.Auto;
        AutomationProperties.SetName(card, $"{label}: {value}");
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
    }

    private static FontIcon DecorativeIcon(string glyph, double size, Brush? foreground = null)
    {
        var icon = new FontIcon { Glyph = glyph, FontSize = size, Foreground = foreground };
        AutomationProperties.SetAccessibilityView(icon, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
        return icon;
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
        QuickStudyLanguage.IsEnabled = QuickLevel.IsEnabled = !loading && !_studyBusy;
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
