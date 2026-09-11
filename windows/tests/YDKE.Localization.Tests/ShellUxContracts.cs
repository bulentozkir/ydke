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
        Test(windowSource.Contains("EnterFullScreen();", StringComparison.Ordinal) &&
            windowSource.Contains("ChangeWindowMode(AppWindowPresenterKind.FullScreen)", StringComparison.Ordinal), "every app launch must enter true full-screen presentation");
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
        foreach (var id in new[] { "shell.AppLanguage", "shell.StudyLanguage", "shell.Level", "shell.WindowMode", "shell.ContentScroll", "shell.PageContent" })
            Test(markup.Descendants().Any(node => (string?)node.Attribute("AutomationProperties.AutomationId") == id), "missing stable shell control " + id);
        foreach (var fragment in new[] { "while (remaining.Length > pageLength)", "LastIndexOfAny", "previous.IsEnabled = page > 0", "next.IsEnabled = page + 1 < pages.Count" })
            Test(shell.Contains(fragment, StringComparison.Ordinal), "paged dialog text contract " + fragment);
        Test(main.Contains("Content = content", StringComparison.Ordinal) &&
            library.Contains("Content = content, FontSize", StringComparison.Ordinal) &&
            study.Contains("Content = PagedTextContent(message, \"dialog.Confirmation\")", StringComparison.Ordinal) &&
            !main.Contains("ScrollableDialogContent", StringComparison.Ordinal) &&
            !library.Contains("ScrollableDialogContent", StringComparison.Ordinal) &&
            !study.Contains("ScrollableDialogContent", StringComparison.Ordinal), "app-owned modals must use direct or paged content with no nested scrolling");
        Test(main.Contains("ConfigureReadingDialog(dialog)", StringComparison.Ordinal) &&
            library.Contains("ConfigureReadingDialog(waitDialog)", StringComparison.Ordinal) &&
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
            "matches.Skip(_libraryPage).Take(1)", "library.ResultCount", "library.Empty", "library.ClearFilters", "library.Previous", "library.Next" })
            Test(library.Contains(fragment, StringComparison.Ordinal), "library search/empty-state contract " + fragment);
        Test(!library.Contains("ContentScroll.VerticalOffset", StringComparison.Ordinal) &&
            !library.Contains("ContentScroll.ScrollableHeight", StringComparison.Ordinal) &&
            !library.Contains("library.More", StringComparison.Ordinal), "the word browser must page one complete word instead of retaining or extending a scroll list");
        foreach (var fragment in new[] { "Take(4)", "games.PreviousPage", "games.NextPage", "_gameCatalogPage", "Games.CatalogPage" })
            Test(games.Contains(fragment, StringComparison.Ordinal), "game catalog paging contract " + fragment);
        Test(main.Contains("ConfigureResponsiveGrid(grid, 2, 300)", StringComparison.Ordinal),
            "four-game catalog pages must retain two columns at an 800-pixel window");
        foreach (var id in new[] { "stats.Recall", "stats.Legacy" })
            Test(library.Contains('"' + id + '"', StringComparison.Ordinal), "historical metric disclosure " + id);
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
}