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
    private readonly AppStorage _storage = new();
    private readonly VocabularyRepository _repository = new();
    private readonly Random _random = new();

    private UserSettings _settings = new();
    private ProgressState _progress = new();
    private IReadOnlyList<VocabularyEntry> _words = [];
    private string _currentPage = "home";
    private int _cardIndex;
    private bool _initialized;
    private GameSession? _activeGame;
    private DispatcherTimer? _gameTimer;
    private TextBlock? _gameTimerText;
    private ProgressBar? _gameTimerProgress;
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
        if (_initialized) return;
        _initialized = true;
        _settings = await _storage.LoadSettingsAsync();
        _progress = await _storage.LoadProgressAsync();

        if (!Localizer.UiLanguages.Any(language => language.Code == _settings.UiLanguage))
            _settings.UiLanguage = "tr";
        if (!VocabularyRepository.Languages.Any(language => language.Code == _settings.StudyLanguage))
            _settings.StudyLanguage = "en";

        ApplyAppearance();
        _ = GameCatalog.All;
        ApplyNavigationLanguage();
        QuickUiLanguage.ItemsSource = Localizer.UiLanguages;
        QuickStudyLanguage.ItemsSource = VocabularyRepository.Languages;
        ConfigureResponsiveGrid(QuickSettingsGrid, 3, 170);
        await ReloadWordsAsync();
        Navigation.SelectedItem = HomeItem;
        RenderCurrentPage();

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
        ContentHost.Padding = new Thickness(horizontalPadding, compact ? 16 : 24, horizontalPadding, 52);
        PageContent.Width = Math.Max(0, Math.Min(1080, e.NewSize.Width - (horizontalPadding * 2)));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
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
        if (args.SelectedItemContainer?.Tag is not string tag) return;
        StopGameTimer();
        _activeGame = null;
        _currentPage = tag;
        await EnsureWordsAsync();
        RenderCurrentPage();
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
    }

    private void ApplyAppearance()
    {
        AppearancePalette.SetCurrent(_settings);
        RequestedTheme = AppearancePalette.Current.Theme;
        Background = AppearancePalette.Current.BackgroundBrush;
        Foreground = AppearancePalette.Current.BackgroundForegroundBrush;
        Navigation.Background = AppearancePalette.Current.BackgroundBrush;
        Navigation.Foreground = AppearancePalette.Current.BackgroundForegroundBrush;
        Navigation.FontSize = Font(14);
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
            _cardIndex = 0;
            await _storage.SaveSettingsAsync(_settings);
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
        ContentScroll.ChangeView(0, 0, null, disableAnimation: false);
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
            case "help": RenderInformation(T("Help.Title"), T("Help.Body"), ""); break;
            case "about": RenderInformation(T("About.Title"), T("About.Body"), ""); break;
            default: RenderHome(); break;
        }
        SyncQuickSettingsBar();
    }

    private void SyncQuickSettingsBar()
    {
        QuickUiLanguage.SelectedValue = _settings.UiLanguage;
        QuickStudyLanguage.SelectedValue = _settings.StudyLanguage;
        QuickLevel.ItemsSource = CurrentStudyLanguage().Levels;
        QuickLevel.SelectedItem = _settings.Level;
        AutomationProperties.SetName(QuickUiLanguage, T("Profile.UiLanguage"));
        AutomationProperties.SetName(QuickStudyLanguage, T("Profile.StudyLanguage"));
        AutomationProperties.SetName(QuickLevel, T("Profile.Level"));
    }

    private async void OnQuickUiLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QuickUiLanguage.SelectedValue is not string code || code == _settings.UiLanguage) return;
        _settings.UiLanguage = code;
        await _storage.SaveSettingsAsync(_settings);
        ApplyNavigationLanguage();
        RenderCurrentPage();
    }

    private async void OnQuickStudyLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QuickStudyLanguage.SelectedValue is not string code || code == _settings.StudyLanguage) return;
        _settings.StudyLanguage = code;
        _settings.Level = "A1";
        await ReloadWordsAsync();
        RenderCurrentPage();
    }

    private async void OnQuickLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QuickLevel.SelectedItem is not string selected || selected == _settings.Level) return;
        _settings.Level = selected;
        await ReloadWordsAsync();
        RenderCurrentPage();
    }

    private void RenderHome()
    {
        AddPageHeader(T("Home.Title"), T("Home.Subtitle"));
        PageContent.Children.Add(new InfoBar
        {
            IsOpen = true,
            IsClosable = false,
            Severity = InfoBarSeverity.Success,
            Title = T("Home.Offline"),
            Message = $"{CurrentStudyLanguage().NativeName} · {_settings.Level} · {_words.Count:N0}",
        });

        var stats = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        for (var index = 0; index < 4; index++) stats.ColumnDefinitions.Add(new ColumnDefinition());
        AddStat(stats, 0, T("Home.Known"), _progress.KnownWords.Count.ToString("N0"), "");
        AddStat(stats, 1, T("Home.Favorites"), _progress.FavoriteWords.Count.ToString("N0"), "");
        AddStat(stats, 2, T("Home.Games"), GameCatalog.All.Count.ToString(), "");
        AddStat(stats, 3, T("Stats.Success"), SuccessRate(), "");
        ConfigureResponsiveGrid(stats, 4, 190);
        PageContent.Children.Add(stats);

        var continuePanel = new StackPanel { Spacing = 12 };
        continuePanel.Children.Add(Heading(T("Home.Daily"), 20));
        continuePanel.Children.Add(Body($"{CurrentStudyLanguage().NativeName} · {_settings.Level}"));
        var continueButton = AccentButton(T("Home.Continue"), "");
        continueButton.Click += (_, _) => NavigateTo("cards", CardsItem);
        continuePanel.Children.Add(continueButton);
        PageContent.Children.Add(Card(continuePanel, 24));
    }

    private void RenderCards()
    {
        PageContent.Children.Clear();
        ContentScroll.ChangeView(0, 0, null, disableAnimation: true);
        AddPageHeader(T("Cards.Title"), $"{CurrentStudyLanguage().NativeName} · {_settings.Level}");
        if (_words.Count == 0) { PageContent.Children.Add(Body(T("Words.Empty"))); return; }

        var entry = _words[_cardIndex % _words.Count];
        var content = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Stretch };
        content.Children.Add(ChipRow(entry));
        content.Children.Add(new TextBlock
        {
            Text = entry.Word,
            FontSize = Font(42),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 20),
        });

        var definition = Body(LocalizedPart(entry.Definition));
        definition.FontSize = Font(19);
        definition.TextAlignment = TextAlignment.Center;
        definition.Visibility = Visibility.Collapsed;
        content.Children.Add(definition);
        var example = Body(entry.Example);
        example.TextAlignment = TextAlignment.Center;
        example.Visibility = Visibility.Collapsed;
        content.Children.Add(example);

        var tools = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        var reveal = AccentButton(T("Cards.Reveal"), "");
        reveal.Click += (_, _) =>
        {
            definition.Visibility = Visibility.Visible;
            reveal.IsEnabled = false;
        };
        var listen = SecondaryButton(T("Cards.Listen"), "");
        listen.Click += async (_, _) => await PlayWordAsync(entry.Word, listen);
        var showExample = SecondaryButton(T("Cards.Example"), "");
        showExample.Click += (_, _) =>
        {
            example.Visibility = Visibility.Visible;
            showExample.IsEnabled = false;
        };
        tools.Children.Add(reveal);
        tools.Children.Add(listen);
        tools.Children.Add(showExample);
        ConfigureResponsiveGrid(tools, 3, 150);
        content.Children.Add(tools);
        var studyCard = Card(content, 32);
        studyCard.MinHeight = 260;
        studyCard.HorizontalAlignment = HorizontalAlignment.Stretch;
        PageContent.Children.Add(studyCard);

        var actions = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        var favorite = SecondaryButton(T("Cards.Favorite"), "");
        favorite.Click += async (_, _) => { Toggle(_progress.FavoriteWords, entry.Key); await _storage.SaveProgressAsync(_progress); };
        var known = SecondaryButton(T("Cards.Known"), "");
        known.Click += async (_, _) =>
        {
            _progress.KnownWords.Add(entry.Key);
            RegisterStudy();
            await _storage.SaveProgressAsync(_progress);
            NextCard();
        };
        var next = AccentButton(T("Cards.Next"), "");
        next.Click += (_, _) => NextCard();
        actions.Children.Add(favorite);
        actions.Children.Add(known);
        actions.Children.Add(next);
        ConfigureResponsiveGrid(actions, 3, 165);
        PageContent.Children.Add(actions);
    }

    private void NextCard()
    {
        StopSpeechPlayback();
        _cardIndex = (_cardIndex + 1) % Math.Max(_words.Count, 1);
        RenderCards();
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

    private void RenderQuiz()
    {
        AddPageHeader(T("Quiz.Title"), $"{CurrentStudyLanguage().NativeName} · {_settings.Level}");
        if (_words.Count < 4) { PageContent.Children.Add(Body(T("Words.Empty"))); return; }
        var answer = _words[_random.Next(_words.Count)];
        var prompt = new StackPanel { Spacing = 12 };
        prompt.Children.Add(Body(T("Quiz.Question")));
        prompt.Children.Add(Heading(LocalizedPart(answer.Definition), 24));
        PageContent.Children.Add(Card(prompt, 24));
        var choices = _words.Where(word => word.Key != answer.Key).OrderBy(_ => _random.Next()).Take(3)
            .Append(answer).OrderBy(_ => _random.Next()).ToArray();
        var answerPanel = new StackPanel { Spacing = 10 };
        foreach (var choice in choices)
        {
            var button = ChoiceButton(choice.Word);
            button.Click += async (_, _) =>
            {
                await RegisterAnswerAsync(choice.Key == answer.Key, answer.Word);
                RenderQuiz();
            };
            answerPanel.Children.Add(button);
        }
        PageContent.Children.Add(answerPanel);
    }

    private void RenderWords()
    {
        AddPageHeader(T("Words.Title"), $"{CurrentStudyLanguage().NativeName} · {_settings.Level} · {_words.Count:N0}");
        var search = new AutoSuggestBox
        {
            PlaceholderText = T("Words.Search"),
            QueryIcon = new SymbolIcon(Symbol.Find),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var list = new StackPanel { Spacing = 8 };
        void Populate(string query)
        {
            list.Children.Clear();
            var matches = _words.Where(entry => string.IsNullOrWhiteSpace(query) ||
                    entry.Word.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    entry.Definition.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    entry.Category.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .Take(200).ToArray();
            if (matches.Length == 0) { list.Children.Add(Body(T("Words.Empty"))); return; }
            foreach (var entry in matches)
            {
                var row = new StackPanel { Spacing = 6 };
                row.Children.Add(Heading(entry.Word, 17));
                row.Children.Add(Body(LocalizedPart(entry.Definition)));
                list.Children.Add(Card(row, 14));
            }
        }
        search.TextChanged += (_, args) => { if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) Populate(search.Text); };
        PageContent.Children.Add(search);
        PageContent.Children.Add(list);
        Populate(string.Empty);
    }

    private void RenderGames(GameGroup group)
    {
        var games = GameCatalog.Get(group);
        AddPageHeader(T(group == GameGroup.Simple ? "Games.SimpleTitle" : "Games.ComplexTitle"),
            T(group == GameGroup.Simple ? "Games.SimpleSubtitle" : "Games.ComplexSubtitle"));
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        for (var index = 0; index < games.Count; index++)
        {
            var button = GameButton(games[index]);
            grid.Children.Add(button);
        }
        ConfigureResponsiveGrid(grid, 2, 390);
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
        best.Children.Add(new TextBlock { Text = T("Game.Best"), FontSize = Font(11), HorizontalAlignment = HorizontalAlignment.Right });
        best.Children.Add(Heading(_progress.GameBestScores.GetValueOrDefault(game.Id).ToString("N0"), 18));
        Grid.SetColumn(best, 2);
        layout.Children.Add(best);
        var description = Body(GameDescription(game));
        description.Margin = new Thickness(0, 10, 0, 0);
        Grid.SetRow(description, 1);
        Grid.SetColumnSpan(description, 3);
        layout.Children.Add(description);
        var button = new Button
        {
            Content = layout,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(18),
            MinHeight = 112,
            CornerRadius = new CornerRadius(8),
            Background = AppearancePalette.Current.BoxBrush,
            BorderBrush = AppearancePalette.Current.BorderBrush,
            BorderThickness = new Thickness(1),
            Foreground = AppearancePalette.Current.BoxForegroundBrush,
        };
        ApplyReadableForeground(layout, AppearancePalette.Current.BoxForegroundBrush);
        AutomationProperties.SetName(button, game.Title);
        button.Click += (_, _) => RenderGameDetail(game);
        return button;
    }

    private void RenderGameDetail(GameDefinition game)
    {
        StopGameTimer();
        _activeGame = null;
        PageContent.Children.Clear();
        var back = SecondaryButton(T("Games.Back"), "");
        back.HorizontalAlignment = HorizontalAlignment.Left;
        back.Click += (_, _) => RenderGames(game.Group);
        PageContent.Children.Add(back);
        var hero = GameHero(game, showBest: true);
        PageContent.Children.Add(hero);
        AnimateEntrance(hero);
        var details = new StackPanel { Spacing = 10 };
        details.Children.Add(Heading(GameDescription(game), 19));
        details.Children.Add(Body($"{T("Common.Language")}: {CurrentStudyLanguage().NativeName}  ·  {T("Common.Level")}: {_settings.Level}"));
        var start = AccentButton(T("Games.Start"), "");
        start.HorizontalAlignment = HorizontalAlignment.Left;
        start.Click += (_, _) => StartGame(game);
        details.Children.Add(start);
        PageContent.Children.Add(Card(details, 22));
    }

    private void StartGame(GameDefinition game)
    {
        StopGameTimer();
        _activeGame = new GameSession(game);
        RenderGameRound(_activeGame);
        if (_activeGame.IsTimed) StartGameTimer(_activeGame);
    }

    private void RenderGameRound(GameSession session)
    {
        if (_activeGame != session) return;
        PageContent.Children.Clear();
        var back = SecondaryButton(T("Games.Back"), "");
        back.HorizontalAlignment = HorizontalAlignment.Left;
        back.Click += (_, _) => RenderGameDetail(session.Game);
        PageContent.Children.Add(back);
        PageContent.Children.Add(GameHud(session));
        var progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = session.MaxRounds,
            Value = Math.Min(session.Round - 1, session.MaxRounds),
            Height = 5,
            CornerRadius = new CornerRadius(3),
        };
        PageContent.Children.Add(progress);

        if (_words.Count < 4) { PageContent.Children.Add(Body(T("Words.Empty"))); return; }

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
            case GameMechanic.TimedTyping:
            case GameMechanic.ListeningTyping:
            case GameMechanic.Scrabble:
            case GameMechanic.CategorySprint:
            case GameMechanic.ProgressiveClues:
            case GameMechanic.ClueWords:
            case GameMechanic.Crossword:
            case GameMechanic.WordSequence:
                RenderTypingChallenge(session);
                break;
            default:
                RenderChoiceChallenge(session);
                break;
        }
    }

    private void RenderTypingChallenge(GameSession session)
    {
        var entry = _words[_random.Next(_words.Count)];
        var panel = GameSceneContent();
        panel.Children.Add(GamePrompt(LocalizedPart(entry.Definition), 27));
        panel.Children.Add(GameCaption(entry.Category.ToUpperInvariant()));
        var input = new TextBox
        {
            PlaceholderText = T("Game.TypeAnswer"),
            FontSize = Font(20),
            MinHeight = 52,
            MaxWidth = 560,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        panel.Children.Add(input);
        var submit = GameActionButton(T("Game.Submit"), "", session.Game);
        submit.Click += async (_, _) =>
        {
            var correct = string.Equals(NormalizeAnswer(input.Text), NormalizeAnswer(entry.Word), StringComparison.OrdinalIgnoreCase);
            await ResolveGameAnswerAsync(session, correct, entry.Word, submit);
        };
        panel.Children.Add(submit);
        AddGameScene(session.Game, panel);
        input.Focus(FocusState.Programmatic);
    }

    private void RenderTrueFalseChallenge(GameSession session)
    {
        var entry = _words[_random.Next(_words.Count)];
        var isTrue = _random.Next(2) == 0;
        var shown = isTrue ? entry : _words.First(word => word.Key != entry.Key);
        var panel = GameSceneContent();
        panel.Children.Add(GamePrompt(entry.Word, 40));
        panel.Children.Add(GameCaption(LocalizedPart(shown.Definition)));
        var actions = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        foreach (var answer in new[] { true, false })
        {
            var button = GameChoiceButton(answer ? "✓  TRUE" : "✕  FALSE", session.Game);
            button.Click += async (_, _) =>
            {
                await ResolveGameAnswerAsync(session, answer == isTrue, entry.Word, button);
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
        var choices = _words.Where(word => word.Key != entry.Key).OrderBy(_ => _random.Next()).Take(3)
            .Append(entry).OrderBy(_ => _random.Next()).ToArray();
        var panel = GameSceneContent();
        panel.Children.Add(GameCaption(T("Quiz.Question").ToUpperInvariant()));
        panel.Children.Add(GamePrompt(LocalizedPart(entry.Definition), 27));
        var choicesGrid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        for (var index = 0; index < choices.Length; index++)
        {
            var choice = choices[index];
            var button = GameChoiceButton($"{index + 1}   {choice.Word}", session.Game);
            button.Click += async (_, _) =>
            {
                await ResolveGameAnswerAsync(session, choice.Key == entry.Key, entry.Word, button);
            };
            choicesGrid.Children.Add(button);
        }
        ConfigureResponsiveGrid(choicesGrid, 2, 260);
        panel.Children.Add(choicesGrid);
        AddGameScene(session.Game, panel);
    }

    private void RenderHangmanRound(GameSession session)
    {
        var entry = _words.Where(word => NormalizeAnswer(word.Word).Length is >= 4 and <= 12)
            .OrderBy(_ => _random.Next()).First();
        var target = NormalizeAnswer(entry.Word).ToUpperInvariant();
        var guessed = new HashSet<char>();
        var misses = 0;
        var panel = GameSceneContent();
        panel.Children.Add(GameCaption(LocalizedPart(entry.Definition)));
        var word = GamePrompt(string.Empty, 36);
        word.CharacterSpacing = 180;
        var missText = GameCaption(string.Empty);
        panel.Children.Add(word);
        panel.Children.Add(missText);
        var keyboard = new Grid { ColumnSpacing = 6, RowSpacing = 6 };
        void Refresh()
        {
            word.Text = string.Join(' ', target.Select(character => !char.IsLetter(character) || guessed.Contains(character) ? character : '_'));
            missText.Text = $"{T("Game.Lives")}: {Math.Max(0, 6 - misses)}  ·  {string.Join(' ', guessed.Order())}";
        }
        foreach (var letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
        {
            var button = GameChoiceButton(letter.ToString(), session.Game);
            button.MinHeight = 42;
            button.Padding = new Thickness(4);
            button.Click += async (_, _) =>
            {
                button.IsEnabled = false;
                guessed.Add(letter);
                if (!target.Contains(letter)) misses++;
                Refresh();
                if (target.All(character => !char.IsLetter(character) || guessed.Contains(character)))
                    await ResolveGameAnswerAsync(session, true, entry.Word, button);
                else if (misses >= 6)
                    await ResolveGameAnswerAsync(session, false, entry.Word, button);
            };
            keyboard.Children.Add(button);
        }
        ConfigureResponsiveGrid(keyboard, 9, 44);
        Refresh();
        panel.Children.Add(keyboard);
        AddGameScene(session.Game, panel);
    }

    private void RenderMemoryRound(GameSession session)
    {
        var entries = _words.OrderBy(_ => _random.Next()).Take(6).ToArray();
        var tiles = entries.SelectMany(entry => new[]
            {
                (Entry: entry, Text: entry.Word),
                (Entry: entry, Text: LocalizedPart(entry.Definition)),
            })
            .OrderBy(_ => _random.Next()).ToArray();
        var panel = GameSceneContent();
        panel.Children.Add(GameCaption("MATCH 6 PAIRS"));
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        Button? firstButton = null;
        VocabularyEntry? firstEntry = null;
        var matches = 0;
        for (var index = 0; index < tiles.Length; index++)
        {
            var tile = tiles[index];
            var button = GameChoiceButton("✦", session.Game);
            button.Click += async (_, _) =>
            {
                if (!button.IsEnabled || ReferenceEquals(button, firstButton)) return;
                button.Content = new TextBlock
                {
                    Text = tile.Text,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                };
                if (firstButton is null)
                {
                    firstButton = button;
                    firstEntry = tile.Entry;
                    return;
                }
                if (firstEntry?.Key == tile.Entry.Key)
                {
                    firstButton.IsEnabled = false;
                    button.IsEnabled = false;
                    firstButton = null;
                    firstEntry = null;
                    matches++;
                    if (matches == 6) await ResolveGameAnswerAsync(session, true, "6 / 6", button);
                }
                else
                {
                    var previous = firstButton;
                    firstButton = null;
                    firstEntry = null;
                    await Task.Delay(650);
                    previous.Content = "✦";
                    button.Content = "✦";
                }
            };
            grid.Children.Add(button);
        }
        ConfigureResponsiveGrid(grid, 4, 120, new GridLength(82));
        panel.Children.Add(grid);
        AddGameScene(session.Game, panel);
    }

    private void RenderWordGuessRound(GameSession session)
    {
        var entry = _words.Where(word => NormalizeAnswer(word.Word).All(char.IsLetter) && NormalizeAnswer(word.Word).Length is >= 4 and <= 9)
            .OrderBy(_ => _random.Next()).First();
        var target = NormalizeAnswer(entry.Word).ToUpperInvariant();
        var attempts = 0;
        var panel = GameSceneContent();
        panel.Children.Add(GameCaption($"{target.Length} LETTERS · 6 TRIES"));
        var board = new StackPanel { Spacing = 7, HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(board);
        var input = new TextBox { MaxLength = target.Length, FontSize = Font(20), MaxWidth = 420, PlaceholderText = T("Game.TypeAnswer") };
        panel.Children.Add(input);
        var submit = GameActionButton(T("Game.Submit"), "", session.Game);
        submit.Click += async (_, _) =>
        {
            var guess = NormalizeAnswer(input.Text).ToUpperInvariant();
            if (guess.Length != target.Length) return;
            attempts++;
            board.Children.Add(WordGuessRow(guess, target));
            input.Text = string.Empty;
            if (guess == target) await ResolveGameAnswerAsync(session, true, entry.Word, submit);
            else if (attempts >= 6) await ResolveGameAnswerAsync(session, false, entry.Word, submit);
        };
        panel.Children.Add(submit);
        AddGameScene(session.Game, panel);
        input.Focus(FocusState.Programmatic);
    }

    private static UIElement WordGuessRow(string guess, string target)
    {
        var row = new Grid { ColumnSpacing = 6, MaxWidth = 480, HorizontalAlignment = HorizontalAlignment.Stretch };
        for (var index = 0; index < guess.Length; index++)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var color = guess[index] == target[index]
                ? Color.FromArgb(255, 4, 120, 87)
                : target.Contains(guess[index]) ? Color.FromArgb(255, 161, 98, 7) : Color.FromArgb(255, 71, 85, 105);
            var tile = new Border
            {
                Height = 44,
                MaxWidth = 44,
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(color),
                Child = new TextBlock
                {
                    Text = guess[index].ToString(),
                    FontSize = Font(20),
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            Grid.SetColumn(tile, index);
            row.Children.Add(tile);
        }
        return row;
    }

    private async Task ResolveGameAnswerAsync(GameSession session, bool correct, string answer, Control source)
    {
        source.IsEnabled = false;
        if (correct)
        {
            session.Streak++;
            session.Score += 100 + Math.Min(session.Streak, 10) * 15;
        }
        else
        {
            session.Streak = 0;
            session.Lives--;
        }
        AnimatePulse(source, correct);
        await RegisterAnswerAsync(correct, answer);
        await Task.Delay(600);
        if (_activeGame != session) return;
        session.Round++;
        if (session.Lives <= 0 || session.Round > session.MaxRounds)
            await CompleteGameAsync(session);
        else
            RenderGameRound(session);
    }

    private async Task CompleteGameAsync(GameSession session)
    {
        StopGameTimer();
        var oldBest = _progress.GameBestScores.GetValueOrDefault(session.Game.Id);
        if (session.Score > oldBest) _progress.GameBestScores[session.Game.Id] = session.Score;
        await _storage.SaveProgressAsync(_progress);
        PageContent.Children.Clear();
        var summary = GameSceneContent();
        summary.Children.Add(GameCaption("✦  ✦  ✦"));
        summary.Children.Add(GamePrompt(T("Game.Finished"), 32));
        summary.Children.Add(GamePrompt(session.Score.ToString("N0"), 54));
        summary.Children.Add(GameCaption($"{T("Game.Best")}: {_progress.GameBestScores.GetValueOrDefault(session.Game.Id):N0}"));
        var replay = GameActionButton(T("Game.PlayAgain"), "", session.Game);
        replay.Click += (_, _) => StartGame(session.Game);
        summary.Children.Add(replay);
        AddGameScene(session.Game, summary);
    }

    private UIElement GameHud(GameSession session)
    {
        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        AddHudCell(grid, 0, T("Game.Score"), session.Score.ToString("N0"), "");
        AddHudCell(grid, 1, T("Game.Streak"), session.Streak.ToString(), "");
        AddHudCell(grid, 2, T("Game.Round"), $"{session.Round}/{session.MaxRounds}", "");
        if (session.IsTimed)
        {
            _gameTimerText = Heading(session.SecondsRemaining.ToString(), 20);
            _gameTimerProgress = new ProgressBar { Minimum = 0, Maximum = 60, Value = session.SecondsRemaining, Height = 3 };
            AddHudCell(grid, 3, T("Game.Time"), _gameTimerText, _gameTimerProgress, "");
        }
        else
            AddHudCell(grid, 3, T("Game.Lives"), new string('♥', Math.Max(0, session.Lives)), "");
        ConfigureResponsiveGrid(grid, 4, 170);
        return grid;
    }

    private static void AddHudCell(Grid grid, int column, string label, string value, string glyph) =>
        AddHudCell(grid, column, label, Heading(value, 20), null, glyph);

    private static void AddHudCell(Grid grid, int column, string label, UIElement value, ProgressBar? progress, string glyph)
    {
        var copy = new StackPanel { Spacing = 3 };
        var labelRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        labelRow.Children.Add(new FontIcon { Glyph = glyph, FontSize = Font(13) });
        labelRow.Children.Add(new TextBlock { Text = label, FontSize = Font(12) });
        copy.Children.Add(labelRow);
        copy.Children.Add(value);
        if (progress is not null) copy.Children.Add(progress);
        var card = Card(copy, 13);
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
    }

    private UIElement GameHero(GameDefinition game, bool showBest)
    {
        var palette = PaletteFor(game);
        var grid = new Grid { MinHeight = 170 };
        grid.Children.Add(new TextBlock
        {
            Text = "✦     ✧        ✦",
            FontSize = Font(32),
            Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 20, 0),
        });
        var content = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(28) };
        content.Children.Add(new FontIcon { Glyph = game.Glyph, FontSize = Font(44), Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), HorizontalAlignment = HorizontalAlignment.Left });
        content.Children.Add(new TextBlock { Text = game.Title, FontSize = Font(32), FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), TextWrapping = TextWrapping.Wrap });
        if (showBest) content.Children.Add(new TextBlock
        {
            Text = $"{T("Game.Best")}: {_progress.GameBestScores.GetValueOrDefault(game.Id):N0}",
            FontSize = Font(14),
            Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
        });
        grid.Children.Add(content);
        return new Border { Child = grid, CornerRadius = new CornerRadius(8), Background = Gradient(palette) };
    }

    private void AddGameScene(GameDefinition game, UIElement content)
    {
        var grid = new Grid { MinHeight = 400 };
        grid.Children.Add(new TextBlock
        {
            Text = "✦        ✧             ✦       ✧",
            FontSize = Font(34),
            Foreground = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 18, 28, 0),
        });
        grid.Children.Add(content);
        var arena = new Border
        {
            Child = grid,
            CornerRadius = new CornerRadius(8),
            Background = Gradient(PaletteFor(game)),
            Padding = new Thickness(20),
        };
        PageContent.Children.Add(arena);
        AnimateEntrance(arena);
    }

    private static StackPanel GameSceneContent() => new()
    {
        Spacing = 18,
        MaxWidth = 760,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static TextBlock GamePrompt(string text, double size) => new()
    {
        Text = text,
        FontSize = Font(size),
        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
        Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private static TextBlock GameCaption(string text) => new()
    {
        Text = text,
        FontSize = Font(14),
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private static Button GameChoiceButton(string text, GameDefinition game)
    {
        var button = new Button
        {
            Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
            MinHeight = 60,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(16, 12, 16, 12),
            CornerRadius = new CornerRadius(8),
            Background = AppearancePalette.Current.ButtonBrush,
            BorderBrush = AppearancePalette.Current.ButtonBorderBrush,
            BorderThickness = new Thickness(1),
            Foreground = AppearancePalette.Current.ButtonForegroundBrush,
            FontSize = Font(16),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        AutomationProperties.SetName(button, text);
        return button;
    }

    private static Button GameActionButton(string text, string glyph, GameDefinition game)
    {
        var button = GameChoiceButton(text, game);
        button.Content = ButtonContent(text, glyph);
        button.HorizontalAlignment = HorizontalAlignment.Center;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.MaxWidth = 320;
        return button;
    }

    private void StartGameTimer(GameSession session)
    {
        _gameTimer?.Stop();
        _gameTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _gameTimer.Tick += async (_, _) =>
        {
            if (_activeGame != session) { StopGameTimer(); return; }
            if (App.MainWindow is YDKE_Windows.MainWindow { IsWindowMinimized: true }) return;
            session.SecondsRemaining--;
            if (_gameTimerText is not null) _gameTimerText.Text = session.SecondsRemaining.ToString();
            if (_gameTimerProgress is not null) _gameTimerProgress.Value = session.SecondsRemaining;
            if (session.SecondsRemaining <= 0) await CompleteGameAsync(session);
        };
        _gameTimer.Start();
    }

    private void StopGameTimer()
    {
        _gameTimer?.Stop();
        _gameTimer = null;
        _gameTimerText = null;
        _gameTimerProgress = null;
    }

    private static void AnimateEntrance(UIElement element)
    {
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

    private static void AnimatePulse(FrameworkElement element, bool correct)
    {
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

    private async Task RegisterAnswerAsync(bool correct, string answer)
    {
        if (correct)
        {
            _progress.CorrectAnswers++;
            RegisterStudy();
        }
        else _progress.WrongAnswers++;
        await _storage.SaveProgressAsync(_progress);
        ShowNotice(correct ? T("Common.Correct") : T("Common.Wrong"), answer,
            correct ? InfoBarSeverity.Success : InfoBarSeverity.Error);
    }

    private void RenderStats()
    {
        AddPageHeader(T("Stats.Title"), T("Profile.LocalHint"));
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        for (var index = 0; index < 3; index++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        AddStat(grid, 0, T("Stats.Correct"), _progress.CorrectAnswers.ToString("N0"), "");
        AddStat(grid, 1, T("Stats.Wrong"), _progress.WrongAnswers.ToString("N0"), "");
        AddStat(grid, 2, T("Stats.Success"), SuccessRate(), "");
        ConfigureResponsiveGrid(grid, 3, 210);
        PageContent.Children.Add(grid);
        foreach (var (gameId, score) in _progress.GameBestScores.OrderByDescending(pair => pair.Value).Take(10))
        {
            var game = GameCatalog.All.First(item => item.Id == gameId);
            PageContent.Children.Add(Card(Body($"{game.Title}  ·  {score:N0}"), 14));
        }
    }

    private void RenderProfile()
    {
        AddPageHeader(T("Profile.Title"), T("Profile.Subtitle"));
        var quickHint = new StackPanel { Spacing = 8 };
        quickHint.Children.Add(SettingHeader("", T("Profile.QuickSettings"), T("Profile.QuickSettingsHint")));
        PageContent.Children.Add(Card(quickHint, 24));

        var appearance = new StackPanel { Spacing = 18 };
        appearance.Children.Add(SettingHeader(
            "",
            T("Profile.Appearance"),
            T("Profile.AppearanceHint")));

        var fontValue = Body($"{T("Profile.FontSize")}: {_settings.FontScale * 100:0}%");
        var fontSlider = new Slider
        {
            Minimum = 85,
            Maximum = 140,
            StepFrequency = 5,
            Value = Math.Clamp(_settings.FontScale * 100, 85, 140),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(fontSlider, T("Profile.FontSize"));
        fontSlider.ValueChanged += (_, _) =>
            fontValue.Text = $"{T("Profile.FontSize")}: {fontSlider.Value:0}%";
        appearance.Children.Add(SettingHeader("", T("Profile.FontSize"), T("Profile.FontSizeHint")));
        appearance.Children.Add(fontValue);
        appearance.Children.Add(fontSlider);

        var appColor = AppearanceColorSetting(
            T("Profile.AppColor"),
            T("Profile.ColorHint"),
            AppearancePalette.Parse(_settings.AppBackgroundColor, AppearancePalette.DefaultBackground));
        var buttonColor = AppearanceColorSetting(
            T("Profile.ButtonColor"),
            T("Profile.ColorHint"),
            AppearancePalette.Parse(_settings.ButtonColor, AppearancePalette.DefaultButton));
        var boxColor = AppearanceColorSetting(
            T("Profile.BoxColor"),
            T("Profile.ColorHint"),
            AppearancePalette.Parse(_settings.BoxColor, AppearancePalette.DefaultBox));
        appearance.Children.Add(appColor.Row);
        appearance.Children.Add(buttonColor.Row);
        appearance.Children.Add(boxColor.Row);

        var appearanceActions = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        var resetAppearance = SecondaryButton(T("Profile.ResetAppearance"), "");
        resetAppearance.Click += (_, _) =>
        {
            fontSlider.Value = 100;
            appColor.Picker.Color = AppearancePalette.Parse(
                AppearancePalette.DefaultBackground,
                AppearancePalette.DefaultBackground);
            buttonColor.Picker.Color = AppearancePalette.Parse(
                AppearancePalette.DefaultButton,
                AppearancePalette.DefaultButton);
            boxColor.Picker.Color = AppearancePalette.Parse(
                AppearancePalette.DefaultBox,
                AppearancePalette.DefaultBox);
        };
        var applyAppearance = AccentButton(T("Profile.ApplyAppearance"), "");
        applyAppearance.Click += async (_, _) =>
        {
            _settings.FontScale = fontSlider.Value / 100;
            _settings.AppBackgroundColor = AppearancePalette.ToHex(appColor.Picker.Color);
            _settings.ButtonColor = AppearancePalette.ToHex(buttonColor.Picker.Color);
            _settings.BoxColor = AppearancePalette.ToHex(boxColor.Picker.Color);
            await _storage.SaveSettingsAsync(_settings);
            ApplyAppearance();
            RenderCurrentPage();
        };
        appearanceActions.Children.Add(resetAppearance);
        appearanceActions.Children.Add(applyAppearance);
        ConfigureResponsiveGrid(appearanceActions, 2, 190);
        appearance.Children.Add(appearanceActions);
        PageContent.Children.Add(Card(appearance, 24));

        var local = new StackPanel { Spacing = 8 };
        local.Children.Add(SettingHeader("", T("Profile.Local"), T("Profile.LocalHint")));
        local.Children.Add(Body($"{_progress.KnownWords.Count:N0} {T("Home.Known")} · {_progress.FavoriteWords.Count:N0} {T("Home.Favorites")}"));
        PageContent.Children.Add(Card(local, 22));

        var cloud = new StackPanel { Spacing = 12 };
        cloud.Children.Add(SettingHeader("", T("Profile.Cloud"), T("Profile.CloudHint")));
        var optional = SecondaryButton(T("Profile.SignIn"), "");
        optional.Click += (_, _) => ShowNotice(T("Profile.Optional"), T("Profile.CloudHint"), InfoBarSeverity.Informational);
        cloud.Children.Add(optional);
        PageContent.Children.Add(Card(cloud, 22));
    }

    private void RenderInformation(string title, string body, string glyph)
    {
        AddPageHeader(title, "YDKE - Yabancı Dil Kelime Ezberleme");
        var content = new StackPanel { Spacing = 18 };
        content.Children.Add(new FontIcon { Glyph = glyph, FontSize = Font(46), HorizontalAlignment = HorizontalAlignment.Left });
        var text = Body(body);
        text.FontSize = Font(18);
        content.Children.Add(text);
        content.Children.Add(Body("Sürüm 1.0.0 · Windows 11 · .NET 10 · WinUI 3"));
        PageContent.Children.Add(Card(content, 28));
    }

    private void NavigateTo(string page, NavigationViewItem item)
    {
        _currentPage = page;
        Navigation.SelectedItem = item;
        RenderCurrentPage();
    }

    private void AddPageHeader(string title, string subtitle)
    {
        var header = new StackPanel { Spacing = 5, Margin = new Thickness(0, 4, 0, 8) };
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = Font(30),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        header.Children.Add(Body(subtitle));
        PageContent.Children.Add(header);
    }

    private static Border Card(UIElement child, double padding)
    {
        ApplyReadableForeground(child, AppearancePalette.Current.BoxForegroundBrush);
        return new Border
        {
            Child = child,
            Padding = new Thickness(padding),
            CornerRadius = new CornerRadius(8),
            Background = AppearancePalette.Current.BoxBrush,
            BorderBrush = AppearancePalette.Current.BorderBrush,
            BorderThickness = new Thickness(1),
        };
    }

    private static TextBlock Heading(string text, double size) => new()
    {
        Text = text,
        FontSize = Font(size),
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = AppearancePalette.Current.BackgroundForegroundBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    private static TextBlock Body(string text) => new()
    {
        Text = text,
        FontSize = Font(15),
        Foreground = AppearancePalette.Current.BackgroundForegroundBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    private static Button AccentButton(string text, string glyph)
    {
        var button = new Button
        {
            Content = ButtonContent(text, glyph),
            Padding = new Thickness(16, 10, 16, 10),
            Background = AppearancePalette.Current.ButtonBrush,
            BorderBrush = AppearancePalette.Current.ButtonBorderBrush,
            Foreground = AppearancePalette.Current.ButtonForegroundBrush,
            FontSize = Font(14),
        };
        AutomationProperties.SetName(button, text);
        return button;
    }

    private static Button SecondaryButton(string text, string glyph)
    {
        var button = new Button
        {
            Content = ButtonContent(text, glyph),
            Padding = new Thickness(14, 9, 14, 9),
            Background = AppearancePalette.Current.ButtonBrush,
            BorderBrush = AppearancePalette.Current.ButtonBorderBrush,
            Foreground = AppearancePalette.Current.ButtonForegroundBrush,
            FontSize = Font(14),
        };
        AutomationProperties.SetName(button, text);
        return button;
    }

    private static Button ChoiceButton(string text) => new()
    {
        Content = text,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Left,
        Padding = new Thickness(16, 12, 16, 12),
        Background = AppearancePalette.Current.ButtonBrush,
        BorderBrush = AppearancePalette.Current.ButtonBorderBrush,
        Foreground = AppearancePalette.Current.ButtonForegroundBrush,
        FontSize = Font(14),
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
            FontSize = Font(14),
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
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new FontIcon { Glyph = glyph, FontSize = Font(20), VerticalAlignment = VerticalAlignment.Top });
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
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                },
                Padding = new Thickness(9, 4, 9, 4),
                CornerRadius = new CornerRadius(8),
                Background = AppearancePalette.Current.BoxBrush,
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
            Height = 44,
            CornerRadius = new CornerRadius(6),
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
        var content = new StackPanel { Spacing = 7 };
        content.Children.Add(new FontIcon { Glyph = glyph, FontSize = Font(20), HorizontalAlignment = HorizontalAlignment.Left });
        content.Children.Add(Heading(value, 27));
        content.Children.Add(Body(label));
        var card = Card(content, 18);
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
    }

    private string GameDescription(GameDefinition game) => _settings.UiLanguage == "tr" ? game.DescriptionTr : game.DescriptionEn;

    private string LocalizedPart(string value)
    {
        var separator = value.LastIndexOf(" - ", StringComparison.Ordinal);
        if (separator < 0) return value;
        return _settings.UiLanguage == "tr" ? value[(separator + 3)..] : value[..separator];
    }

    private StudyLanguage CurrentStudyLanguage() =>
        VocabularyRepository.Languages.First(language => language.Code == _settings.StudyLanguage);

    private string SuccessRate()
    {
        var total = _progress.CorrectAnswers + _progress.WrongAnswers;
        return total == 0 ? "0%" : $"{(100.0 * _progress.CorrectAnswers / total):0}%";
    }

    private void RegisterStudy()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (_progress.LastStudyDate == today) return;
        _progress.StudyStreak = _progress.LastStudyDate == today.AddDays(-1) ? _progress.StudyStreak + 1 : 1;
        _progress.LastStudyDate = today;
    }

    private static string NormalizeAnswer(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        foreach (var article in new[] { "the ", "a ", "an ", "der ", "die ", "das ", "le ", "la ", "les ", "l'", "el ", "los ", "las ", "il ", "lo ", "gli ", "o ", "os ", "as ", "de ", "het " })
            if (normalized.StartsWith(article, StringComparison.Ordinal)) return normalized[article.Length..];
        return normalized;
    }

    private static void Toggle(HashSet<string> values, string value) { if (!values.Add(value)) values.Remove(value); }

    private static double Font(double size) => Math.Round(size * AppearancePalette.Current.FontScale, 1);

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
        ContentScroll.Opacity = loading ? 0.35 : 1;
    }

    private void ShowNotice(string title, string message, InfoBarSeverity severity)
    {
        Notice.Title = title;
        Notice.Message = message;
        Notice.Severity = severity;
        Notice.IsOpen = true;
    }
}
