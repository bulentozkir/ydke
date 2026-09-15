using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace YDKE_Windows;

internal static class ShellUxContracts
{
    public static void Verify(string directory, Action<bool, string> check)
    {
        var main = SourceAudit.WithoutComments(File.ReadAllText(Path.Combine(directory, "MainPage.xaml.cs")));
        var shell = SourceAudit.WithoutComments(File.ReadAllText(Path.Combine(directory, "MainPage.Shell.cs")));
        var library = SourceAudit.WithoutComments(File.ReadAllText(Path.Combine(directory, "MainPage.Library.cs")));
        var games = SourceAudit.WithoutComments(File.ReadAllText(Path.Combine(directory, "MainPage.Games.cs")));
        var study = SourceAudit.WithoutComments(File.ReadAllText(Path.Combine(directory, "MainPage.Study.cs")));
        var markup = XDocument.Load(Path.Combine(directory, "MainPage.xaml"));
        var windowSource = SourceAudit.WithoutComments(File.ReadAllText(Path.Combine(directory, "MainWindow.xaml.cs")));
        var windowMarkup = XDocument.Load(Path.Combine(directory, "MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var count = 0;
        void Test(bool value, string reason) { count++; check(value, "Shell UX: " + reason); }
        foreach (var fragment in new[] { "home.TodayProgress", "AutomationProperties.SetName(dailyProgress, dailyLabel)",
            "AutomationProperties.SetAutomationId(button, $\"home.{mode}.Session\")", "FocusTarget(button, $\"home.{mode}.Session\")",
            "Kids.Home.WordSummary", "Kids.Home.WordSummaryHelp", "AutomationProperties.SetAutomationId(myWords, \"home.MyWords\")",
            "NavigateTo(\"words\", WordsItem)", "AutomationProperties.SetAutomationId(settings, \"home.Settings\")",
            "Kids.Home.TodayProgressHelp", "Storage.Notice", "home.ManageBackups" })
            Test(library.Contains(fragment, StringComparison.Ordinal), "Home accessibility contract " + fragment);
        XElement Named(string name) => markup.Descendants().Single(node => (string?)node.Attribute(x + "Name") == name);
        var navigation = Named("Navigation");
        Test((string?)navigation.Attribute("PaneDisplayMode") == "Auto", "navigation must adapt instead of forcing an overlay at every width");
        Test((string?)navigation.Attribute("CompactModeThresholdWidth") == "640" &&
            (string?)navigation.Attribute("ExpandedModeThresholdWidth") == "1280", "navigation breakpoints should preserve space at narrow widths");
        Test((string?)Named("PageContent").Attribute("HorizontalAlignment") == "Left" &&
            (string?)Named("PageBounds").Attribute("AutomationProperties.AutomationId") == "shell.PageContent", "retain the established layout and expose its bounds for no-scroll validation");
        var contentScroll = Named("ContentScroll");
        Test((string?)contentScroll.Attribute("HorizontalScrollMode") == "Disabled" &&
            (string?)contentScroll.Attribute("VerticalScrollMode") == "Disabled" &&
            (string?)contentScroll.Attribute("HorizontalScrollBarVisibility") == "Disabled" &&
            (string?)contentScroll.Attribute("VerticalScrollBarVisibility") == "Disabled",
            "child pages must not pan vertically or horizontally");
        Test(Regex.IsMatch(windowSource, @"AppWindow\.Changed\s*\+=\s*OnAppWindowChanged\s*;\s*EnterMaximizedWindow\s*\(\s*\)\s*;") &&
            windowSource.Contains("ChangeWindowMode(AppWindowPresenterKind.FullScreen)", StringComparison.Ordinal), "app launch must start maximized (windowed) while keeping full-screen support available");
        Test(windowSource.Contains("AppTitleBar.Visibility = sender.Presenter.Kind == AppWindowPresenterKind.FullScreen", StringComparison.Ordinal),
            "the custom title bar must not consume learning space in full-screen mode");
        Test(windowSource.Contains("args.Key == VirtualKey.F11", StringComparison.Ordinal) &&
            windowSource.Contains("args.Key == VirtualKey.Escape", StringComparison.Ordinal) &&
            windowSource.Contains("ChangeWindowMode(AppWindowPresenterKind.Default)", StringComparison.Ordinal), "F11 and Escape must provide a safe windowed exit from full screen");
        Test((string?)windowMarkup.Root?.Descendants().First(node => (string?)node.Attribute(x + "Name") == "WindowRoot").Attribute("PreviewKeyDown") == "OnWindowRootPreviewKeyDown",
            "the full-screen exit keys must be wired at the window root");
        var modeButton = Named("WindowModeButton");
        Test((string?)modeButton.Attribute("AutomationProperties.AutomationId") == "shell.WindowMode" &&
            modeButton.Ancestors().Any(node => (string?)node.Attribute(x + "Name") == "QuickSettingsBar"),
            "the window-mode action must stay in the fixed quick-settings toolbar");
        Test((string?)modeButton.Attribute("MinHeight") == "64" && (string?)modeButton.Attribute("MinWidth") == "96",
            "the window-mode action must be an easy child-sized target");
        Test(windowSource.Contains("internal void ToggleWindowMode()", StringComparison.Ordinal) &&
            shell.Contains("window.ToggleWindowMode()", StringComparison.Ordinal) &&
            shell.Contains("Shell.FullScreen", StringComparison.Ordinal) && shell.Contains("Shell.Windowed", StringComparison.Ordinal),
            "the toolbar action must toggle both modes and describe its next action");
        Test(windowSource.Contains("TimeSpan.FromMilliseconds(500)", StringComparison.Ordinal) &&
            windowSource.Contains("if (_windowModeChanging || mode == AppWindow.Presenter.Kind) return;", StringComparison.Ordinal) &&
            shell.Contains("window?.CanChangeWindowMode", StringComparison.Ordinal),
            "rapid clicks and repeated shortcut keys must not overlap native presenter transitions");
        Test(windowSource.Contains("PreferredMinimumWidth = Math.Min((int)Math.Round(800 * scale)", StringComparison.Ordinal) &&
            windowSource.Contains("PreferredMinimumHeight = Math.Min((int)Math.Round(680 * scale)", StringComparison.Ordinal),
            "windowed mode must preserve a viable no-scroll viewport instead of allowing unusably tiny windows");
        Test((string?)Named("Notice").Attribute("Grid.Row") == "2" && (string?)Named("ContentScroll").Attribute("Grid.Row") == "1",
            "notifications must not overlay study content");
        foreach (var id in new[] { "shell.StudyLanguage", "shell.Level", "shell.WindowMode", "shell.ContentScroll", "shell.PageContent" })
            Test(markup.Descendants().Any(node => (string?)node.Attribute("AutomationProperties.AutomationId") == id), "missing stable shell control " + id);
        Test(!markup.Descendants().Any(node => (string?)node.Attribute("AutomationProperties.AutomationId") == "shell.AppLanguage") &&
            !main.Contains("QuickUiLanguage", StringComparison.Ordinal) &&
            main.Contains("ConfigureResponsiveGrid(QuickSettingsGrid, 2, 170)", StringComparison.Ordinal),
            "App language must not remain in the fixed toolbar; the toolbar keeps only learning language and level.");
        foreach (var fragment in new[] { "ApplyQuickSettingsVisuals()", "EnsureTextContrast(toolbarBackground, palette.BoxForeground)",
            "ApplyAccessibleButtonVisuals(WindowModeButton", "ButtonBackground{state}" })
            Test(main.Contains(fragment, StringComparison.Ordinal), "accessible toolbar visual contract " + fragment);
        Test(markup.Descendants().Where(node => node.Name.LocalName == "FontIcon")
            .All(node => (string?)node.Attribute("AutomationProperties.AccessibilityView") == "Raw"),
            "shell glyphs must be decorative; their named parent controls and adjacent text carry the accessible labels");
        foreach (var fragment in new[] { "while (remaining.Length > pageLength)", "LastIndexOfAny", "previous.IsEnabled = page > 0", "next.IsEnabled = page + 1 < pages.Count" })
            Test(shell.Contains(fragment, StringComparison.Ordinal), "paged dialog text contract " + fragment);
        Test(main.Contains("Content = content", StringComparison.Ordinal) &&
            shell.Contains("Content = PagedTextContent(instruction, \"help.\" + id", StringComparison.Ordinal) &&
            study.Contains("Content = PagedTextContent(message, \"dialog.Confirmation\")", StringComparison.Ordinal) &&
            !main.Contains("ScrollableDialogContent", StringComparison.Ordinal) &&
            !shell.Contains("ScrollableDialogContent", StringComparison.Ordinal) &&
            !library.Contains("ScrollableDialogContent", StringComparison.Ordinal) &&
            !study.Contains("ScrollableDialogContent", StringComparison.Ordinal), "app-owned modals must use direct or paged content with no nested scrolling");
        Test(main.Contains("ConfigureReadingDialog(dialog)", StringComparison.Ordinal) &&
            shell.Contains("ConfigureReadingDialog(dialog)", StringComparison.Ordinal) &&
            study.Contains("ConfigureReadingDialog(dialog)", StringComparison.Ordinal), "all native dialogs must enlarge their existing button targets");
        foreach (var fragment in new[] { "button.MinHeight = Math.Max(48, button.MinHeight)", "button.MinWidth = Math.Max(48, button.MinWidth)",
            "label.FontSize = Math.Max(ReadingSize(18), label.FontSize)", "label.TextTrimming = TextTrimming.None" })
            Test(shell.Contains(fragment, StringComparison.Ordinal), "dialog reading/touch contract " + fragment);
        Test(study.Contains("PagedTextContent(message, \"dialog.Confirmation\")", StringComparison.Ordinal), "confirmation messages need readable Previous/Next text pages");
        Test(main.Contains("ConfigureReadingComboBox(control)", StringComparison.Ordinal) &&
            library.Contains("ConfigureReadingComboBox(categories)", StringComparison.Ordinal), "quick settings and library categories must size dropdown items as well as the selected value");
        foreach (var fragment in new[] { "new Style(typeof(ComboBoxItem))", "new Setter(Control.FontSizeProperty, ReadingSize(18))",
            "new Setter(FrameworkElement.MinHeightProperty, 48d)", "control.ItemContainerStyle = items" })
            Test(shell.Contains(fragment, StringComparison.Ordinal), "dropdown reading/touch contract " + fragment);
        Test(Regex.Matches(library, @"FontSize = ReadingSize\(18\), MinHeight = 48, MinWidth = 48").Count >= 3,
            "library search, filters and word disclosures need explicit child-sized controls");
        foreach (var fragment in new[] { "_currentPage != page", "StudyContext != context", "_storageBlocked", "IsLoaded: true, IsEnabled: true", "IsWithin(control, this)" })
            Test(shell.Contains(fragment, StringComparison.Ordinal), "focus restore must guard " + fragment);
        Test(main.Contains("RestoreDialogFocus(focus, page, context)", StringComparison.Ordinal) && study.Contains("RestoreDialogFocus(focus, page, context)", StringComparison.Ordinal),
            "rules and confirmation must return keyboard focus safely");
        Test(main.Contains("ContentScroll.IsEnabled = !loading && !_studyBusy", StringComparison.Ordinal) &&
            main.Contains("_dialogOpen || LoadingRing.IsActive", StringComparison.Ordinal), "loading must prevent stale page actions/navigation");
        Test(main.Contains("AutomationHeadingLevel.Level1", StringComparison.Ordinal), "page title must be exposed as a heading");
        Test(!main.Contains("Opacity = 0.6", StringComparison.Ordinal), "page subtitles must not be washed out");
        Test(main.Contains("PagedTextContent(message, \"notice.Details\")", StringComparison.Ordinal) &&
            main.Contains("StudyPopupButton(", StringComparison.Ordinal) && shell.Contains("selectable: true", StringComparison.Ordinal),
            "long notices must remain readable and copyable without adding a scroll region");
        foreach (var fragment in new[] { "TimeSpan.FromMilliseconds(180)", "searchTimer.Tick -= SearchTick", "search.QuerySubmitted -= SearchSubmitted", "IsCurrentRender()",
            "practice.IsEnabled = previous.IsEnabled = next.IsEnabled = false", "Refresh(resetPage: true)", "_libraryFavorites = _libraryKnown = _libraryDue = false",
            "matches.Skip(_libraryPage).Take(1)", "library.ResultCount", "library.Empty", "library.ClearFilters", "library.Previous", "library.Next",
            "Library.Repeat", "Library.RepeatHelp", "LearningEngine.ScheduleReview(_progress, entry.Key, Today)", "library.Repeat." })
            Test(library.Contains(fragment, StringComparison.Ordinal), "library search/empty-state contract " + fragment);
        foreach (var fragment in new[] { "Library.FavoritesFilterHelp", "Library.KnownFilterHelp", "Library.DueFilterHelp",
            "Library.ListenHelp", "Library.AudioGroup", "Library.ReviewGroup", "Library.MarksGroup", "Library.WordActions", "Library.WordActionsHelp", "Library.WordDetailsExpanded", "Library.WordDetailsCollapsed",
            "var practiceLabel = U(\"Library.Practice\"", "var practiceHelp = U(\"Library.PracticeHelp\"",
            "ToolTipService.SetToolTip(practice, practiceHelp)", "UpdateWordDetailsAccessibility()", "RegisterPropertyChangedCallback(Expander.IsExpandedProperty" })
            Test(library.Contains(fragment, StringComparison.Ordinal), "Words accessibility contract " + fragment);
        Test(!library.Contains("U(\"Library.FilterHelp\"", StringComparison.Ordinal),
            "Words filters must not reuse one generic help description");
        foreach (var (element, labelKey) in new[] { ("audio", "Library.AudioGroup"), ("repeat", "Library.ReviewGroup"), ("marks", "Library.MarksGroup") })
        {
            Test(WordActionHasSingleParent(library, element, labelKey),
                $"Words {element} must belong to exactly one action group, never also to the outer actions grid");
            Test(!WordActionHasSingleParent(library + $"\nactions.Children.Add({element});", element, labelKey),
                $"the ownership guard must reject the original duplicate-parent failure for {element}");
        }
        Test(Regex.Matches(library, @"\bStudySurface\s*\(\s*details\s*,\s*14\s*\)").Count == 1 &&
            library.Contains("expander.Content = detailSurface;", StringComparison.Ordinal) &&
            library.Contains("expander.Content = null;", StringComparison.Ordinal) &&
            library.Contains("fallbackStack.Children.Add(detailSurface);", StringComparison.Ordinal),
            "Words fallback must detach and reuse the original detail surface instead of parenting its contents twice");
        Test(library.Contains("Open the test islands for this level. Search filters do not change the tests.", StringComparison.Ordinal) &&
            library.Contains("Bu seviyenin test adacıklarını aç. Arama filtreleri testleri değiştirmez.", StringComparison.Ordinal) &&
            library.Contains("ShowQuizExamPicker()", StringComparison.Ordinal) &&
            !library.Contains("StartStudySessionAsync", StringComparison.Ordinal) &&
            library.Contains("mode == \"quiz\" ? T(\"Kids.Quiz.ChooseExam\")", StringComparison.Ordinal),
            "Words and Home must open the numbered-island picker, never resume or create a quick filtered quiz");
        Test(library.Contains("showing word {1:N0} of {0:N0}", StringComparison.Ordinal) &&
            library.Contains("kelime arasından {1:N0}. kelime gösteriliyor", StringComparison.Ordinal),
            "Words result count must be explicit in its fallback text");
        Test(!library.Contains("ContentScroll.VerticalOffset", StringComparison.Ordinal) &&
            !library.Contains("ContentScroll.ScrollableHeight", StringComparison.Ordinal) &&
            !library.Contains("library.More", StringComparison.Ordinal), "the word browser must page one complete word instead of retaining or extending a scroll list");
        foreach (var fragment in new[] { "foreach (var game in filtered)", "games.CatalogCount", "Games.CatalogCount" })
            Test(games.Contains(fragment, StringComparison.Ordinal), "single-window game catalog contract " + fragment);
        foreach (var fragment in new[] { "games.PreviousPage", "games.NextPage", "_gameCatalogPage", "Games.CatalogPage" })
            Test(!games.Contains(fragment, StringComparison.Ordinal), "single-window catalog must not retain paging " + fragment);
        Test(main.Contains("ConfigureResponsiveGrid(grid, 4, 180)", StringComparison.Ordinal),
            "the complete game catalog must use a compact responsive grid");
        Test(main.Contains("MinHeight = 84", StringComparison.Ordinal) &&
            main.Contains("ToolTipService.SetToolTip(button, GameTask(game))", StringComparison.Ordinal) &&
            !main.Contains("var description = Body(GameTask(game))", StringComparison.Ordinal),
            "single-window catalog cards must stay compact while exposing each task through accessible help and a tooltip");
        Test(library.Contains("AutomationProperties.SetAutomationId(cardsTitle, \"stats.Cards\")", StringComparison.Ordinal) &&
            library.Contains("AutomationHeadingLevel.Level2", StringComparison.Ordinal) &&
            library.Contains("history.Children.Add(StudySurface(cardsPanel, 14))", StringComparison.Ordinal),
            "Cards statistics must render as an inline section in Statistics > Cards.");
        foreach (var fragment in new[] { "ApplySettingsSelectorVisuals(tab)", "AutomationProperties.SetName(tab, section.Label)",
            "Stats.SelectSection", "AutomationProperties.SetHelpText(tab", "ToolTipService.SetToolTip(tab, section.Label)" })
            Test(library.Contains(fragment, StringComparison.Ordinal), "statistics section selector accessibility contract " + fragment);
        foreach (var fragment in new[] { "AutomationProperties.SetAutomationId(cardsPanel, \"stats.Cards.Section\")",
            "AutomationProperties.SetName(cardsPanel, cardsTitleText)", "AutomationProperties.SetHelpText(cardsPanel, cardsHint)",
            "AccessibilityView.Control", "AddStat(cardsSummary, 0, MarkedKnownLabel",
            "AddStat(cardsSummary, 1, T(\"Home.Favorites\")", "AddStat(cardsSummary, 2, U(\"Study.Due\"",
            "AddStat(cardActivity, 0, U(\"Stats.TodayWords\"", "AddStat(cardActivity, 1, U(\"Stats.WeekWords\"",
            "CurrentStudyLanguage().NativeName + \" · \" + U(\"Stats.AllLevels\"",
            "ConfigureResponsiveGrid(cardsSummary, 3, 200)", "ConfigureResponsiveGrid(cardActivity, 3, 200)" })
            Test(library.Contains(fragment, StringComparison.Ordinal), "card statistics summary contract " + fragment);
        foreach (var fragment in new[] { "Stats.RatingCount", "stats.Recall.", "RatingVisual(rating)", "RecallRatings.GetValueOrDefault",
            "Stats.PreviousRatings", "Stats.NextRatings", "Stats.LegacyShort", "\"stats.Legacy\"" })
            Test(!library.Contains(fragment, StringComparison.Ordinal), "retired card self-rating/legacy UI must stay removed " + fragment);
        foreach (var fragment in new[] { "AutomationProperties.SetName(card, $\"{label}: {value}\")", "card.HighContrastAdjustment = ElementHighContrastAdjustment.Auto" })
            Test(main.Contains(fragment, StringComparison.Ordinal), "statistics metric color-and-text contract " + fragment);
        foreach (var fragment in new[] { "AutomationProperties.SetName(accuracyLabel, accuracyText)",
            "AutomationProperties.SetName(totalsLabel, totalsText)", "surface.HighContrastAdjustment = ElementHighContrastAdjustment.Auto" })
            Test(library.Contains(fragment, StringComparison.Ordinal), "statistics score color-and-text contract " + fragment);
        Test(main.Contains("private static FontIcon DecorativeIcon", StringComparison.Ordinal) &&
            main.Contains("AutomationProperties.SetAccessibilityView(icon, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw)", StringComparison.Ordinal),
            "decorative glyphs must not be announced as meaningless labels.");
        Test(!Regex.IsMatch(main, @"Opacity\s*=\s*0\.[0-9]+") && !Regex.IsMatch(study, @"Opacity\s*=\s*0\.[0-9]+"),
            "active MainPage text and label surfaces must not use washed-out opacity styling.");
        Test(Regex.Matches(library, @"BuildScopedGameStatistics\(\)").Count == 1 &&
            library.Contains("stats.Section.overview", StringComparison.Ordinal) == false &&
            library.Contains("\"stats.Section.\" + section.Id", StringComparison.Ordinal), "statistics must use one tabbed, paged game-score surface");
        foreach (var fragment in new[] { "case \"help\": RenderHelpPage()", "case \"about\": RenderAboutPage()" })
            Test(main.Contains(fragment, StringComparison.Ordinal), "active information route " + fragment);
        foreach (var id in new[] { "Cards", "Quiz", "Games", "Words" })
            Test(shell.Contains("Topic(\"" + id + "\"", StringComparison.Ordinal), "help must cover " + id);
        Test(shell.Contains("help.Backups", StringComparison.Ordinal) && shell.Contains("about.Help", StringComparison.Ordinal), "secondary pages need useful next actions");
        Console.WriteLine($"SHELL_UX checks={count} profile-io=none");
    }

    // Source-only regression: grouping a control already added to a Grid compiles,
    // but WinUI rejects the second parent at runtime before the word card is shown.
    private static bool WordActionHasSingleParent(string source, string element, string labelKey)
    {
        var name = Regex.Escape(element);
        var groups = Regex.Matches(source,
            $@"\bActionGroup\s*\(\s*U\s*\(\s*""(?<label>[^""]+)""[^;\r\n]*?\)\s*,\s*{name}\s*\)");
        return groups.Count == 1 && groups[0].Groups["label"].Value == labelKey &&
            !Regex.IsMatch(source, $@"\.\s*Children\s*\.\s*Add\s*\(\s*{name}\s*\)");
    }
}