using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
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

        _ = GameCatalog.All;
        ApplyNavigationLanguage();
        await ReloadWordsAsync();
        Navigation.SelectedItem = HomeItem;
        RenderCurrentPage();
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

    private async Task EnsureWordsAsync()
    {
        if (_words.Count == 0) await ReloadWordsAsync();
    }

    private async Task ReloadWordsAsync()
    {
        SetLoading(true);
        try
        {
            var language = CurrentStudyLanguage();
            if (!language.Files.ContainsKey(_settings.Level)) _settings.Level = language.Levels[0];
            _words = await _repository.LoadAsync(_settings.StudyLanguage, _settings.Level);
            _cardIndex = 0;
            await _storage.SaveSettingsAsync(_settings);
        }
        catch (Exception ex)
        {
            ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error);
            _words = [];
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void RenderCurrentPage()
    {
        StopGameTimer();
        ContentScroll.ChangeView(null, 0, null, disableAnimation: false);
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

        var stats = new Grid { ColumnSpacing = 12 };
        for (var index = 0; index < 4; index++) stats.ColumnDefinitions.Add(new ColumnDefinition());
        AddStat(stats, 0, T("Home.Known"), _progress.KnownWords.Count.ToString("N0"), "");
        AddStat(stats, 1, T("Home.Favorites"), _progress.FavoriteWords.Count.ToString("N0"), "");
        AddStat(stats, 2, T("Home.Games"), GameCatalog.All.Count.ToString(), "");
        AddStat(stats, 3, T("Stats.Success"), SuccessRate(), "");
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
        AddPageHeader(T("Cards.Title"), $"{CurrentStudyLanguage().NativeName} · {_settings.Level}");
        if (_words.Count == 0) { PageContent.Children.Add(Body(T("Words.Empty"))); return; }

        var entry = _words[_cardIndex % _words.Count];
        var content = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Stretch };
        content.Children.Add(ChipRow(entry));
        content.Children.Add(new TextBlock
        {
            Text = entry.Word,
            FontSize = 42,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 20),
        });

        var definition = Body(LocalizedPart(entry.Definition));
        definition.FontSize = 19;
        definition.TextAlignment = TextAlignment.Center;
        definition.Visibility = Visibility.Collapsed;
        content.Children.Add(definition);
        var example = Body(entry.Example);
        example.TextAlignment = TextAlignment.Center;
        example.Visibility = Visibility.Collapsed;
        content.Children.Add(example);
        var reveal = AccentButton(T("Cards.Reveal"), "");
        reveal.HorizontalAlignment = HorizontalAlignment.Center;
        reveal.Click += (_, _) =>
        {
            definition.Visibility = Visibility.Visible;
            example.Visibility = Visibility.Visible;
            reveal.Visibility = Visibility.Collapsed;
        };
        content.Children.Add(reveal);
        PageContent.Children.Add(Card(content, 32));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };
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
        PageContent.Children.Add(actions);
    }

    private void NextCard()
    {
        _cardIndex = (_cardIndex + 1) % Math.Max(_words.Count, 1);
        RenderCards();
    }

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
                await RegisterAnswerAsync(choice.Key == answer.Key, null, answer.Word);
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
            MaxWidth = 620,
            HorizontalAlignment = HorizontalAlignment.Left,
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
                var row = new Grid { ColumnSpacing = 16 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.Children.Add(Heading(entry.Word, 17));
                var meaning = Body(LocalizedPart(entry.Definition));
                Grid.SetColumn(meaning, 1);
                row.Children.Add(meaning);
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
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var index = 0; index < games.Count; index++)
        {
            var row = index / 2;
            while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var button = GameButton(games[index]);
            Grid.SetRow(button, row);
            Grid.SetColumn(button, index % 2);
            grid.Children.Add(button);
        }
        PageContent.Children.Add(grid);
    }

    private Button GameButton(GameDefinition game)
    {
        var palette = PaletteFor(game);
        var layout = new Grid { ColumnSpacing = 16 };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.Children.Add(new Border
        {
            Width = 46,
            Height = 46,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(palette.Accent),
            Child = new FontIcon { Glyph = game.Glyph, FontSize = 23, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) },
        });
        var copy = new StackPanel { Spacing = 5 };
        copy.Children.Add(Heading(game.Title, 18));
        copy.Children.Add(Body(GameDescription(game)));
        Grid.SetColumn(copy, 1);
        layout.Children.Add(copy);
        var best = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        best.Children.Add(new TextBlock { Text = T("Game.Best"), FontSize = 11, Opacity = 0.65, HorizontalAlignment = HorizontalAlignment.Right });
        best.Children.Add(Heading(_progress.GameBestScores.GetValueOrDefault(game.Id).ToString("N0"), 18));
        Grid.SetColumn(best, 2);
        layout.Children.Add(best);
        var button = new Button
        {
            Content = layout,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(18),
            MinHeight = 112,
            CornerRadius = new CornerRadius(8),
            Background = ResourceBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
        };
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
            FontSize = 20,
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
        var actions = new Grid { ColumnSpacing = 12 };
        actions.ColumnDefinitions.Add(new ColumnDefinition());
        actions.ColumnDefinitions.Add(new ColumnDefinition());
        var column = 0;
        foreach (var answer in new[] { true, false })
        {
            var button = GameChoiceButton(answer ? "✓  TRUE" : "✕  FALSE", session.Game);
            Grid.SetColumn(button, column++);
            button.Click += async (_, _) =>
            {
                await ResolveGameAnswerAsync(session, answer == isTrue, entry.Word, button);
            };
            actions.Children.Add(button);
        }
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
        choicesGrid.ColumnDefinitions.Add(new ColumnDefinition());
        choicesGrid.ColumnDefinitions.Add(new ColumnDefinition());
        choicesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        choicesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var index = 0; index < choices.Length; index++)
        {
            var choice = choices[index];
            var button = GameChoiceButton($"{index + 1}   {choice.Word}", session.Game);
            Grid.SetColumn(button, index % 2);
            Grid.SetRow(button, index / 2);
            button.Click += async (_, _) =>
            {
                await ResolveGameAnswerAsync(session, choice.Key == entry.Key, entry.Word, button);
            };
            choicesGrid.Children.Add(button);
        }
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
        for (var column = 0; column < 9; column++) keyboard.ColumnDefinitions.Add(new ColumnDefinition());
        for (var row = 0; row < 3; row++) keyboard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        void Refresh()
        {
            word.Text = string.Join(' ', target.Select(character => !char.IsLetter(character) || guessed.Contains(character) ? character : '_'));
            missText.Text = $"{T("Game.Lives")}: {Math.Max(0, 6 - misses)}  ·  {string.Join(' ', guessed.Order())}";
        }
        foreach (var (letter, index) in "ABCDEFGHIJKLMNOPQRSTUVWXYZ".Select((letter, index) => (letter, index)))
        {
            var button = GameChoiceButton(letter.ToString(), session.Game);
            button.MinHeight = 42;
            button.Padding = new Thickness(4);
            Grid.SetColumn(button, index % 9);
            Grid.SetRow(button, index / 9);
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
        for (var column = 0; column < 4; column++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var row = 0; row < 3; row++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(82) });
        Button? firstButton = null;
        VocabularyEntry? firstEntry = null;
        var matches = 0;
        for (var index = 0; index < tiles.Length; index++)
        {
            var tile = tiles[index];
            var button = GameChoiceButton("✦", session.Game);
            Grid.SetColumn(button, index % 4);
            Grid.SetRow(button, index / 4);
            button.Click += async (_, _) =>
            {
                if (!button.IsEnabled || ReferenceEquals(button, firstButton)) return;
                button.Content = tile.Text;
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
        var input = new TextBox { MaxLength = target.Length, FontSize = 20, MaxWidth = 420, PlaceholderText = T("Game.TypeAnswer") };
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
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        for (var index = 0; index < guess.Length; index++)
        {
            var color = guess[index] == target[index]
                ? Color.FromArgb(255, 16, 185, 129)
                : target.Contains(guess[index]) ? Color.FromArgb(255, 245, 158, 11) : Color.FromArgb(150, 71, 85, 105);
            row.Children.Add(new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(color),
                Child = new TextBlock
                {
                    Text = guess[index].ToString(),
                    FontSize = 20,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            });
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
        await RegisterAnswerAsync(correct, session.Game, answer);
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
        var grid = new Grid { ColumnSpacing = 10 };
        for (var index = 0; index < 4; index++) grid.ColumnDefinitions.Add(new ColumnDefinition());
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
        return grid;
    }

    private static void AddHudCell(Grid grid, int column, string label, string value, string glyph) =>
        AddHudCell(grid, column, label, Heading(value, 20), null, glyph);

    private static void AddHudCell(Grid grid, int column, string label, UIElement value, ProgressBar? progress, string glyph)
    {
        var copy = new StackPanel { Spacing = 3 };
        var labelRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        labelRow.Children.Add(new FontIcon { Glyph = glyph, FontSize = 13 });
        labelRow.Children.Add(new TextBlock { Text = label, FontSize = 12, Opacity = 0.7 });
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
            FontSize = 32,
            Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 20, 0),
        });
        var content = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(28) };
        content.Children.Add(new FontIcon { Glyph = game.Glyph, FontSize = 44, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), HorizontalAlignment = HorizontalAlignment.Left });
        content.Children.Add(new TextBlock { Text = game.Title, FontSize = 32, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });
        if (showBest) content.Children.Add(new TextBlock
        {
            Text = $"{T("Game.Best")}: {_progress.GameBestScores.GetValueOrDefault(game.Id):N0}",
            FontSize = 14,
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
            FontSize = 34,
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
            Padding = new Thickness(30),
        };
        PageContent.Children.Add(arena);
        AnimateEntrance(arena);
    }

    private static StackPanel GameSceneContent() => new()
    {
        Spacing = 18,
        MaxWidth = 760,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static TextBlock GamePrompt(string text, double size) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
        Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private static TextBlock GameCaption(string text) => new()
    {
        Text = text,
        FontSize = 14,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private static Button GameChoiceButton(string text, GameDefinition game) => new()
    {
        Content = text,
        MinHeight = 60,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Left,
        Padding = new Thickness(16, 12, 16, 12),
        CornerRadius = new CornerRadius(8),
        Background = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
        BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
        BorderThickness = new Thickness(1),
        Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
        FontSize = 16,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
    };

    private static Button GameActionButton(string text, string glyph, GameDefinition game)
    {
        var button = GameChoiceButton(text, game);
        button.Content = ButtonContent(text, glyph);
        button.HorizontalAlignment = HorizontalAlignment.Center;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.MinWidth = 180;
        return button;
    }

    private void StartGameTimer(GameSession session)
    {
        _gameTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _gameTimer.Tick += async (_, _) =>
        {
            if (_activeGame != session) { StopGameTimer(); return; }
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
            control.Background = new SolidColorBrush(correct
                ? Color.FromArgb(230, 16, 185, 129)
                : Color.FromArgb(230, 244, 63, 94));
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
            new GamePalette(Color.FromArgb(255, 8, 145, 178), Color.FromArgb(255, 37, 99, 235), Color.FromArgb(255, 6, 182, 212)),
            new GamePalette(Color.FromArgb(255, 5, 150, 105), Color.FromArgb(255, 15, 118, 110), Color.FromArgb(255, 16, 185, 129)),
            new GamePalette(Color.FromArgb(255, 190, 24, 93), Color.FromArgb(255, 234, 88, 12), Color.FromArgb(255, 244, 63, 94)),
            new GamePalette(Color.FromArgb(255, 30, 64, 175), Color.FromArgb(255, 14, 116, 144), Color.FromArgb(255, 59, 130, 246)),
            new GamePalette(Color.FromArgb(255, 161, 98, 7), Color.FromArgb(255, 194, 65, 12), Color.FromArgb(255, 245, 158, 11)),
        };
        var index = Math.Abs(game.Id.Aggregate(0, (value, character) => value + character)) % palettes.Length;
        return palettes[index];
    }

    private sealed record GamePalette(Color Start, Color End, Color Accent);

    private async Task RegisterAnswerAsync(bool correct, GameDefinition? game, string answer)
    {
        if (correct)
        {
            _progress.CorrectAnswers++;
            if (game is not null) _progress.GameBestScores[game.Id] = _progress.GameBestScores.GetValueOrDefault(game.Id) + 1;
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
        var grid = new Grid { ColumnSpacing = 12 };
        for (var index = 0; index < 3; index++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        AddStat(grid, 0, T("Stats.Correct"), _progress.CorrectAnswers.ToString("N0"), "");
        AddStat(grid, 1, T("Stats.Wrong"), _progress.WrongAnswers.ToString("N0"), "");
        AddStat(grid, 2, T("Stats.Success"), SuccessRate(), "");
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
        var languageCard = new StackPanel { Spacing = 18 };
        languageCard.Children.Add(SettingHeader("", T("Profile.UiLanguage"), T("Profile.UiLanguageHint")));
        var uiLanguage = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = Localizer.UiLanguages,
            DisplayMemberPath = nameof(LanguageOption.NativeName),
            SelectedValuePath = nameof(LanguageOption.Code),
            SelectedValue = _settings.UiLanguage,
        };
        uiLanguage.SelectionChanged += async (_, _) =>
        {
            if (uiLanguage.SelectedValue is not string code || code == _settings.UiLanguage) return;
            _settings.UiLanguage = code;
            await _storage.SaveSettingsAsync(_settings);
            ApplyNavigationLanguage();
            RenderProfile();
        };
        languageCard.Children.Add(uiLanguage);

        languageCard.Children.Add(SettingHeader("", T("Profile.StudyLanguage"), T("Profile.StudyLanguageHint")));
        var studyLanguage = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = VocabularyRepository.Languages,
            DisplayMemberPath = nameof(StudyLanguage.NativeName),
            SelectedValuePath = nameof(StudyLanguage.Code),
            SelectedValue = _settings.StudyLanguage,
        };
        studyLanguage.SelectionChanged += async (_, _) =>
        {
            if (studyLanguage.SelectedValue is not string code || code == _settings.StudyLanguage) return;
            _settings.StudyLanguage = code;
            _settings.Level = "A1";
            await ReloadWordsAsync();
            RenderProfile();
        };
        languageCard.Children.Add(studyLanguage);

        languageCard.Children.Add(SettingHeader("", T("Profile.Level"), T("Profile.StudyLanguageHint")));
        var level = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = CurrentStudyLanguage().Levels,
            SelectedItem = _settings.Level,
        };
        level.SelectionChanged += async (_, _) =>
        {
            if (level.SelectedItem is not string selected || selected == _settings.Level) return;
            _settings.Level = selected;
            await ReloadWordsAsync();
            RenderProfile();
        };
        languageCard.Children.Add(level);
        PageContent.Children.Add(Card(languageCard, 24));

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
        content.Children.Add(new FontIcon { Glyph = glyph, FontSize = 46, HorizontalAlignment = HorizontalAlignment.Left });
        var text = Body(body);
        text.FontSize = 18;
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
            FontSize = 30,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        header.Children.Add(Body(subtitle));
        PageContent.Children.Add(header);
    }

    private static Border Card(UIElement child, double padding) => new()
    {
        Child = child,
        Padding = new Thickness(padding),
        CornerRadius = new CornerRadius(8),
        Background = ResourceBrush("CardBackgroundFillColorDefaultBrush"),
        BorderBrush = ResourceBrush("CardStrokeColorDefaultBrush"),
        BorderThickness = new Thickness(1),
    };

    private static TextBlock Heading(string text, double size) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        TextWrapping = TextWrapping.Wrap,
    };

    private static TextBlock Body(string text) => new()
    {
        Text = text,
        FontSize = 15,
        Opacity = 0.86,
        TextWrapping = TextWrapping.Wrap,
    };

    private static Button AccentButton(string text, string glyph) => new()
    {
        Content = ButtonContent(text, glyph),
        Style = Application.Current.Resources["AccentButtonStyle"] as Style,
        Padding = new Thickness(16, 10, 16, 10),
    };

    private static Button SecondaryButton(string text, string glyph) => new()
    {
        Content = ButtonContent(text, glyph),
        Padding = new Thickness(14, 9, 14, 9),
    };

    private static Button ChoiceButton(string text) => new()
    {
        Content = text,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Left,
        Padding = new Thickness(16, 12, 16, 12),
    };

    private static StackPanel ButtonContent(string text, string glyph)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(new FontIcon { Glyph = glyph, FontSize = 16 });
        panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    private static UIElement SettingHeader(string glyph, string title, string hint)
    {
        var grid = new Grid { ColumnSpacing = 14 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new FontIcon { Glyph = glyph, FontSize = 20, VerticalAlignment = VerticalAlignment.Top });
        var copy = new StackPanel { Spacing = 3 };
        copy.Children.Add(Heading(title, 17));
        copy.Children.Add(Body(hint));
        Grid.SetColumn(copy, 1);
        grid.Children.Add(copy);
        return grid;
    }

    private static UIElement ChipRow(VocabularyEntry entry)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var value in new[] { entry.LanguageCode.ToUpperInvariant(), entry.Level, entry.PartOfSpeech })
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            row.Children.Add(new Border
            {
                Child = new TextBlock { Text = value, FontSize = 12 },
                Padding = new Thickness(9, 4, 9, 4),
                CornerRadius = new CornerRadius(8),
                Background = ResourceBrush("SubtleFillColorSecondaryBrush"),
            });
        }
        return row;
    }

    private static void AddStat(Grid grid, int column, string label, string value, string glyph)
    {
        var content = new StackPanel { Spacing = 7 };
        content.Children.Add(new FontIcon { Glyph = glyph, FontSize = 20, HorizontalAlignment = HorizontalAlignment.Left });
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

    private static Brush ResourceBrush(string key) =>
        Application.Current.Resources[key] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.Transparent);

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
