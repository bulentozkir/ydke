using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using Windows.UI;

namespace YDKE_Windows;

public sealed partial class MainPage
{
    private string _libraryQuery = "";
    private string? _libraryCategory;
    private bool _libraryFavorites;
    private bool _libraryKnown;
    private bool _libraryDue;
    private int _libraryPage;
    private string? _libraryContext;
    private StatsSection _statsSection;
    private int _statsGamePage;

    private enum StatsSection { Overview, Answers, History }

    private string MarkedKnownLabel => U("Known.Marked", "Marked known", "Biliniyor olarak işaretli");

    private string FavoriteLabel(string key) => _progress.FavoriteWords.Contains(key)
        ? U("Favorite.Remove", "★ Favorited — remove", "★ Favori — kaldır")
        : U("Favorite.Add", "☆ Add favorite", "☆ Favorilere ekle");

    private string KnownLabel(string key) => _progress.KnownWords.Contains(key)
        ? U("Known.Remove", "✓ Marked known — unmark (U)", "✓ Biliniyor olarak işaretli — kaldır (U)")
        : U("Known.Add", "Mark known (U)", "Biliniyor olarak işaretle (U)");

    private Button FavoriteButton(VocabularyEntry entry, Action? refresh = null)
    {
        var button = SecondaryButton(FavoriteLabel(entry.Key), "");
        FocusTarget(button, "favorite." + entry.Key);
        button.Click += async (_, _) =>
        {
            if (!await MutateStudyAsync(() => Toggle(_progress.FavoriteWords, entry.Key))) return;
            button.Content = ButtonContent(FavoriteLabel(entry.Key), "");
            AutomationProperties.SetName(button, FavoriteLabel(entry.Key));
            refresh?.Invoke(); // Cards update in place, preserving reveal, focus and audio.
        };
        return button;
    }

    private Button KnownButton(VocabularyEntry entry, Action? refresh = null)
    {
        var button = SecondaryButton(KnownLabel(entry.Key), "");
        FocusTarget(button, "known." + entry.Key);
        button.Click += async (_, _) =>
        {
            if (!await MutateStudyAsync(() => Toggle(_progress.KnownWords, entry.Key))) return;
            button.Content = ButtonContent(KnownLabel(entry.Key), "");
            AutomationProperties.SetName(button, KnownLabel(entry.Key));
            refresh?.Invoke();
        };
        return button;
    }

    private void RenderWords()
    {
        if (_libraryContext != StudyContext)
        {
            _libraryContext = StudyContext;
            _libraryCategory = null;
            _libraryPage = 0;
        }
        var context = StudyContext;
        var wordBatch = _words;
        var palette = AppearancePalette.Current;
        AddPageHeader(T("Words.Title"), $"{CurrentStudyLanguage().NativeName} · {_settings.Level}");
        var search = new AutoSuggestBox
        {
            PlaceholderText = T("Words.Search"), QueryIcon = new SymbolIcon(Symbol.Find), Text = _libraryQuery,
            FontSize = ReadingSize(18), MinHeight = 48, MinWidth = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(search, T("Words.Search"));
        FocusTarget(search, "library.Search");
        var clearLabel = U("Library.ClearFilters", "Clear filters", "Filtreleri temizle");
        var clearFilters = SecondaryButton(clearLabel, "");
        clearFilters.Padding = new Thickness(12, 8, 12, 8);
        clearFilters.HorizontalAlignment = HorizontalAlignment.Left;
        clearFilters.VerticalAlignment = VerticalAlignment.Center;
        FocusTarget(clearFilters, "library.ClearFilters");
        var searchRow = new Grid { ColumnSpacing = 8 };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition());
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        searchRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(clearFilters, 1);
        searchRow.Children.Add(search);
        searchRow.Children.Add(clearFilters);
        searchRow.SizeChanged += (_, args) =>
        {
            var stacked = args.NewSize.Width < Font(420);
            Grid.SetColumn(clearFilters, stacked ? 0 : 1);
            Grid.SetRow(clearFilters, stacked ? 1 : 0);
            Grid.SetColumnSpan(search, stacked ? 2 : 1);
            Grid.SetColumnSpan(clearFilters, stacked ? 2 : 1);
            searchRow.RowSpacing = stacked ? 8 : 0;
        };
        PageContent.Children.Add(searchRow);

        CheckBox Filter(string label, bool selected, string id)
        {
            var filter = new CheckBox
            {
                Content = Body(label), IsChecked = selected,
                FontSize = ReadingSize(18), MinHeight = 48, MinWidth = 48,
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            AutomationProperties.SetName(filter, label);
            FocusTarget(filter, id);
            return filter;
        }
        var filters = new Grid { ColumnSpacing = 10, RowSpacing = 8 };
        var favorite = Filter(T("Home.Favorites"), _libraryFavorites, "library.Filter.Favorites");
        var known = Filter(MarkedKnownLabel, _libraryKnown, "library.Filter.Known");
        var due = Filter(U("Study.Due", "Due", "Tekrar zamanı"), _libraryDue, "library.Filter.Due");
        filters.Children.Add(favorite);
        filters.Children.Add(known);
        filters.Children.Add(due);
        var categoryLabel = U("Library.Category", "Category", "Kategori");
        var categories = new ComboBox { Header = Body(categoryLabel), HorizontalAlignment = HorizontalAlignment.Stretch };
        ConfigureReadingComboBox(categories);
        AutomationProperties.SetName(categories, categoryLabel);
        FocusTarget(categories, "library.Category");
        categories.Items.Add(new ComboBoxItem { Content = U("Library.AllCategories", "All categories", "Tüm kategoriler"), Tag = "" });
        foreach (var category in wordBatch.Select(word => word.Category).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().Order())
            categories.Items.Add(new ComboBoxItem { Content = category, Tag = category });
        categories.SelectedItem = categories.Items.OfType<ComboBoxItem>().FirstOrDefault(item => item.Tag as string == _libraryCategory) ?? categories.Items[0];
        filters.Children.Add(categories);
        ConfigureResponsiveGrid(filters, 4, 140);
        PageContent.Children.Add(filters);
        var count = Body("");
        StudyLive(count, "library.ResultCount");
        var practice = AccentButton(U("Library.Practice", "Practice results (up to 8)", "Sonuçları çalış (en fazla 8)"), "");
        FocusTarget(practice, "library.Practice");
        var previous = SecondaryButton(U("Library.Previous", "Previous word", "Önceki kelime"), "");
        FocusTarget(previous, "library.Previous");
        var next = SecondaryButton(U("Library.Next", "Next word", "Sonraki kelime"), "");
        FocusTarget(next, "library.Next");
        var browserActions = new Grid { ColumnSpacing = 8, RowSpacing = 8, Children = { count, practice, previous, next } };
        ConfigureResponsiveGrid(browserActions, 4, 130);
        PageContent.Children.Add(browserActions);
        var emptyContent = new StackPanel { Spacing = 8 };
        var emptyTitle = StudyText(U("Library.Empty", "No matching words", "Eşleşen kelime yok"), 18, emphasis: true);
        AutomationProperties.SetAutomationId(emptyTitle, "library.Empty");
        emptyContent.Children.Add(emptyTitle);
        emptyContent.Children.Add(StudyText(U("Library.EmptyHint", "Try a different search, clear the filters, or choose another study language or level.", "Farklı bir arama deneyin, filtreleri temizleyin veya başka bir öğrenme dili ya da seviye seçin."), 14));
        var emptyReset = SecondaryButton(clearLabel, "");
        FocusTarget(emptyReset, "library.Empty.ClearFilters");
        emptyContent.Children.Add(emptyReset);
        var empty = StudySurface(emptyContent, 16);
        empty.Visibility = Visibility.Collapsed;
        PageContent.Children.Add(empty);
        var list = new StackPanel { Spacing = 8 };
        PageContent.Children.Add(list);
        VocabularyEntry[] matches = [];
        var searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        var searchPending = false;
        var searchDeadline = 0L;
        var unloaded = false;
        var syncingFilters = false;

        bool IsCurrentRender() => !unloaded && _currentPage == "words" && _libraryContext == context && StudyContext == context &&
            ReferenceEquals(wordBatch, _words) && PageContent.Children.Contains(searchRow);

        void Refresh(bool resetPage = false, string? changedKey = null, string? action = null)
        {
            if (!IsCurrentRender()) return;
            resetPage |= searchPending;
            searchTimer.Stop();
            searchPending = false;
            if (resetPage) _libraryPage = 0;
            var dueKeys = _libraryDue ? LearningEngine.DueWords(wordBatch, _progress, Today).Select(word => word.Key).ToHashSet() : null;
            var query = _libraryQuery;
            var hasQuery = !string.IsNullOrWhiteSpace(query);
            matches = wordBatch.Where(word =>
                (!_libraryFavorites || _progress.FavoriteWords.Contains(word.Key)) &&
                (!_libraryKnown || _progress.KnownWords.Contains(word.Key)) &&
                (dueKeys is null || dueKeys.Contains(word.Key)) &&
                (string.IsNullOrEmpty(_libraryCategory) || word.Category == _libraryCategory) &&
                (!hasQuery || word.Word.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    word.Definition.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    word.Category.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
                .DistinctBy(word => word.Key).ToArray();
            _libraryPage = matches.Length == 0 ? 0 : Math.Clamp(_libraryPage, 0, matches.Length - 1);
            string? focusId = null;
            if (changedKey is not null)
            {
                var retained = matches.Length > 0 && matches[_libraryPage].Key == changedKey;
                focusId = resetPage || !retained ? "library.Search" : action is not null ? action + "." + changedKey : "library.Next";
            }
            if (focusId is not null) RequestUiFocus(focusId);
            list.Children.Clear();
            Announce(count, string.Format(System.Globalization.CultureInfo.CurrentCulture,
                U("Library.BrowseCount", "{0:N0} matches · word {1:N0} of {0:N0}", "{0:N0} eşleşme · {0:N0} kelimeden {1:N0}."), matches.Length, matches.Length == 0 ? 0 : _libraryPage + 1));
            empty.Visibility = matches.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            previous.IsEnabled = _libraryPage > 0;
            next.IsEnabled = _libraryPage + 1 < matches.Length;
            practice.IsEnabled = matches.Length > 0;
            foreach (var entry in matches.Skip(_libraryPage).Take(1))
            {
                var definition = LocalizedPart(entry.Definition);
                var header = new StackPanel { Spacing = 4 };
                header.Children.Add(StudyText(entry.Word, 18, emphasis: true));
                header.Children.Add(StudyText(definition, 14));
                var details = new StackPanel { Spacing = 8 };
                details.Children.Add(StudyText($"{entry.Category} · {entry.Level} · {entry.PartOfSpeech}", 14));
                details.Children.Add(StudyText(entry.Example, 15, selectable: true));
                var actions = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
                var audio = SecondaryButton(T("Cards.Listen"), "");
                FocusTarget(audio, "library.Listen." + entry.Key);
                AutomationProperties.SetName(audio, $"{T("Cards.Listen")}: {entry.Word}");
                audio.Click += async (_, _) => { if (IsCurrentRender()) await PlayWordAsync(entry.Word, audio); };
                actions.Children.Add(audio);
                var markFavorite = FavoriteButton(entry, () => Refresh(changedKey: entry.Key, action: "favorite"));
                AutomationProperties.SetName(markFavorite, $"{FavoriteLabel(entry.Key)}: {entry.Word}");
                actions.Children.Add(markFavorite);
                var markKnown = KnownButton(entry, () => Refresh(changedKey: entry.Key, action: "known"));
                AutomationProperties.SetName(markKnown, $"{KnownLabel(entry.Key)}: {entry.Word}");
                actions.Children.Add(markKnown);
                ConfigureResponsiveGrid(actions, 3, 155);
                details.Children.Add(actions);
                var expander = new Expander
                {
                    Header = header, Content = StudySurface(details, 14), IsExpanded = true, Tag = entry.Key,
                    FontSize = ReadingSize(18), MinHeight = 48, MinWidth = 48,
                    Background = palette.BoxBrush, Foreground = palette.BoxForegroundBrush,
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                };
                foreach (var state in new[] { "", "PointerOver", "Pressed", "Disabled" })
                {
                    expander.Resources[$"ExpanderHeaderBackground{state}"] = palette.BoxBrush;
                    expander.Resources[$"ExpanderHeaderForeground{state}"] = palette.BoxForegroundBrush;
                }
                expander.Resources["ExpanderContentBackground"] = palette.BoxBrush;
                expander.Resources["ExpanderContentBorderBrush"] = palette.BorderBrush;
                AutomationProperties.SetName(expander, $"{entry.Word} · {definition}");
                FocusTarget(expander, "library.Word." + entry.Key);
                expander.KeyDown += async (_, args) =>
                {
                    if (!IsCurrentRender() || args.Key != Windows.System.VirtualKey.U || args.KeyStatus.WasKeyDown || _studyBusy || _dialogOpen || _navigationBusy) return;
                    foreach (var modifier in new[] { Windows.System.VirtualKey.Control, Windows.System.VirtualKey.Menu, Windows.System.VirtualKey.Shift })
                        if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(modifier).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) return;
                    args.Handled = true;
                    if (await MutateStudyAsync(() => Toggle(_progress.KnownWords, entry.Key))) Refresh(changedKey: entry.Key, action: "known");
                };
                list.Children.Add(expander);
            }
            if (focusId == "library.Search") FocusTarget(search, "library.Search");
        }

        void SearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (syncingFilters || !IsCurrentRender() || args.Reason != AutoSuggestionBoxTextChangeReason.UserInput || _libraryQuery == sender.Text) return;
            _libraryQuery = sender.Text;
            _libraryPage = 0;
            searchTimer.Stop();
            searchDeadline = Environment.TickCount64 + 180;
            if (!searchPending) Announce(count, U("Library.SearchPending", "Searching…", "Aranıyor…"));
            searchPending = true;
            practice.IsEnabled = previous.IsEnabled = next.IsEnabled = false;
            empty.Visibility = Visibility.Collapsed;
            searchTimer.Interval = TimeSpan.FromMilliseconds(180);
            searchTimer.Start();
        }
        void SearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (!IsCurrentRender() || (!searchPending && _libraryQuery == args.QueryText)) return;
            _libraryQuery = args.QueryText;
            _libraryPage = 0;
            Refresh(resetPage: true);
        }
        void SearchTick(object? sender, object args)
        {
            searchTimer.Stop();
            if (!searchPending || !IsCurrentRender() || _studyBusy) return;
            // Stop can leave a queued tick; honor the latest keystroke's deadline.
            var remaining = searchDeadline - Environment.TickCount64;
            if (remaining > 0)
            {
                searchTimer.Interval = TimeSpan.FromMilliseconds(remaining);
                searchTimer.Start();
                return;
            }
            Refresh(resetPage: true);
        }
        // Resume after a save without polling or displaying tentative bookmark changes.
        long? busyChangedToken = null;
        void SearchLoaded(object sender, RoutedEventArgs args)
        {
            if (!IsCurrentRender() || busyChangedToken is not null) return;
            busyChangedToken = ContentScroll.RegisterPropertyChangedCallback(Control.IsEnabledProperty, (_, _) =>
            {
                if (searchPending && ContentScroll.IsEnabled && !searchTimer.IsEnabled && IsCurrentRender())
                {
                    searchTimer.Interval = TimeSpan.FromMilliseconds(1);
                    searchTimer.Start();
                }
            });
        }
        void FilterChanged(object sender, RoutedEventArgs args)
        {
            if (syncingFilters || !IsCurrentRender()) return;
            var favoritesOnly = favorite.IsChecked == true;
            var knownOnly = known.IsChecked == true;
            var dueOnly = due.IsChecked == true;
            if (_libraryFavorites == favoritesOnly && _libraryKnown == knownOnly && _libraryDue == dueOnly) return;
            _libraryFavorites = favoritesOnly;
            _libraryKnown = knownOnly;
            _libraryDue = dueOnly;
            _libraryPage = 0;
            Refresh(resetPage: true);
        }
        void CategoryChanged(object sender, SelectionChangedEventArgs args)
        {
            if (syncingFilters || !IsCurrentRender()) return;
            var category = (categories.SelectedItem as ComboBoxItem)?.Tag as string;
            if (string.IsNullOrEmpty(category)) category = null;
            if (_libraryCategory == category) return;
            _libraryCategory = category;
            _libraryPage = 0;
            Refresh(resetPage: true);
        }
        void ResetFilters()
        {
            if (!IsCurrentRender()) return;
            RequestUiFocus("library.Search");
            syncingFilters = true;
            try
            {
                _libraryQuery = "";
                _libraryCategory = null;
                _libraryFavorites = _libraryKnown = _libraryDue = false;
                _libraryPage = 0;
                search.Text = "";
                favorite.IsChecked = known.IsChecked = due.IsChecked = false;
                categories.SelectedIndex = 0;
            }
            finally { syncingFilters = false; }
            Refresh(resetPage: true);
            FocusTarget(search, "library.Search");
        }
        void SearchUnloaded(object sender, RoutedEventArgs args)
        {
            unloaded = true;
            searchPending = false;
            searchTimer.Stop();
            searchTimer.Tick -= SearchTick;
            search.TextChanged -= SearchChanged;
            search.QuerySubmitted -= SearchSubmitted;
            search.Loaded -= SearchLoaded;
            search.Unloaded -= SearchUnloaded;
            if (busyChangedToken is { } token) ContentScroll.UnregisterPropertyChangedCallback(Control.IsEnabledProperty, token);
            busyChangedToken = null;
            foreach (var filter in new[] { favorite, known, due }) { filter.Checked -= FilterChanged; filter.Unchecked -= FilterChanged; }
            categories.SelectionChanged -= CategoryChanged;
        }
        searchTimer.Tick += SearchTick;
        search.TextChanged += SearchChanged;
        search.QuerySubmitted += SearchSubmitted;
        search.Loaded += SearchLoaded;
        search.Unloaded += SearchUnloaded;
        foreach (var filter in new[] { favorite, known, due }) { filter.Checked += FilterChanged; filter.Unchecked += FilterChanged; }
        categories.SelectionChanged += CategoryChanged;
        clearFilters.Click += (_, _) => ResetFilters();
        emptyReset.Click += (_, _) => ResetFilters();
        previous.Click += (_, _) => { if (!searchPending && IsCurrentRender() && _libraryPage > 0) { _libraryPage--; Refresh(); } };
        next.Click += (_, _) => { if (!searchPending && IsCurrentRender() && _libraryPage + 1 < matches.Length) { _libraryPage++; Refresh(); } };
        practice.Click += async (_, _) =>
        {
            if (!searchPending && IsCurrentRender() && matches.Length > 0) await StartStudySessionAsync("quiz", matches);
        };
        Refresh();
    }

    private void RenderHome()
    {
        AddPageHeader(U("Kids.Home.Title", "Let's learn some words!", "Hadi kelime öğrenelim!"), U("Kids.Home.Hint", "Pick cards, a quiz, or a game. Take your time.", "Kartları, testi veya bir oyunu seç. Acele etme."));
        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        AddStat(grid, 0, U("Kids.Home.PracticeAgain", "Words to try again", "Tekrar çalışılacak kelimeler"), LearningEngine.DueWords(_words, _progress, Today).Count.ToString("N0"), "");
        AddStat(grid, 1, MarkedKnownLabel, _words.Count(word => _progress.KnownWords.Contains(word.Key)).ToString("N0"), "");
        AddStat(grid, 2, T("Home.Favorites"), _words.Count(word => _progress.FavoriteWords.Contains(word.Key)).ToString("N0"), "");
        ConfigureResponsiveGrid(grid, 3, 170);
        var today = _progress.DailyReviewedWords.GetValueOrDefault(DayKey(Today))?.Count ?? 0;
        var suite = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        foreach (var mode in new[] { "cards", "quiz" })
        {
            var panel = new StackPanel { Spacing = 10 };
            panel.Children.Add(Heading(mode == "cards" ? T("Nav.Cards") : T("Nav.Quiz"), 22));
            var session = Session(mode);
            panel.Children.Add(Body(session is null
                ? U("Kids.Home.Ready", "A few words at a time. Let's try!", "Bir seferde birkaç kelime. Hadi deneyelim!")
                : $"{U("Session.Progress", "Completed", "Tamamlanan")}: {session.Answers.Count} / {session.WordKeys.Count}"));
            var button = AccentButton(session is not null && session.Index < session.WordKeys.Count
                ? U("Home.Resume", "Resume saved session", "Kayıtlı oturuma devam") : T("Home.Continue"), "");
            button.Click += (_, _) => NavigateTo(mode, mode == "cards" ? CardsItem : QuizItem);
            panel.Children.Add(button);
            suite.Children.Add(Card(panel, 22));
        }
        ConfigureResponsiveGrid(suite, 2, 320);
        PageContent.Children.Add(suite);
        var play = AccentButton(U("Kids.Home.Play", "Play a word game", "Kelime oyunu oyna"), "\uE768");
        AutomationProperties.SetAutomationId(play, "home.Play");
        play.Click += (_, _) => NavigateTo("simple-games", SimpleGamesItem);
        var myWords = StudyPopupButton(U("Kids.Home.MyWords", "My words", "Kelimelerim"), grid, "home.MyWords");
        var settings = SecondaryButton(U("Kids.Home.Settings", "My settings", "Ayarlarım"), "");
        settings.Click += (_, _) => NavigateTo("profile", ProfileItem);
        var actions = new Grid { ColumnSpacing = 10, RowSpacing = 10, Children = { play, myWords, settings } };
        ConfigureResponsiveGrid(actions, 3, 190);
        PageContent.Children.Add(actions);
        var daily = new StackPanel { Spacing = 6 };
        daily.Children.Add(Heading($"{U("Kids.Home.Today", "Words practiced today", "Bugün çalıştığın kelimeler")}: {today} / {_settings.DailyGoal}", 20));
        daily.Children.Add(new ProgressBar { Minimum = 0, Maximum = _settings.DailyGoal, Value = Math.Min(today, _settings.DailyGoal) });
        PageContent.Children.Add(daily);
        AddHomeAccountCard();
    }

    private void AddLearningStats()
    {
        AddPageHeader(T("Stats.Title"), U("Stats.SeparateMetrics", "Recall ratings, quiz answers and game answers are separate measures.", "Hatırlama puanları, test yanıtları ve oyun yanıtları ayrı ölçülür."));
        var overview = new StackPanel { Spacing = 12 };
        var answers = new StackPanel { Spacing = 12 };
        var history = new StackPanel { Spacing = 12 };
        var today = Today;
        var dailyWords = _progress.DailyReviewedWords.GetValueOrDefault(DayKey(today))?.Count ?? 0;
        var weekWords = Enumerable.Range(0, Math.Min(7, today.DayNumber + 1))
            .SelectMany(offset => _progress.DailyReviewedWords.GetValueOrDefault(DayKey(today.AddDays(-offset))) ?? [])
            .Distinct(StringComparer.Ordinal).Count();
        var unique = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        AddStat(unique, 0, U("Stats.UniqueToday", "Unique words today — all languages", "Bugünkü farklı kelimeler — tüm diller"), dailyWords.ToString("N0"), "");
        AddStat(unique, 1, U("Stats.UniqueWeek", "Unique words in the last 7 days — all languages", "Son 7 gündeki farklı kelimeler — tüm diller"), weekWords.ToString("N0"), "");
        ConfigureResponsiveGrid(unique, 2, 230);
        overview.Children.Add(unique);
        overview.Children.Add(Body(U("Stats.GoalScope", "Cards, quizzes and games share word credit. Repeating a word on the same day counts once. Reading answers without vocabulary count only as scored answers.", "Kartlar, testler ve oyunlar kelime sayımını paylaşır. Aynı gün tekrarlanan kelime bir kez sayılır. Kelime içermeyen okuma yanıtları yalnızca puanlanan yanıt sayılır.")));

        var language = _settings.StudyLanguage + ":";
        var reviews = _progress.Reviews.Where(pair => pair.Key.StartsWith(language, StringComparison.Ordinal))
            .Select(pair => pair.Value).Where(review => review.LastReviewed is not null).ToArray();
        overview.Children.Add(Heading(CurrentStudyLanguage().NativeName + " · " + U("Stats.AllLevels", "all levels", "tüm seviyeler"), 22));
        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        AddStat(grid, 0, U("Stats.TodayWords", "Words last reviewed today", "Son tekrarı bugün olan kelimeler"), reviews.Count(review => review.LastReviewed == today).ToString("N0"), "");
        AddStat(grid, 1, U("Stats.WeekWords", "Words last reviewed in 7 days", "Son 7 günde tekrarlanan kelimeler"), reviews.Count(review => review.LastReviewed >= today.AddDays(-6) && review.LastReviewed <= today).ToString("N0"), "");
        AddStat(grid, 2, U("Study.Due", "Due", "Tekrar zamanı"), reviews.Count(review => review.DueDate <= today).ToString("N0"), "");
        ConfigureResponsiveGrid(grid, 3, 200);
        overview.Children.Add(grid);

        var scored = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        void Scored(string label, int correct, int wrong, string? detail = null)
        {
            var total = (long)correct + wrong;
            var accuracy = total == 0 ? "—" : $"{100.0 * correct / total:0}%";
            var content = new StackPanel { Spacing = 8 };
            content.Children.Add(StudyText(label, 18, emphasis: true));
            content.Children.Add(StudyText($"{T("Stats.Success")}: {accuracy}", 24, emphasis: true));
            content.Children.Add(StudyText($"{T("Stats.Correct")}: {correct:N0} · {T("Stats.Wrong")}: {wrong:N0}", 15));
            if (detail is not null) content.Children.Add(StudyText(detail, 14));
            scored.Children.Add(StudySurface(content));
        }
        Scored(U("Stats.QuizAnswers", "Quiz answers — first attempts, all languages", "Test yanıtları — ilk denemeler, tüm diller"), _progress.QuizCorrectAnswers, _progress.QuizWrongAnswers);
        Scored(U("Stats.GameAnswers", "Game answers — scored attempts, all languages", "Oyun yanıtları — puanlanan denemeler, tüm diller"), _progress.GameCorrectAnswers, _progress.GameWrongAnswers,
            $"{U("Stats.CompletedGames", "Completed games", "Tamamlanan oyunlar")}: {_progress.CompletedGames:N0}");
        ConfigureResponsiveGrid(scored, 2, 280);
        answers.Children.Add(scored);
        answers.Children.Add(BuildScopedGameStatistics());

        var recall = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        string[] labels = [U("Rating.Again", "Again", "Tekrar"), U("Rating.Hard", "Hard", "Zor"), U("Rating.Good", "Good", "İyi"), U("Rating.Easy", "Easy", "Kolay")];
        for (var index = 0; index < labels.Length; index++)
            AddStat(recall, index, labels[index], _progress.RecallRatings.GetValueOrDefault(((RecallRating)index).ToString()).ToString("N0"), "");
        ConfigureResponsiveGrid(recall, 4, 150);
        history.Children.Add(StudyPopupButton(U("Stats.Recall", "Card self-ratings — not accuracy", "Kart öz değerlendirmeleri — doğruluk değildir"), recall, "stats.Recall"));

        var legacy = new StackPanel { Spacing = 8 };
        legacy.Children.Add(StudyText($"{T("Stats.Correct")}: {_progress.CorrectAnswers:N0} · {T("Stats.Wrong")}: {_progress.WrongAnswers:N0}", 15));
        legacy.Children.Add(StudyText($"{U("Stats.LegacyActions", "Historical mixed activity actions", "Geçmiş karma etkinlik işlemleri")}: {_progress.DailyActivity.Values.Sum(count => (long)count):N0}", 15));
        legacy.Children.Add(StudyText(U("Stats.LegacyHint", "Old counters mixed self-ratings and scored answers. They are not converted into quiz accuracy, game accuracy or unique-word goals. New metrics start at zero when absent.", "Eski sayaçlar öz değerlendirmeleri ve puanlanan yanıtları karıştırıyordu. Test doğruluğu, oyun doğruluğu veya farklı kelime hedeflerine dönüştürülmezler. Eksik yeni ölçümler sıfırdan başlar."), 14));
        history.Children.Add(StudyPopupButton(U("Stats.Legacy", "Legacy counters — retained history only", "Eski sayaçlar — yalnızca saklanan geçmiş"), legacy, "stats.Legacy"));

        var tabs = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        var content = new Grid();
        var sections = new (StatsSection Section, string Id, string Label, FrameworkElement Panel)[]
        {
            (StatsSection.Overview, "overview", U("Stats.Overview", "Overview", "Genel bakış"), overview),
            (StatsSection.Answers, "answers", U("Stats.Answers", "Answers & games", "Yanıtlar ve oyunlar"), answers),
            (StatsSection.History, "history", U("Stats.History", "Cards & history", "Kartlar ve geçmiş"), history),
        };
        void Select(StatsSection selected)
        {
            _statsSection = selected;
            foreach (var section in sections)
                section.Panel.Visibility = section.Section == selected ? Visibility.Visible : Visibility.Collapsed;
        }
        var group = "stats.Sections." + Guid.NewGuid().ToString("N");
        foreach (var section in sections)
        {
            var tab = new RadioButton
            {
                Content = StudyText(section.Label, 18, emphasis: true), GroupName = group,
                IsChecked = section.Section == _statsSection, MinWidth = 48, MinHeight = 48,
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            FocusTarget(tab, "stats.Section." + section.Id);
            tab.Checked += (_, _) => Select(section.Section);
            tabs.Children.Add(tab);
            content.Children.Add(section.Panel);
        }
        ConfigureResponsiveGrid(tabs, 3, 170);
        Select(_statsSection);
        PageContent.Children.Add(tabs);
        PageContent.Children.Add(content);
    }

    private void AddProfileLearningControls(StackPanel panel)
    {
        panel.Children.Add(Body(CloudStorageNotice));
        panel.Children.Add(Body(U("Known.ManualHint", "Marked known (U) is a manual bookmark, not automatic mastery. It skips unreviewed new words, but never hides a scheduled due review.", "Biliniyor işareti (U) elle konan bir yer imidir, otomatik ustalık değildir. Tekrar edilmemiş yeni kelimeleri atlar, ancak zamanı gelen tekrarı asla gizlemez.")));
        var goal = new NumberBox
        {
            Header = U("Goal.UniqueTarget", "Daily unique-word target — all languages (1–10000)", "Günlük farklı kelime hedefi — tüm diller (1–10000)"),
            Minimum = 1, Maximum = 10000, Value = _settings.DailyGoal, SmallChange = 1, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        };
        panel.Children.Add(goal);
        var motion = new CheckBox { Content = U("Preferences.Motion", "Reduce motion", "Hareketi azalt"), IsChecked = _settings.ReduceMotion };
        var untimed = new CheckBox { Content = U("Preferences.Untimed", "Untimed game practice", "Süresiz oyun alıştırması"), IsChecked = _settings.UntimedPractice };
        panel.Children.Add(motion);
        panel.Children.Add(untimed);
        var save = AccentButton(U("Preferences.Save", "Save learning preferences", "Öğrenme tercihlerini kaydet"), "");
        save.Click += async (_, _) =>
        {
            if (!double.IsFinite(goal.Value) || goal.Value != Math.Truncate(goal.Value) || goal.Value is < 1 or > 10000)
            {
                ShowNotice(T("Common.Error"), U("Goal.Invalid", "Enter a whole number from 1 to 10000.", "1 ile 10000 arasında bir tam sayı girin."), InfoBarSeverity.Error);
                return;
            }
            await SaveUiSettingsAsync(() => { _settings.DailyGoal = (int)goal.Value; _settings.ReduceMotion = motion.IsChecked == true; _settings.UntimedPractice = untimed.IsChecked == true; });
        };
        panel.Children.Add(save);
        var export = SecondaryButton(U("Backup.Export", "Export local backup", "Yerel yedeği dışa aktar"), "");
        export.Click += async (_, _) => await ExportBackupAsync();
        panel.Children.Add(export);
        var import = SecondaryButton(U("Backup.Import", "Import backup…", "Yedek içe aktar…"), "");
        import.Click += async (_, _) => await ImportBackupAsync();
        panel.Children.Add(import);
        panel.Children.Add(Body(U("Backup.Hint", "Backups contain settings, favorites, reviews and sessions. Keep a copy outside this device. Import replaces current data after validation and confirmation.", "Yedekler ayarları, favorileri, tekrarları ve oturumları içerir. Bu cihazın dışında bir kopya saklayın. İçe aktarma doğrulama ve onaydan sonra mevcut verileri değiştirir.")));
    }

    private async Task SaveUiSettingsAsync(Action change)
    {
        if (_studyBusy || _dialogOpen || _navigationBusy) return;
        var before = System.Text.Json.JsonSerializer.Serialize(_settings);
        SetStudyBusy(true);
        try
        {
            change();
            await _storage.SaveSettingsAsync(_settings);
            ApplyAppearance();
            RenderCurrentPage();
            ShowNotice(U("Preferences.Saved", "Preferences saved", "Tercihler kaydedildi"), "", InfoBarSeverity.Success);
        }
        catch (ImportReloadRequiredException ex) { BlockStorageUntilRestart(ex); }
        catch (Exception ex)
        {
            _settings = System.Text.Json.JsonSerializer.Deserialize<UserSettings>(before)!;
            ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error);
        }
        finally { SetStudyBusy(false); }
    }

    private static void InitializePicker(object picker)
    {
        var window = App.MainWindow ?? throw new InvalidOperationException("The main window is unavailable.");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
    }

    private bool CloudUiUnavailable => !IsLoaded || !_initialized || QuickUiLanguage.ItemsSource is null ||
        _studyBusy || _dialogOpen || _dialogClosed is not null || _navigationBusy || LoadingRing.IsActive;

    private string CloudConnectLabel => U("Cloud.Connect", "Sign in with Google", "Google ile oturum aç");
    private string CloudAccountName => !string.IsNullOrWhiteSpace(_cloud.DisplayName) ? _cloud.DisplayName :
        !string.IsNullOrWhiteSpace(_cloud.Email) ? _cloud.Email : U("Cloud.GoogleAccount", "Google account", "Google hesabı");
    private string CloudOfflineHint => U("Cloud.OfflineOptional", "Google sign-in is optional. Learning always works offline.", "Google ile oturum açmak isteğe bağlıdır. Öğrenme her zaman çevrimdışı çalışır.");
    private string CloudFamilyHint => U("Cloud.FamilyHint", "With a grown-up, save your learning on another device.", "Bir büyüğünle birlikte ilerlemeni başka bir cihaza kaydet.");
    private string CloudStorageNotice => _cloud.IsConnected
        ? U("Cloud.StorageConnected", "Signed in with Google. Learning stays on this device until you choose Save to cloud in Settings > For grown-ups.", "Google ile oturum açıldı. Ayarlar > Büyükler için'de Buluta kaydet'i seçene kadar çalışmaların bu cihazda kalır.")
        : U("Cloud.StorageSignedOut", "Learning is saved on this device. Google sign-in is optional. Make a copy in Settings > For grown-ups exports a local backup.", "Çalışmaların bu cihaza kaydedilir. Google ile oturum açmak isteğe bağlıdır. Ayarlar > Büyükler için'de Bir kopya oluştur yerel yedek dışa aktarır.");

    private void SyncAccountStatus()
    {
        LocalStatusButton.IsEnabled = !CloudUiUnavailable;
        if (!IsLoaded) return;
        var connected = _cloud.IsConnected;
        var palette = AppearancePalette.Current;
        var label = connected ? U("Cloud.Connected", "Connected", "Bağlı") : U("Cloud.SignIn", "Sign in", "Oturum aç");
        var actionHint = connected
            ? U("Cloud.ClickToSignOut", "Select to sign out", "Çıkış yapmak için seçin")
            : U("Cloud.ClickToSignIn", "Select to sign in with Google", "Google ile oturum açmak için seçin");
        var account = connected ? $"{U("Cloud.SignedInAs", "Signed in as", "Oturum açan")}: {CloudAccountName}" : CloudConnectLabel;
        LocalStatusBadge.Background = connected ? new SolidColorBrush(Color.FromArgb(255, 16, 124, 16)) : palette.BoxBrush;
        LocalStatusBadge.BorderBrush = connected ? new SolidColorBrush(Microsoft.UI.Colors.White) : palette.BorderBrush;
        LocalStatusIcon.Glyph = connected ? "\uE73E" : "\uE77B";
        LocalStatusIcon.Foreground = connected ? new SolidColorBrush(Microsoft.UI.Colors.White) : palette.BoxForegroundBrush;
        LocalStatusText.Foreground = palette.BoxForegroundBrush;
        LocalStatusText.FontSize = Math.Max(18, Font(18));
        LocalStatusButton.Foreground = palette.BoxForegroundBrush;
        AutomationProperties.SetName(LocalStatusButton, $"{account}. {actionHint}");
        AutomationProperties.SetHelpText(LocalStatusButton, connected ? CloudStorageNotice : CloudOfflineHint);
        ToolTipService.SetToolTip(LocalStatusButton, $"{account}\n{(connected ? CloudStorageNotice : CloudOfflineHint)}\n{actionHint}");
        if (LocalStatusText.Text != label) UpdateCloudLiveText(LocalStatusText, label);
    }

    private void RefreshCloudUi(Control? previousFocus = null)
    {
        try
        {
            if (IsLoaded && !_dialogOpen && _dialogClosed is null && !_studyBusy && !_navigationBusy && !LoadingRing.IsActive && _activeGame is null &&
                _currentPage is "home" or "profile")
            {
                var id = previousFocus is not null ? AutomationProperties.GetAutomationId(previousFocus) : "";
                id = id switch
                {
                    "home.OtherBrowser" when _cloud.IsConnected => "home.AccountAction",
                    "profile.OtherBrowser" when _cloud.IsConnected => "profile.AccountAction",
                    "cloud.Save" or "cloud.Load" or "cloud.DeleteProfile" or "cloud.GrownUpTools" when !_cloud.IsConnected => "profile.AccountAction",
                    "cloud.DeleteProfile" => "cloud.GrownUpTools",
                    _ => id,
                };
                if (id is "home.AccountAction" or "profile.AccountAction" or "home.OtherBrowser" or "profile.OtherBrowser" or
                    "cloud.Save" or "cloud.Load" or "cloud.GrownUpTools") RequestUiFocus(id);
                RenderCurrentPage();
            }
        }
        finally
        {
            SyncAccountStatus();
            if (ReferenceEquals(previousFocus, LocalStatusButton) && !CloudUiUnavailable && IsLoaded)
                LocalStatusButton.Focus(FocusState.Programmatic);
        }
    }

    // Follow the toolbar's live busy/loading state, including controls built while busy.
    private void BindCloudAction(Control control) => control.SetBinding(Control.IsEnabledProperty, new Microsoft.UI.Xaml.Data.Binding
    {
        Source = LocalStatusButton, Path = new PropertyPath(nameof(Control.IsEnabled)),
        Mode = Microsoft.UI.Xaml.Data.BindingMode.OneWay,
    });

    private Button CloudActionButton(string label, string automationId, bool primary = false)
    {
        var button = primary ? AccentButton(label, "") : SecondaryButton(label, "");
        button.Content = CloudDialogText(label);
        button.FontSize = Math.Max(18, Font(18));
        FocusTarget(button, automationId);
        BindCloudAction(button);
        return button;
    }

    private Button CloudAccountAction(string automationId)
    {
        var connected = _cloud.IsConnected;
        var label = connected ? U("Cloud.SignOut", "Sign out", "Çıkış") : CloudConnectLabel;
        var button = CloudActionButton(label, automationId, primary: !connected);
        if (connected) AutomationProperties.SetName(button, $"{label}: {CloudAccountName}");
        button.Click += OnLocalStatusButtonClick;
        return button;
    }

    private Button CloudBrowserButton(string automationId)
    {
        var button = CloudActionButton(U("Cloud.UseAnotherBrowser", "Use another browser", "Başka bir tarayıcı kullan"), automationId);
        button.Padding = new Thickness(8, 6, 8, 6);
        button.MinHeight = 48;
        button.HorizontalAlignment = HorizontalAlignment.Left;
        var menu = new MenuFlyout();
        var edge = new MenuFlyoutItem
        {
            Text = U("Cloud.OpenInEdge", "Open in Edge", "Edge'de aç"), Tag = button, FontSize = Math.Max(18, Font(18)),
            MinHeight = 48, MinWidth = 48,
        };
        var chrome = new MenuFlyoutItem
        {
            Text = U("Cloud.OpenInChrome", "Open in Chrome", "Chrome'da aç"), Tag = button, FontSize = Math.Max(18, Font(18)),
            MinHeight = 48, MinWidth = 48,
        };
        AutomationProperties.SetAutomationId(edge, automationId + ".Edge");
        AutomationProperties.SetAutomationId(chrome, automationId + ".Chrome");
        BindCloudAction(edge);
        BindCloudAction(chrome);
        edge.Click += async (_, _) => await BeginGoogleSignInAsync(GoogleSignInBrowser.Edge);
        chrome.Click += async (_, _) => await BeginGoogleSignInAsync(GoogleSignInBrowser.Chrome);
        menu.Items.Add(edge);
        menu.Items.Add(chrome);
        button.Flyout = menu;
        return button;
    }

    private void AddHomeAccountCard()
    {
        var copy = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        var title = Heading(_cloud.IsConnected
            ? $"{U("Cloud.SignedInAs", "Signed in as", "Oturum açan")}: {CloudAccountName}"
            : U("Cloud.GoogleAccount", "Google account", "Google hesabı"), 20);
        title.FontSize = Math.Max(18, Font(20));
        copy.Children.Add(title);
        copy.Children.Add(CloudDialogText(CloudFamilyHint));
        var actions = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(CloudAccountAction("home.AccountAction"));
        if (_cloud.IsConnected)
        {
            var label = U("Cloud.ManageAccount", "Manage account", "Hesabı yönet");
            var manage = new HyperlinkButton
            {
                Content = CloudDialogText(label), Foreground = AppearancePalette.Current.BoxForegroundBrush,
                HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(6),
            };
            AutomationProperties.SetAutomationId(manage, "home.ManageSync");
            AutomationProperties.SetName(manage, label);
            BindCloudAction(manage);
            manage.Click += (_, _) =>
            {
                if (CloudUiUnavailable) return;
                _settingsSection = SettingsSection.Account;
                NavigateTo("profile", ProfileItem);
            };
            actions.Children.Add(manage);
        }
        else actions.Children.Add(CloudBrowserButton("home.OtherBrowser"));
        var layout = new Grid { ColumnSpacing = 16, RowSpacing = 10, Children = { copy, actions } };
        ConfigureResponsiveGrid(layout, 2, Font(330));
        PageContent.Children.Add(Card(layout, 18));
    }

    private void AddCloudSection(StackPanel panel)
    {
        panel.Children.Add(SettingHeader("", U("Cloud.GoogleAccount", "Google account", "Google hesabı"), CloudFamilyHint));
        panel.Children.Add(CloudAccountAction("profile.AccountAction"));
        if (_cloud.IsConnected)
        {
            var name = CloudDialogText($"{U("Cloud.SignedInAs", "Signed in as", "Oturum açan")}: {CloudAccountName}");
            name.Foreground = AppearancePalette.Current.BoxForegroundBrush;
            panel.Children.Add(name);
            var actions = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
            var save = CloudActionButton(U("Cloud.Save", "Save to cloud", "Buluta kaydet"), "cloud.Save");
            save.Click += async (_, _) => await CloudSaveAsync();
            var load = CloudActionButton(U("Cloud.Load", "Load from cloud", "Buluttan al"), "cloud.Load");
            load.Click += async (_, _) => await CloudLoadAsync();
            actions.Children.Add(save); actions.Children.Add(load);
            ConfigureResponsiveGrid(actions, 2, 150);
            panel.Children.Add(actions);
            var deleteCloud = CloudActionButton(U("Cloud.DeleteProfile", "Delete cloud profile", "Bulut profilini sil"), "cloud.DeleteProfile");
            deleteCloud.Click += async (_, _) => await CloudDeleteAsync();
            var tools = StudyPopupButton(U("Cloud.GrownUpTools", "Grown-up tools", "Büyükler için araçlar"), deleteCloud, "cloud.GrownUpTools");
            BindCloudAction(tools);
            panel.Children.Add(tools);
        }
        else panel.Children.Add(CloudBrowserButton("profile.OtherBrowser"));
    }

    private async Task BeginGoogleSignInAsync(GoogleSignInBrowser? preferredBrowser = null)
    {
        if (CloudUiUnavailable || _cloud.IsConnected) return;
        if (_activeGame is not null && (_gameLocalBusy || _gameRoundClosed || _resolvingGame is not null || _completingGame is not null))
        {
            ShowNotice(U("Cloud.WaitingForGame", "Finish game feedback first", "Önce oyun geri bildirimini tamamlayın"),
                U("Cloud.SignInAfterFeedback", "Go to the next question, then try signing in. Your game will stay here.", "Sonraki soruya geç, sonra oturum açmayı dene. Oyunun burada kalacak."), InfoBarSeverity.Informational);
            return;
        }
        var browser = preferredBrowser ?? _cloud.LastGoogleBrowser;
        if (!Enum.IsDefined(browser)) browser = GoogleSignInBrowser.Default;
        var focus = FocusManager.GetFocusedElement(XamlRoot) as Control;
        if (focus is MenuFlyoutItem { Tag: Control owner }) focus = owner;
        var page = _currentPage;
        var context = StudyContext;
        var timer = _gameTimer;
        var wasRunning = timer?.IsEnabled == true;
        var game = _activeGame;
        var closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dialogOpen = true;
        _dialogClosed = closed;
        try
        {
            timer?.Stop();
            SetStudyBusy(true);
            var result = await ConnectCloudAsync(browser);
            if (!IsLoaded) return;
            if (result.Success && _cloud.IsConnected)
                ShowNotice(U("Cloud.Connected", "Connected", "Bağlı"), CloudAccountName, InfoBarSeverity.Success);
            else if (result.ErrorMessage is not null)
                ShowNotice(T("Common.Error"), CloudSignInFailureMessage(), InfoBarSeverity.Error);
        }
        catch (Exception)
        {
            if (IsLoaded) ShowNotice(T("Common.Error"), CloudSignInFailureMessage(), InfoBarSeverity.Error);
        }
        finally
        {
            _dialogOpen = false;
            _dialogClosed = null;
            try
            {
                SetStudyBusy(false);
                if (IsLoaded && wasRunning && !_storageBlocked && _activeGame == game && ReferenceEquals(timer, _gameTimer)) timer?.Start();
                RefreshCloudUi(focus);
                RestoreDialogFocus(focus, page, context);
            }
            finally { closed.TrySetResult(true); }
        }
    }

    private static TextBlock CloudDialogText(string text) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, FontSize = Math.Max(18, Font(18)),
    };

    private static void UpdateCloudLiveText(TextBlock text, string message)
    {
        text.Text = message;
        (FrameworkElementAutomationPeer.FromElement(text) ?? FrameworkElementAutomationPeer.CreatePeerForElement(text))
            ?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private async Task<CloudResult> ConnectCloudAsync(GoogleSignInBrowser browser)
    {
        using var cts = new CancellationTokenSource();
        var content = new StackPanel { Spacing = 16 };
        content.Children.Add(new ProgressRing { IsActive = true, Width = 32, Height = 32 });
        content.Children.Add(CloudDialogText(
            U("Cloud.BrowserSignInHint", "Google will open in your browser. Sign in there, then come back.", "Google tarayıcında açılacak. Orada oturum aç, sonra buraya dön.") + " " +
            U("Cloud.BrowserContinueHint", "If the browser shows Continue with Google, choose it.", "Tarayıcıda Google ile devam et görünürse onu seç.")));
        var title = U("Cloud.FinishInBrowser", "Finish in your browser", "Tarayıcında tamamla");
        var waitDialog = new ContentDialog
        {
            XamlRoot = XamlRoot, RequestedTheme = RequestedTheme, Title = title,
            Content = content, FontSize = Math.Max(18, Font(18)),
            CloseButtonText = U("Dialog.Cancel", "Cancel", "İptal"),
            DefaultButton = ContentDialogButton.Close,
        };
        AutomationProperties.SetAutomationId(waitDialog, "cloud.WaitDialog");
        AutomationProperties.SetName(waitDialog, title);
        ConfigureReadingDialog(waitDialog);
        var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var abandonedSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ContentDialogResult>? dialogTask = null;
        Task<CloudResult>? signInTask = null;
        CloudResult? result = null;
        var abandoned = false;
        var closingForResult = false;
        var uiFailed = false;
        void CancelRequest()
        {
            if (cts.IsCancellationRequested) return;
            try { cts.Cancel(); }
            catch (AggregateException) { uiFailed = true; abandoned = true; abandonedSignal.TrySetResult(true); }
        }
        void Abandon()
        {
            abandoned = true;
            abandonedSignal.TrySetResult(true);
            CancelRequest();
        }
        waitDialog.Opened += (_, _) => opened.TrySetResult(true);
        waitDialog.CloseButtonClick += (_, _) => Abandon();
        waitDialog.Closing += (_, _) => { if (!closingForResult) Abandon(); };
        // Unloading still cancels during the result's close animation.
        void UnloadSignIn(object sender, RoutedEventArgs args) => Abandon();
        Unloaded += UnloadSignIn;
        try
        {
            dialogTask = waitDialog.ShowAsync().AsTask();
            await Task.WhenAny(opened.Task, dialogTask, abandonedSignal.Task);
            if (opened.Task.IsCompletedSuccessfully && !abandoned && IsLoaded && !dialogTask.IsCompleted)
            {
                await opened.Task;
                signInTask = _cloud.SignInWithBrowserAsync(cts.Token, browser, _settings.UiLanguage);
                await Task.WhenAny(signInTask, dialogTask, abandonedSignal.Task);
                if (dialogTask.IsCompleted || !IsLoaded) Abandon();
                result = await signInTask;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) { Abandon(); }
        catch (Exception) { uiFailed = true; Abandon(); }
        finally
        {
            closingForResult = true;
            CancelRequest();
            try
            {
                if (dialogTask is not null)
                {
                    if (!dialogTask.IsCompleted) waitDialog.Hide();
                    await dialogTask;
                }
            }
            catch (Exception) { uiFailed = true; Abandon(); }
            finally
            {
                try
                {
                    if (signInTask is not null)
                    {
                        try { result = await signInTask; }
                        catch (OperationCanceledException) when (cts.IsCancellationRequested) { Abandon(); }
                        catch (Exception) { uiFailed = true; Abandon(); }
                    }
                    if (!IsLoaded) Abandon();
                }
                finally { Unloaded -= UnloadSignIn; }
            }
        }
        if (abandoned && result?.Success == true)
        {
            // A cancel racing the atomic credential commit must not retain a new session.
            try { await _cloud.SignOutAsync(); }
            catch (Exception) { ShowCloudSignOutFailure(); return new(false, null); }
        }
        if (uiFailed) return new(false, CloudSignInFailureMessage());
        if (abandoned || result is null) return new(false, null);
        // Only the coordinator's verified, persisted Firebase session is a success.
        return result.Success && _cloud.IsConnected ? result : new(false, CloudSignInFailureMessage());
    }

    // Never interpolate provider errors or browser responses into notices or logs.
    private string CloudSignInFailureMessage() => U("Cloud.SignInHelp", "We could not sign in. Try again, or ask a grown-up for help.", "Oturum açılamadı. Yeniden dene ya da bir büyüğünden yardım iste.");

    private string CloudSyncFailureMessage() => U("Cloud.SyncTryAgain", "We could not finish that. Try again, or ask a grown-up for help.", "Bu işlem tamamlanamadı. Yeniden dene ya da bir büyüğünden yardım iste.");

    private void ShowCloudSignOutFailure()
    {
        if (!IsLoaded) return;
        ShowNotice(T("Common.Error"), U("Cloud.SignOutNotSaved", "Signed out here, but we could not save that change. Ask a grown-up to close the app and try again.", "Burada çıkış yapıldı, ancak bu değişiklik kaydedilemedi. Bir büyüğünden uygulamayı kapatıp yeniden denemesini iste."), InfoBarSeverity.Error);
    }

    private async void OnLocalStatusButtonClick(object sender, RoutedEventArgs e)
    {
        if (CloudUiUnavailable) return;
        if (_cloud.IsConnected) await DisconnectCloudAsync();
        else await BeginGoogleSignInAsync();
    }

    private async Task DisconnectCloudAsync()
    {
        if (CloudUiUnavailable) return;
        var focus = FocusManager.GetFocusedElement(XamlRoot) as Control;
        SetStudyBusy(true);
        try
        {
            await _cloud.SignOutAsync();
            if (IsLoaded) ShowNotice(U("Cloud.SignedOut", "Signed out", "Oturum kapatıldı"), CloudOfflineHint, InfoBarSeverity.Informational);
        }
        catch (Exception) { ShowCloudSignOutFailure(); }
        finally { SetStudyBusy(false); RefreshCloudUi(focus); }
    }

    private async Task CloudSaveAsync()
    {
        if (CloudUiUnavailable || !_cloud.IsConnected) return;
        var focus = FocusManager.GetFocusedElement(XamlRoot) as Control;
        SetStudyBusy(true);
        try
        {
            var result = await _cloud.SaveToCloudAsync(_settings, _progress);
            if (IsLoaded) ShowNotice(result.Success ? U("Cloud.Saved", "Saved to cloud", "Buluta kaydedildi") : T("Common.Error"),
                result.Success ? "" : CloudSyncFailureMessage(), result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error);
        }
        catch (Exception) { if (IsLoaded) ShowNotice(T("Common.Error"), CloudSyncFailureMessage(), InfoBarSeverity.Error); }
        finally { SetStudyBusy(false); RefreshCloudUi(focus); }
    }

    private async Task CloudLoadAsync()
    {
        if (CloudUiUnavailable || !_cloud.IsConnected || _activeGame is not null) return;
        SetStudyBusy(true);
        var importCommitted = false;
        try
        {
            var (result, settings, progress) = await _cloud.LoadFromCloudAsync();
            if (!result.Success || settings is null || progress is null)
            {
                if (IsLoaded) ShowNotice(T("Common.Error"), CloudSyncFailureMessage(), InfoBarSeverity.Warning);
                return;
            }
            if (!await ConfirmAsync(U("Backup.Replace", "Replace local settings and progress?", "Yerel ayarlar ve ilerleme değiştirilsin mi?"),
                $"{settings.StudyLanguage} · {settings.Level}\n" +
                U("Cloud.ReplaceHint", "This replaces all current progress and preferences with the cloud copy. A separate safety backup will be saved before replacement.", "Bu, tüm mevcut ilerleme ve tercihleri bulut kopyasıyla değiştirir. Değiştirmeden önce ayrı bir güvenlik yedeği kaydedilir."))) return;
            var safety = Path.Combine(_storage.FolderPath, $"before-cloud-load-{Guid.NewGuid():N}.json");
            await _storage.ExportAsync(safety, _settings, _progress);
            try
            {
                await _storage.ApplyImportAsync(settings, progress);
                importCommitted = true;
            }
            catch (ImportReloadRequiredException) { throw; }
            catch (Exception failure) { throw new IOException($"{failure.Message}\n{safety}", failure); }
            var loaded = await _storage.LoadStateAsync();
            (_settings, _progress) = loaded;
            _cardUndo = null; _cardUndoContext = null; _revealedCardKey = null; _pendingFocus = null; _cardRevealed = false;
            _libraryContext = null; _libraryQuery = ""; _libraryFavorites = _libraryKnown = _libraryDue = false; _libraryPage = 0;
            await ReloadWordsAsync();
            ApplyAppearance(); ApplyNavigationLanguage();
            ShowNotice(U("Cloud.Loaded", "Loaded from cloud. Previous data saved at:", "Buluttan yüklendi. Önceki veriler şuraya kaydedildi:"), safety, InfoBarSeverity.Success);
        }
        catch (ImportReloadRequiredException ex) { BlockStorageUntilRestart(ex); }
        catch (Exception ex)
        {
            if (importCommitted) BlockStorageUntilRestart(ex);
            else ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error);
        }
        finally { SetStudyBusy(false); RefreshCloudUi(); }
    }

    private async Task CloudDeleteAsync()
    {
        if (CloudUiUnavailable || !_cloud.IsConnected) return;
        var focus = FocusManager.GetFocusedElement(XamlRoot) as Control;
        if (!await ConfirmAsync(U("Cloud.DeleteConfirm", "Delete your cloud profile?", "Bulut profiliniz silinsin mi?"),
            U("Cloud.DeleteConfirmHint", "This only removes the cloud copy. Local progress on this device and your Google account are unaffected.", "Bu yalnızca bulut kopyasını kaldırır. Bu cihazdaki yerel ilerleme ve Google hesabınız etkilenmez."))) return;
        if (CloudUiUnavailable || !_cloud.IsConnected) return;
        SetStudyBusy(true);
        try
        {
            var result = await _cloud.DeleteCloudProfileAsync();
            if (IsLoaded) ShowNotice(result.Success ? U("Cloud.Deleted", "Cloud profile deleted", "Bulut profili silindi") : T("Common.Error"),
                result.Success ? "" : CloudSyncFailureMessage(), result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error);
        }
        catch (Exception) { if (IsLoaded) ShowNotice(T("Common.Error"), CloudSyncFailureMessage(), InfoBarSeverity.Error); }
        finally { SetStudyBusy(false); RefreshCloudUi(focus); }
    }

    private async Task ExportBackupAsync()
    {
        if (_studyBusy || _dialogOpen || _navigationBusy) return;
        SetStudyBusy(true);
        try
        {
            var picker = new FileSavePicker { SuggestedFileName = $"YDKE-backup-{DateTime.Now:yyyyMMdd-HHmmss}", DefaultFileExtension = ".json" };
            picker.FileTypeChoices.Add("YDKE JSON", new[] { ".json" });
            InitializePicker(picker);
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            var storageFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_storage.FolderPath));
            // Never remove a picker's empty file inside managed storage, even if the
            // user selected a corrupt zero-byte primary or backup. Core validation
            // must remain the first code allowed to inspect/replace managed files.
            if (string.Equals(Path.GetDirectoryName(Path.GetFullPath(file.Path)), storageFolder, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(U("Backup.SeparateFolder", "Choose a backup location outside the application's local data folder.", "Uygulamanın yerel veri klasörünün dışında bir yedek konumu seçin."));
            // FileSavePicker creates a zero-byte reservation for a new destination.
            // AppStorage intentionally rejects invalid existing exports; remove only that
            // empty reservation, never a nonempty invalid file or an existing valid backup.
            if (new FileInfo(file.Path).Length == 0) File.Delete(file.Path);
            await _storage.ExportAsync(file.Path, _settings, _progress);
            ShowNotice(U("Backup.Exported", "Backup exported", "Yedek dışa aktarıldı"), file.Path, InfoBarSeverity.Success);
        }
        catch (ImportReloadRequiredException ex) { BlockStorageUntilRestart(ex); }
        catch (Exception ex) { ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error); }
        finally { SetStudyBusy(false); }
    }

    private async Task ImportBackupAsync()
    {
        if (_studyBusy || _dialogOpen || _navigationBusy) return;
        SetStudyBusy(true);
        var importCommitted = false;
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            InitializePicker(picker);
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            var imported = await _storage.ImportAsync(file.Path);
            if (!await ConfirmAsync(U("Backup.Replace", "Replace local settings and progress?", "Yerel ayarlar ve ilerleme değiştirilsin mi?"),
                $"{file.Name}\n{imported.Settings.StudyLanguage} · {imported.Settings.Level}\n" +
                U("Backup.ReplaceHint", "This replaces all current progress and preferences. A separate safety backup will be saved before replacement.", "Tüm mevcut ilerleme ve tercihler değiştirilir. Değiştirmeden önce ayrı bir güvenlik yedeği kaydedilir."))) return;
            var safety = Path.Combine(_storage.FolderPath, $"before-import-{Guid.NewGuid():N}.json");
            await _storage.ExportAsync(safety, _settings, _progress);
            try
            {
                // Storage owns the crash-recovery journal and atomic pair contract.
                await _storage.ApplyImportAsync(imported.Settings, imported.Progress);
                importCommitted = true;
            }
            catch (ImportReloadRequiredException) { throw; }
            catch (Exception failure)
            {
                // Only a pre-commit failure reaches this wrapper.
                throw new IOException($"{failure.Message}\n{safety}", failure);
            }
            var loaded = await _storage.LoadStateAsync();
            (_settings, _progress) = loaded;
            _cardUndo = null;
            _cardUndoContext = null;
            _revealedCardKey = null;
            _pendingFocus = null;
            _cardRevealed = false;
            _libraryContext = null;
            _libraryQuery = "";
            _libraryFavorites = _libraryKnown = _libraryDue = false;
            _libraryPage = 0;
            await ReloadWordsAsync();
            ApplyAppearance();
            ApplyNavigationLanguage();
            RenderCurrentPage();
            ShowNotice(U("Backup.Imported", "Backup imported. Previous data saved at:", "Yedek içe aktarıldı. Önceki verilerin yedeği:"), safety, InfoBarSeverity.Success);
        }
        catch (ImportReloadRequiredException ex)
        {
            BlockStorageUntilRestart(ex);
        }
        catch (Exception ex)
        {
            if (importCommitted) BlockStorageUntilRestart(ex);
            else ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error);
        }
        finally { SetStudyBusy(false); }
    }
}