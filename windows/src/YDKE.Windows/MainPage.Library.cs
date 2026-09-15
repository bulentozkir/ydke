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

    private string MarkedKnownLabel => U("Known.Marked", "Words marked as known", "Biliniyor olarak işaretlenen kelimeler");

    private string FavoriteLabel(string key) => _progress.FavoriteWords.Contains(key)
        ? U("Favorite.Remove", "★ Favorited — remove", "★ Favori — kaldır")
        : U("Favorite.Add", "☆ Add favorite", "☆ Favorilere ekle");

    private string KnownLabel(string key) => _progress.KnownWords.Contains(key)
        ? U("Known.Remove", "✓ Marked known — unmark (U)", "✓ Biliniyor olarak işaretli — kaldır (U)")
        : U("Known.Add", "Mark known (U)", "Biliniyor olarak işaretle (U)");

    private Button FavoriteButton(VocabularyEntry entry, Action? refresh = null)
    {
        var label = FavoriteLabel(entry.Key);
        var button = SecondaryButton(label, "");
        AutomationProperties.SetHelpText(button, label);
        ToolTipService.SetToolTip(button, label);
        FocusTarget(button, "favorite." + entry.Key);
        button.Click += async (_, _) =>
        {
            if (!await MutateStudyAsync(() => Toggle(_progress.FavoriteWords, entry.Key))) return;
            button.Content = ButtonContent(FavoriteLabel(entry.Key), "");
            if (button.Content is UIElement content) ApplyButtonIconForeground(content, button.Foreground);
            var updatedLabel = FavoriteLabel(entry.Key);
            AutomationProperties.SetName(button, updatedLabel);
            AutomationProperties.SetHelpText(button, updatedLabel);
            ToolTipService.SetToolTip(button, updatedLabel);
            refresh?.Invoke(); // Cards update in place, preserving reveal, focus and audio.
        };
        return button;
    }

    private Button KnownButton(VocabularyEntry entry, Action? refresh = null)
    {
        var label = KnownLabel(entry.Key);
        var button = SecondaryButton(label, "");
        AutomationProperties.SetHelpText(button, label);
        ToolTipService.SetToolTip(button, label);
        FocusTarget(button, "known." + entry.Key);
        button.Click += async (_, _) =>
        {
            if (!await MutateStudyAsync(() => Toggle(_progress.KnownWords, entry.Key))) return;
            button.Content = ButtonContent(KnownLabel(entry.Key), "");
            if (button.Content is UIElement content) ApplyButtonIconForeground(content, button.Foreground);
            var updatedLabel = KnownLabel(entry.Key);
            AutomationProperties.SetName(button, updatedLabel);
            AutomationProperties.SetHelpText(button, updatedLabel);
            ToolTipService.SetToolTip(button, updatedLabel);
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
        var wordCardBackground = AppearancePalette.EnsureFillContrast(palette.Background, palette.Box, 5.2);
        var wordCardTitleForeground = EnsureStrongTextContrast(wordCardBackground, palette.BoxForeground, 10);
        var wordCardBodyForeground = EnsureStrongTextContrast(wordCardBackground, palette.BoxForeground, 8.5);
        var wordCardMetaForeground = EnsureStrongTextContrast(wordCardBackground, palette.BackgroundForeground, 7);
        var wordCardBorder = AppearancePalette.EnsureBoundaryContrast(wordCardBackground, palette.Border);
        var wordCardBackgroundBrush = new SolidColorBrush(wordCardBackground);
        var wordCardTitleForegroundBrush = new SolidColorBrush(wordCardTitleForeground);
        var wordCardBodyForegroundBrush = new SolidColorBrush(wordCardBodyForeground);
        var wordCardMetaForegroundBrush = new SolidColorBrush(wordCardMetaForeground);
        var wordCardBorderBrush = new SolidColorBrush(wordCardBorder);
        void ApplyWordsActionVisual(Button button)
        {
            var background = AppearancePalette.EnsureFillContrast(palette.Background, palette.ButtonTint, 5.2);
            var foreground = EnsureStrongTextContrast(background, palette.BoxForeground);
            var border = AppearancePalette.EnsureBoundaryContrast(background, palette.Border);
            ApplyAccessibleButtonVisuals(button, background, foreground, border);
            button.BorderThickness = new Thickness(2);
            button.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        }
        void ApplyWordsNavigationVisual(Button button, bool nextAction)
        {
            var baseColor = nextAction
                ? (palette.Theme == ElementTheme.Dark
                    ? Color.FromArgb(255, 14, 96, 53)
                    : Color.FromArgb(255, 220, 252, 231))
                : (palette.Theme == ElementTheme.Dark
                    ? Color.FromArgb(255, 146, 64, 14)
                    : Color.FromArgb(255, 255, 237, 213));
            var background = AppearancePalette.EnsureFillContrast(palette.Background, baseColor, 5.2);
            var foreground = EnsureStrongTextContrast(background, palette.BoxForeground, 7);
            var border = AppearancePalette.EnsureBoundaryContrast(background, palette.Border);
            ApplyAccessibleButtonVisuals(button, background, foreground, border);
            button.BorderThickness = new Thickness(2);
            button.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        }
        var wordDetailSelectionBackground = AppearancePalette.EnsureFillContrast(wordCardBackground, palette.Button, 4.5);
        var wordDetailSelectionForeground = EnsureStrongTextContrast(wordDetailSelectionBackground, Microsoft.UI.Colors.White, 4.5);
        var wordDetailSelectionBrush = new SolidColorBrush(wordDetailSelectionBackground);
        StackPanel WordDetailSection(string label, string text, Color badgeSeed, bool italic)
        {
            var badgeBackground = AppearancePalette.EnsureFillContrast(wordCardBackground, badgeSeed, 4.5);
            var badgeForeground = EnsureStrongTextContrast(badgeBackground, palette.BoxForeground, 7);
            var badgeBorder = AppearancePalette.EnsureBoundaryContrast(badgeBackground, palette.Border);
            var badgeText = StudyText(label, 16, new SolidColorBrush(badgeForeground), emphasis: true);
            AutomationProperties.SetHeadingLevel(badgeText, AutomationHeadingLevel.Level3);
            AutomationProperties.SetName(badgeText, label);
            var badge = new Border
            {
                Child = badgeText,
                Padding = new Thickness(10, 5, 10, 5),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(badgeBackground),
                BorderBrush = new SolidColorBrush(badgeBorder),
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                HighContrastAdjustment = ElementHighContrastAdjustment.Auto,
            };
            var body = StudyText(text, 20, wordCardBodyForegroundBrush, selectable: true);
            body.FontStyle = italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal;
            body.SelectionHighlightColor = wordDetailSelectionBrush;
            body.HighContrastAdjustment = ElementHighContrastAdjustment.Auto;
            AutomationProperties.SetName(body, text);
            var section = new StackPanel { Spacing = 6 };
            section.Children.Add(badge);
            section.Children.Add(body);
            return section;
        }
        AddPageHeader(T("Words.Title"), $"{CurrentStudyLanguage().NativeName} · {_settings.Level} · " +
            U("Known.SharedWithCards", "known marks are shared with Cards", "biliniyor işaretleri Kartlar ile paylaşılır"));
        var search = new AutoSuggestBox
        {
            PlaceholderText = T("Words.Search"), QueryIcon = new SymbolIcon(Symbol.Find), Text = _libraryQuery,
            FontSize = ReadingSize(18), MinHeight = 48, MinWidth = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(search, T("Words.Search"));
        AutomationProperties.SetHelpText(search, U("Words.SearchHelp", "Search by word, meaning or category.", "Kelime, anlam veya kategoriye göre ara."));
        FocusTarget(search, "library.Search");
        var clearLabel = U("Library.ClearFilters", "Clear filters", "Filtreleri temizle");
        var clearFilters = SecondaryButton(clearLabel, "");
        clearFilters.Padding = new Thickness(12, 8, 12, 8);
        clearFilters.HorizontalAlignment = HorizontalAlignment.Left;
        clearFilters.VerticalAlignment = VerticalAlignment.Center;
        ApplyWordsActionVisual(clearFilters);
        AutomationProperties.SetHelpText(clearFilters, clearLabel);
        ToolTipService.SetToolTip(clearFilters, clearLabel);
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
        var searchLabel = StudyText(T("Words.Search"), 18, emphasis: true);
        AutomationProperties.SetAutomationId(searchLabel, "library.Search.Label");
        AutomationProperties.SetName(searchLabel, T("Words.Search"));
        PageContent.Children.Add(searchLabel);
        PageContent.Children.Add(searchRow);

        CheckBox Filter(string label, string helpText, bool selected, string id)
        {
            var filter = new CheckBox
            {
                Content = Body(label), IsChecked = selected,
                FontSize = ReadingSize(18), MinHeight = 48, MinWidth = 48,
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            AutomationProperties.SetName(filter, label);
            AutomationProperties.SetHelpText(filter, helpText);
            ToolTipService.SetToolTip(filter, $"{label}: {helpText}");
            filter.HighContrastAdjustment = ElementHighContrastAdjustment.Auto;
            filter.UseSystemFocusVisuals = true;
            FocusTarget(filter, id);
            return filter;
        }
        var filtersTitle = StudyText(U("Library.Filters", "Filters", "Filtreler"), 18, emphasis: true);
        AutomationProperties.SetAutomationId(filtersTitle, "library.Filters.Label");
        AutomationProperties.SetName(filtersTitle, filtersTitle.Text);
        PageContent.Children.Add(filtersTitle);
        var filters = new Grid { ColumnSpacing = 10, RowSpacing = 8 };
        var favorite = Filter(T("Home.Favorites"),
            U("Library.FavoritesFilterHelp", "Show only words marked as favorites.", "Yalnızca favori olarak işaretlenen kelimeleri göster."),
            _libraryFavorites, "library.Filter.Favorites");
        var known = Filter(U("Known.Filter", "Words marked as known", "Biliniyor olarak işaretlenen kelimeler"),
            U("Library.KnownFilterHelp", "Show only words marked as known.", "Yalnızca biliniyor olarak işaretlenen kelimeleri göster."),
            _libraryKnown, "library.Filter.Known");
        var due = Filter(U("Study.Due", "Due", "Tekrar zamanı"),
            U("Library.DueFilterHelp", "Show only words due for review today.", "Yalnızca bugün tekrarı gelen kelimeleri göster."),
            _libraryDue, "library.Filter.Due");
        filters.Children.Add(favorite);
        filters.Children.Add(known);
        filters.Children.Add(due);
        var categoryLabel = U("Library.Category", "Category", "Kategori");
        var categories = new ComboBox { Header = Body(categoryLabel), HorizontalAlignment = HorizontalAlignment.Stretch };
        ConfigureReadingComboBox(categories);
        ApplySettingsComboBoxVisuals(categories);
        AutomationProperties.SetName(categories, categoryLabel);
        AutomationProperties.SetHelpText(categories, U("Library.CategoryHelp", "Choose a topic category.", "Bir konu kategorisi seç."));
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
        var practiceLabel = U("Library.Practice", "Choose a 20-question test", "20 soruluk test seç");
        var practiceHelp = U("Library.PracticeHelp", "Open the test islands for this level. Search filters do not change the tests.", "Bu seviyenin test adacıklarını aç. Arama filtreleri testleri değiştirmez.");
        var practice = AccentButton(practiceLabel, "");
        AutomationProperties.SetHelpText(practice, practiceHelp);
        ToolTipService.SetToolTip(practice, practiceHelp);
        FocusTarget(practice, "library.Practice");
        var browserActions = new Grid { ColumnSpacing = 8, RowSpacing = 8, Children = { count, practice } };
        ConfigureResponsiveGrid(browserActions, 2, 170);
        PageContent.Children.Add(browserActions);
        var emptyContent = new StackPanel { Spacing = 8 };
        var emptyTitle = StudyText(U("Library.Empty", "No matching words", "Eşleşen kelime yok"), 18, emphasis: true);
        AutomationProperties.SetAutomationId(emptyTitle, "library.Empty");
        emptyContent.Children.Add(emptyTitle);
        emptyContent.Children.Add(StudyText(U("Library.EmptyHint", "Try a different search, clear the filters, or choose another study language or level.", "Farklı bir arama deneyin, filtreleri temizleyin veya başka bir öğrenme dili ya da seviye seçin."), 14));
        var emptyReset = SecondaryButton(clearLabel, "");
        ApplyWordsActionVisual(emptyReset);
        AutomationProperties.SetHelpText(emptyReset, clearLabel);
        ToolTipService.SetToolTip(emptyReset, clearLabel);
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
        Button? previousButton = null;
        Button? nextButton = null;

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
            previousButton = null;
            nextButton = null;
            list.Children.Clear();
            Announce(count, string.Format(System.Globalization.CultureInfo.CurrentCulture,
                U("Library.BrowseCount", "{0:N0} matches · showing word {1:N0} of {0:N0}", "{0:N0} eşleşme · {0:N0} kelime arasından {1:N0}. kelime gösteriliyor"), matches.Length, matches.Length == 0 ? 0 : _libraryPage + 1));
            empty.Visibility = matches.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            var canGoPrevious = _libraryPage > 0;
            var canGoNext = _libraryPage + 1 < matches.Length;
            practice.IsEnabled = wordBatch.Select(word => word.Word).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).Count() >= 2;
            foreach (var entry in matches.Skip(_libraryPage).Take(1))
            {
                var definition = LocalizedPart(entry.Definition);
                var (exampleStudyLanguage, exampleTurkish) = ExampleParts(entry.Example);
                var meaningBadgeSeed = palette.Theme == ElementTheme.Dark
                    ? Color.FromArgb(255, 146, 64, 14) : Color.FromArgb(255, 255, 237, 213);
                var exampleBadgeSeed = palette.Theme == ElementTheme.Dark
                    ? Color.FromArgb(255, 14, 96, 53) : Color.FromArgb(255, 220, 252, 231);
                var translationBadgeSeed = palette.Theme == ElementTheme.Dark
                    ? Color.FromArgb(255, 91, 33, 182) : Color.FromArgb(255, 243, 232, 255);
                var header = new StackPanel { Spacing = 4 };
                header.Children.Add(StudyText(entry.Word, 26, wordCardTitleForegroundBrush, emphasis: true, selectable: true));
                header.Children.Add(WordDetailSection(T("Cards.Meaning"), definition, meaningBadgeSeed, italic: false));
                var details = new StackPanel { Spacing = 8 };
                details.Children.Add(StudyText($"{entry.Category} · {entry.Level} · {entry.PartOfSpeech}", 18, wordCardMetaForegroundBrush));
                // Side-by-side (not stacked) so the fixed no-scroll viewport still fits Previous/Next below.
                var examples = new Grid { ColumnSpacing = 16, RowSpacing = 8 };
                examples.Children.Add(WordDetailSection(CurrentStudyLanguage().NativeName, exampleStudyLanguage, exampleBadgeSeed, italic: true));
                if (!string.IsNullOrWhiteSpace(exampleTurkish))
                    examples.Children.Add(WordDetailSection(U("Words.ExampleTranslation", "Turkish translation", "Türkçe çeviri"), exampleTurkish, translationBadgeSeed, italic: true));
                ConfigureResponsiveGrid(examples, 2, Font(260));
                details.Children.Add(examples);
                var actions = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
                StackPanel ActionGroup(string title, params UIElement[] controls)
                {
                    var group = new StackPanel { Spacing = 6 };
                    var titleText = StudyText(title, 18, wordCardMetaForegroundBrush, emphasis: true);
                    AutomationProperties.SetName(titleText, title);
                    group.Children.Add(titleText);
                    foreach (var control in controls) group.Children.Add(control);
                    AutomationProperties.SetName(group, title);
                    return group;
                }
                var audio = SecondaryButton(T("Cards.Listen"), "");
                ApplyWordsActionVisual(audio);
                var listenHelp = U("Library.ListenHelp", "Play this word's pronunciation.", "Bu kelimenin telaffuzunu dinle.");
                FocusTarget(audio, "library.Listen." + entry.Key);
                AutomationProperties.SetName(audio, $"{T("Cards.Listen")}: {entry.Word}");
                AutomationProperties.SetHelpText(audio, listenHelp);
                ToolTipService.SetToolTip(audio, listenHelp);
                audio.Click += async (_, _) => { if (IsCurrentRender()) await PlayWordAsync(entry.Word, audio); };
                // ActionGroup below is the button's only parent; a second Add aborts word rendering.
                var repeatLabel = U("Library.Repeat", "Review again", "Tekrar et");
                var repeatHelp = U("Library.RepeatHelp", "Schedule this word for review tomorrow. It will then appear under Due.", "Bu kelimeyi yarın tekrar etmek için planla. Sonra Tekrar zamanı filtresinde görünür.");
                var repeat = SecondaryButton(repeatLabel, "\uE72C");
                ApplyWordsActionVisual(repeat);
                FocusTarget(repeat, "library.Repeat." + entry.Key);
                AutomationProperties.SetName(repeat, $"{repeatLabel}: {entry.Word}");
                AutomationProperties.SetHelpText(repeat, repeatHelp);
                ToolTipService.SetToolTip(repeat, repeatHelp);
                repeat.Click += async (_, _) =>
                {
                    if (!IsCurrentRender()) return;
                    if (await MutateStudyAsync(() => LearningEngine.ScheduleReview(_progress, entry.Key, Today)))
                    {
                        ShowNotice(U("Library.RepeatScheduled", "Review scheduled for tomorrow", "Yarın için tekrar planlandı"), repeatHelp, InfoBarSeverity.Success);
                        Refresh(changedKey: entry.Key, action: "repeat");
                    }
                };
                var markFavorite = FavoriteButton(entry, () => Refresh(changedKey: entry.Key, action: "favorite"));
                ApplyWordsActionVisual(markFavorite);
                AutomationProperties.SetName(markFavorite, $"{FavoriteLabel(entry.Key)}: {entry.Word}");
                var markKnown = KnownButton(entry, () => Refresh(changedKey: entry.Key, action: "known"));
                ApplyWordsActionVisual(markKnown);
                AutomationProperties.SetName(markKnown, $"{KnownLabel(entry.Key)}: {entry.Word}");
                var marks = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
                marks.Children.Add(markFavorite);
                marks.Children.Add(markKnown);
                ConfigureResponsiveGrid(marks, 2, 145);
                var previous = SecondaryButton(U("Library.Previous", "Previous word", "Önceki kelime"), "");
                ApplyWordsNavigationVisual(previous, nextAction: false);
                previous.IsEnabled = canGoPrevious;
                previous.HorizontalAlignment = HorizontalAlignment.Left;
                AutomationProperties.SetHelpText(previous, T("Library.Previous"));
                FocusTarget(previous, "library.Previous");
                previous.Click += (_, _) =>
                {
                    if (!searchPending && IsCurrentRender() && _libraryPage > 0)
                    {
                        _libraryPage--;
                        Refresh();
                    }
                };
                var next = SecondaryButton(U("Library.Next", "Next word", "Sonraki kelime"), "");
                ApplyWordsNavigationVisual(next, nextAction: true);
                next.IsEnabled = canGoNext;
                next.HorizontalAlignment = HorizontalAlignment.Left;
                AutomationProperties.SetHelpText(next, T("Library.Next"));
                FocusTarget(next, "library.Next");
                next.Click += (_, _) =>
                {
                    if (!searchPending && IsCurrentRender() && _libraryPage + 1 < matches.Length)
                    {
                        _libraryPage++;
                        Refresh();
                    }
                };
                previousButton = previous;
                nextButton = next;
                var audioNavigation = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                audioNavigation.Children.Add(previous);
                audioNavigation.Children.Add(next);
                actions.Children.Add(ActionGroup(U("Library.AudioGroup", "Audio", "Ses"), audio, audioNavigation));
                actions.Children.Add(ActionGroup(U("Library.ReviewGroup", "Review", "Tekrar"), repeat));
                actions.Children.Add(ActionGroup(U("Library.MarksGroup", "Personal marks", "Kişisel işaretler"), marks));
                AutomationProperties.SetAutomationId(actions, $"library.Actions.{entry.Key}");
                var actionsLabel = U("Library.WordActions", "Word actions", "Kelime işlemleri");
                var actionsHelp = U("Library.WordActionsHelp", "Listen, schedule a review, add a favorite mark, or mark this word as known.", "Bu kelimeyi dinle, tekrar planla, favoriye ekle veya biliniyor olarak işaretle.");
                AutomationProperties.SetName(actions, actionsLabel);
                AutomationProperties.SetHelpText(actions, actionsHelp);
                ConfigureResponsiveGrid(actions, 3, Font(220));
                details.Children.Add(actions);
                async void OnWordShortcut(object? sender, KeyRoutedEventArgs args)
                {
                    if (!IsCurrentRender() || args.Key != Windows.System.VirtualKey.U || args.KeyStatus.WasKeyDown || _studyBusy || _dialogOpen || _navigationBusy) return;
                    foreach (var modifier in new[] { Windows.System.VirtualKey.Control, Windows.System.VirtualKey.Menu, Windows.System.VirtualKey.Shift })
                        if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(modifier).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) return;
                    args.Handled = true;
                    if (await MutateStudyAsync(() => Toggle(_progress.KnownWords, entry.Key))) Refresh(changedKey: entry.Key, action: "known");
                }

                // Keep one wrapper for details, including the fallback: detaching an
                // Expander's Content does not detach the children inside that wrapper.
                var detailSurface = StudySurface(details, 14);
                detailSurface.Background = wordCardBackgroundBrush;
                detailSurface.BorderBrush = wordCardBorderBrush;
                Control wordCard;
                Expander? expander = null;
                try
                {
                    expander = new Expander
                    {
                        IsExpanded = true, Tag = entry.Key,
                        FontSize = ReadingSize(18), MinHeight = 48, MinWidth = 48,
                        Background = wordCardBackgroundBrush, Foreground = wordCardTitleForegroundBrush,
                        HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    };
                    // Retain the instance before attaching children so the catch can detach them.
                    expander.Header = header;
                    expander.Content = detailSurface;
                    foreach (var state in new[] { "", "PointerOver", "Pressed", "Disabled" })
                    {
                        expander.Resources[$"ExpanderHeaderBackground{state}"] = wordCardBackgroundBrush;
                        expander.Resources[$"ExpanderHeaderForeground{state}"] = wordCardTitleForegroundBrush;
                    }
                    expander.Resources["ExpanderContentBackground"] = wordCardBackgroundBrush;
                    expander.Resources["ExpanderContentBorderBrush"] = wordCardBorderBrush;
                    AutomationProperties.SetName(expander, $"{entry.Word} · {definition}");
                    void UpdateWordDetailsAccessibility()
                    {
                        var help = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                            U(expander.IsExpanded ? "Library.WordDetailsExpanded" : "Library.WordDetailsCollapsed",
                                expander.IsExpanded ? "Details for {0} are expanded. Select to collapse." : "Details for {0} are collapsed. Select to expand.",
                                expander.IsExpanded ? "{0} kelimesinin ayrıntıları açık. Kapatmak için seç." : "{0} kelimesinin ayrıntıları kapalı. Açmak için seç."), entry.Word);
                        AutomationProperties.SetHelpText(expander, help);
                        ToolTipService.SetToolTip(expander, help);
                    }
                    UpdateWordDetailsAccessibility();
                    expander.RegisterPropertyChangedCallback(Expander.IsExpandedProperty, (_, _) => UpdateWordDetailsAccessibility());
                    wordCard = expander;
                }
                catch (Exception)
                {
                    if (expander is not null)
                    {
                        expander.Header = null;
                        expander.Content = null;
                    }
                    // Expander can fail to activate on incomplete runtimes; keep Words usable with a plain card.
                    var fallbackStack = new StackPanel { Spacing = 8 };
                    fallbackStack.Children.Add(header);
                    fallbackStack.Children.Add(detailSurface);
                    var fallback = new ContentControl
                    {
                        Content = Card(fallbackStack, 14),
                        FontSize = ReadingSize(18),
                        MinHeight = 48,
                        MinWidth = 48,
                        IsTabStop = true,
                        UseSystemFocusVisuals = true,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    };
                    AutomationProperties.SetName(fallback, $"{entry.Word} · {definition}");
                    AutomationProperties.SetHelpText(fallback, actionsHelp);
                    ToolTipService.SetToolTip(fallback, actionsHelp);
                    wordCard = fallback;
                }
                FocusTarget(wordCard, "library.Word." + entry.Key);
                wordCard.KeyDown += OnWordShortcut;
                list.Children.Add(wordCard);
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
            practice.IsEnabled = false;
            if (previousButton is not null) previousButton.IsEnabled = false;
            if (nextButton is not null) nextButton.IsEnabled = false;
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
        practice.Click += (_, _) =>
        {
            if (!searchPending && IsCurrentRender() && practice.IsEnabled) ShowQuizExamPicker();
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
            var modeLabel = mode == "cards" ? T("Nav.Cards") : T("Nav.Quiz");
            panel.Children.Add(Heading(modeLabel, 22));
            var session = mode == "cards" ? Session("cards") : null;
            panel.Children.Add(Body(mode == "quiz" ? T("Kids.Quiz.ExamTitle") : session is null
                ? U("Kids.Home.Ready", "A few words at a time. Let's try!", "Bir seferde birkaç kelime. Hadi deneyelim!")
                : $"{U("Session.Progress", "Completed", "Tamamlanan")}: {session.Answers.Count} / {session.WordKeys.Count}"));
            var sessionLabel = mode == "quiz" ? T("Kids.Quiz.ChooseExam") : session is not null && session.Index < session.WordKeys.Count
                ? U("Home.Resume", "Resume saved session", "Kayıtlı oturuma devam") : T("Home.Continue");
            var button = AccentButton(sessionLabel, "");
            AutomationProperties.SetAutomationId(button, $"home.{mode}.Session");
            AutomationProperties.SetHelpText(button, $"{modeLabel}: {sessionLabel}");
            ToolTipService.SetToolTip(button, $"{modeLabel}: {sessionLabel}");
            FocusTarget(button, $"home.{mode}.Session");
            button.Click += (_, _) =>
            {
                if (mode == "quiz") ShowQuizExamPicker();
                else NavigateTo("cards", CardsItem);
            };
            panel.Children.Add(button);
            suite.Children.Add(Card(panel, 22));
        }
        ConfigureResponsiveGrid(suite, 2, 320);
        PageContent.Children.Add(suite);
        var play = AccentButton(U("Kids.Home.Play", "Play a word game", "Kelime oyunu oyna"), "\uE768");
        AutomationProperties.SetAutomationId(play, "home.Play");
        AutomationProperties.SetHelpText(play, U("Kids.Home.PlayHelp", "Open the word games.", "Kelime oyunlarını aç."));
        ToolTipService.SetToolTip(play, U("Kids.Home.PlayHelp", "Open the word games.", "Kelime oyunlarını aç."));
        FocusTarget(play, "home.Play");
        play.Click += (_, _) => NavigateTo("simple-games", SimpleGamesItem);
        var wordSummary = StudyPopupButton(U("Kids.Home.WordSummary", "Word summary", "Kelime özeti"), grid, "home.WordSummary");
        AutomationProperties.SetHelpText(wordSummary, U("Kids.Home.WordSummaryHelp", "Open counts for due, known and favorite words.", "Tekrar zamanı, biliniyor ve favori kelime sayılarını aç."));
        ToolTipService.SetToolTip(wordSummary, U("Kids.Home.WordSummaryHelp", "Open counts for due, known and favorite words.", "Tekrar zamanı, biliniyor ve favori kelime sayılarını aç."));
        var myWordsLabel = U("Kids.Home.MyWords", "My words", "Kelimelerim");
        var myWords = SecondaryButton(myWordsLabel, "\uE82D");
        AutomationProperties.SetAutomationId(myWords, "home.MyWords");
        AutomationProperties.SetHelpText(myWords, U("Kids.Home.MyWordsHelp", "Open your word list.", "Kelime listenizi aç."));
        ToolTipService.SetToolTip(myWords, U("Kids.Home.MyWordsHelp", "Open your word list.", "Kelime listenizi aç."));
        FocusTarget(myWords, "home.MyWords");
        myWords.Click += (_, _) => NavigateTo("words", WordsItem);
        var settingsLabel = U("Kids.Home.Settings", "My settings", "Ayarlarım");
        var settings = SecondaryButton(settingsLabel, "");
        AutomationProperties.SetAutomationId(settings, "home.Settings");
        AutomationProperties.SetHelpText(settings, settingsLabel);
        ToolTipService.SetToolTip(settings, settingsLabel);
        FocusTarget(settings, "home.Settings");
        settings.Click += (_, _) => NavigateTo("profile", ProfileItem);
        var actions = new Grid { ColumnSpacing = 10, RowSpacing = 10, Children = { play, myWords, wordSummary, settings } };
        ConfigureResponsiveGrid(actions, 3, 190);
        PageContent.Children.Add(actions);
        var daily = new StackPanel { Spacing = 6 };
        var dailyLabel = $"{U("Kids.Home.Today", "Words practiced today", "Bugün çalıştığın kelimeler")}: {today} / {_settings.DailyGoal}";
        daily.Children.Add(Heading(dailyLabel, 20));
        var dailyProgress = new ProgressBar { Minimum = 0, Maximum = _settings.DailyGoal, Value = Math.Min(today, _settings.DailyGoal) };
        AutomationProperties.SetAutomationId(dailyProgress, "home.TodayProgress");
        AutomationProperties.SetName(dailyProgress, dailyLabel);
        AutomationProperties.SetHelpText(dailyProgress, U("Kids.Home.TodayProgressHelp", "Words practiced today compared with your daily goal.", "Bugün çalıştığınız kelimeleri günlük hedefinizle karşılaştırır."));
        daily.Children.Add(dailyProgress);
        PageContent.Children.Add(daily);
        AddHomeStorageCard();
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
            var labelText = StudyText(label, 18, emphasis: true);
            AutomationProperties.SetName(labelText, label);
            AutomationProperties.SetHeadingLevel(labelText, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level3);
            content.Children.Add(labelText);
            var accuracyText = $"{T("Stats.Success")}: {accuracy}";
            var accuracyLabel = StudyText(accuracyText, 24, emphasis: true);
            AutomationProperties.SetName(accuracyLabel, accuracyText);
            content.Children.Add(accuracyLabel);
            var totalsText = $"{T("Stats.Correct")}: {correct:N0} · {T("Stats.Wrong")}: {wrong:N0}";
            var totalsLabel = StudyText(totalsText, 15);
            AutomationProperties.SetName(totalsLabel, totalsText);
            content.Children.Add(totalsLabel);
            if (detail is not null)
            {
                var detailLabel = StudyText(detail, 14);
                AutomationProperties.SetName(detailLabel, detail);
                content.Children.Add(detailLabel);
            }
            var surface = StudySurface(content);
            surface.HighContrastAdjustment = ElementHighContrastAdjustment.Auto;
            AutomationProperties.SetName(surface, $"{label}. {accuracyText}. {totalsText}");
            scored.Children.Add(surface);
        }
        Scored(U("Stats.QuizAnswers", "Quiz answers — first attempts, all languages", "Test yanıtları — ilk denemeler, tüm diller"), _progress.QuizCorrectAnswers, _progress.QuizWrongAnswers);
        Scored(U("Stats.GameAnswers", "Game answers — scored attempts, all languages", "Oyun yanıtları — puanlanan denemeler, tüm diller"), _progress.GameCorrectAnswers, _progress.GameWrongAnswers,
            $"{U("Stats.CompletedGames", "Completed games", "Tamamlanan oyunlar")}: {_progress.CompletedGames:N0}");
        ConfigureResponsiveGrid(scored, 2, 280);
        answers.Children.Add(scored);
        answers.Children.Add(BuildScopedGameStatistics());

        var cardsPanel = new StackPanel { Spacing = 10 };
        var cardsTitleText = U("Stats.CardsTab", "Cards", "Kartlar");
        var cardsHint = U("Stats.History", "Cards & history", "Kartlar ve geçmiş");
        AutomationProperties.SetAutomationId(cardsPanel, "stats.Cards.Section");
        AutomationProperties.SetName(cardsPanel, cardsTitleText);
        AutomationProperties.SetHelpText(cardsPanel, cardsHint);
        AutomationProperties.SetAccessibilityView(cardsPanel, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Control);
        var cardsTitle = StudyText(cardsTitleText, 20, emphasis: true);
        AutomationProperties.SetAutomationId(cardsTitle, "stats.Cards");
        AutomationProperties.SetName(cardsTitle, cardsTitleText);
        AutomationProperties.SetHeadingLevel(cardsTitle, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
        cardsPanel.Children.Add(cardsTitle);

        var languagePrefix = _settings.StudyLanguage + ":";
        var cardReviews = _progress.Reviews
            .Where(pair => pair.Key.StartsWith(languagePrefix, StringComparison.Ordinal))
            .Select(pair => pair.Value)
            .ToArray();
        var reviewedCards = cardReviews.Where(review => review.LastReviewed is not null).ToArray();

        var cardsSummary = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        AddStat(cardsSummary, 0, MarkedKnownLabel,
            _progress.KnownWords.Count(key => key.StartsWith(languagePrefix, StringComparison.Ordinal)).ToString("N0"), "");
        AddStat(cardsSummary, 1, T("Home.Favorites"),
            _progress.FavoriteWords.Count(key => key.StartsWith(languagePrefix, StringComparison.Ordinal)).ToString("N0"), "");
        AddStat(cardsSummary, 2, U("Study.Due", "Due", "Tekrar zamanı"),
            cardReviews.Count(review => review.DueDate <= today).ToString("N0"), "");
        ConfigureResponsiveGrid(cardsSummary, 3, 200);
        cardsPanel.Children.Add(cardsSummary);

        var cardActivity = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        AddStat(cardActivity, 0, U("Stats.TodayWords", "Words last reviewed today", "Son tekrarı bugün olan kelimeler"),
            reviewedCards.Count(review => review.LastReviewed == today).ToString("N0"), "");
        AddStat(cardActivity, 1, U("Stats.WeekWords", "Words last reviewed in 7 days", "Son 7 günde tekrarlanan kelimeler"),
            reviewedCards.Count(review => review.LastReviewed >= today.AddDays(-6) && review.LastReviewed <= today).ToString("N0"), "");
        AddStat(cardActivity, 2, CurrentStudyLanguage().NativeName + " · " + U("Stats.AllLevels", "all levels", "tüm seviyeler"),
            reviewedCards.Length.ToString("N0"), "");
        ConfigureResponsiveGrid(cardActivity, 3, 200);
        cardsPanel.Children.Add(cardActivity);
        history.Children.Add(StudySurface(cardsPanel, 14));

        var tabs = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        AutomationProperties.SetAutomationId(tabs, "stats.Sections");
        AutomationProperties.SetName(tabs, T("Stats.Title"));
        AutomationProperties.SetHelpText(tabs, U("Stats.SelectSection", "Open this statistics section.", "Bu istatistik bölümünü aç."));
        AutomationProperties.SetAccessibilityView(tabs, AccessibilityView.Control);
        var sectionStatus = Body("");
        StudyLive(sectionStatus, "stats.Section.Active");
        var content = new Grid();
        var sections = new (StatsSection Section, string Id, string Label, string Hint, FrameworkElement Panel)[]
        {
            (StatsSection.Overview, "overview", U("Stats.Overview", "Overview", "Genel bakış"),
                U("Stats.AllActivity", "All-language activity — today / last 7 days", "Tüm dillerde etkinlik — bugün / son 7 gün"), overview),
            (StatsSection.Answers, "answers", U("Stats.AnswersTab", "Answers", "Yanıtlar"),
                U("Stats.Answers", "Answers & games", "Yanıtlar ve oyunlar"), answers),
            (StatsSection.History, "history", U("Stats.CardsTab", "Cards", "Kartlar"),
                U("Stats.History", "Cards & history", "Kartlar ve geçmiş"), history),
        };
        var sectionTabs = new Dictionary<StatsSection, RadioButton>();

        void ApplyStatsTabVisuals(RadioButton tab, bool selected)
        {
            var palette = AppearancePalette.Current;
            var selectedBorder = AppearancePalette.EnsureBoundaryContrast(palette.Box, palette.Button);
            var defaultBorder = AppearancePalette.EnsureBoundaryContrast(palette.Box, palette.Border);
            var borderColor = selected ? selectedBorder : defaultBorder;
            var backgroundColor = selected
                ? Color.FromArgb(44, selectedBorder.R, selectedBorder.G, selectedBorder.B)
                : palette.Box;
            var foregroundColor = AppearancePalette.EnsureTextContrast(backgroundColor, palette.BoxForeground);
            tab.Background = new SolidColorBrush(backgroundColor);
            tab.Foreground = new SolidColorBrush(foregroundColor);
            tab.BorderBrush = new SolidColorBrush(borderColor);
            tab.BorderThickness = new Thickness(selected ? 2 : 1);
            tab.HighContrastAdjustment = ElementHighContrastAdjustment.Auto;
            tab.UseSystemFocusVisuals = true;
        }

        void Select(StatsSection selected)
        {
            _statsSection = selected;
            foreach (var section in sections)
            {
                var active = section.Section == selected;
                section.Panel.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
                if (sectionTabs.TryGetValue(section.Section, out var tab)) ApplyStatsTabVisuals(tab, active);
                if (!active) continue;
                var status = $"{section.Label} — {section.Hint}";
                Announce(sectionStatus, status);
                AutomationProperties.SetName(sectionStatus, $"{T("Stats.Title")}: {status}");
            }
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
            ApplySettingsSelectorVisuals(tab);
            ApplyStatsTabVisuals(tab, section.Section == _statsSection);
            AutomationProperties.SetName(tab, section.Label);
            var sectionHelp = U("Stats.SelectSection", "Open this statistics section.", "Bu istatistik bölümünü aç.") + " " + section.Hint;
            AutomationProperties.SetHelpText(tab, sectionHelp);
            ToolTipService.SetToolTip(tab, section.Label);
            tab.Loaded += (_, _) =>
            {
                AutomationProperties.SetName(tab, section.Label);
                AutomationProperties.SetHelpText(tab, sectionHelp);
            };
            FocusTarget(tab, "stats.Section." + section.Id);
            tab.Checked += (_, _) => Select(section.Section);
            sectionTabs[section.Section] = tab;
            tabs.Children.Add(tab);
            content.Children.Add(section.Panel);
        }
        ConfigureResponsiveGrid(tabs, 3, 170);
        Select(_statsSection);
        PageContent.Children.Add(tabs);
        PageContent.Children.Add(sectionStatus);
        PageContent.Children.Add(content);
    }

    private void AddProfileLearningControls(StackPanel panel)
    {
        panel.Children.Add(Body(T("Storage.Notice")));
        panel.Children.Add(Body(U("Known.ManualHint", "Cards and Words share known marks. Toggle one in Words, or choose I knew it or Easy in Cards. A due review still appears.", "Kartlar ve Kelimeler biliniyor işaretlerini paylaşır. Kelimeler'de değiştir veya Kartlar'da Biliyordum ya da Kolay'ı seç. Zamanı gelen tekrar yine görünür.")));
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

    private void AddHomeStorageCard()
    {
        var title = U("Kids.Settings.Backups", "Copies of learning progress", "Öğrenme ilerlemesinin kopyaları");
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Heading(title, 20));
        panel.Children.Add(Body(T("Storage.Notice")));
        var manage = CompactStudyAction(SecondaryButton(T("Kids.Settings.GrownUps"), "\uE950"));
        AutomationProperties.SetAutomationId(manage, "home.ManageBackups");
        AutomationProperties.SetHelpText(manage, T("Kids.Help.Backups"));
        ToolTipService.SetToolTip(manage, T("Kids.Help.Backups"));
        FocusTarget(manage, "home.ManageBackups");
        manage.Click += (_, _) =>
        {
            if (_studyBusy || _dialogOpen || _navigationBusy || LoadingRing.IsActive) return;
            _settingsSection = SettingsSection.Account;
            NavigateTo("profile", ProfileItem);
        };
        panel.Children.Add(manage);
        PageContent.Children.Add(Card(panel, 18));
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