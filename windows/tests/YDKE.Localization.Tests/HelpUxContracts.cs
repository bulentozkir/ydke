using System.Text;
using System.Text.RegularExpressions;

namespace YDKE_Windows;

// Audit the shipped resource table and renderer, not copies of translated Help.
internal static class HelpUxContracts
{
    public static void Verify(string directory, Action<bool, string> check)
    {
        var checks = 0;
        var failures = 0;
        void Test(bool condition, string message)
        {
            checks++;
            if (!condition) failures++;
            check(condition, "Help UX: " + message);
        }

        var shell = SourceAudit.WithoutComments(File.ReadAllText(Path.Combine(directory, "MainPage.Shell.cs")));
        var start = shell.IndexOf("private void RenderHelpPage()", StringComparison.Ordinal);
        var end = shell.IndexOf("private void RenderAboutPage()", start, StringComparison.Ordinal);
        var help = shell[start..end];
        string[] topics = ["Screen", "Cards", "Quiz", "Words", "Games", "Settings", "Statistics", "Backups"];
        var actualTopics = Regex.Matches(help, "\\bTopic\\(\"(?<id>[^\"]+)\"")
            .Select(match => match.Groups["id"].Value).ToArray();
        Test(actualTopics.SequenceEqual(topics), "all eight current topics must be reachable once in the fixed Help grid");
        foreach (var fragment in new[]
        {
            "PagedTextContent(instruction, \"help.\" + id, pageLength: 220)",
            "CompactStudyAction(SecondaryButton(title", "button.MinHeight = 64",
            "new ContentDialog", "DefaultButton = ContentDialogButton.Close", "ConfigureReadingDialog(dialog)",
            "T(\"Help.OpenPage\")", "T(\"Help.Close\")", "openScreen?.Invoke()",
            "dialog.Opened +=", "AutomationProperties.SetItemStatus(source", "RestoreDialogFocus(source, page, context)",
            "\"help.\" + id + \".Dialog\"", "await dialog.ShowAsync() == ContentDialogResult.Primary",
            "ConfigureResponsiveGrid(topics, 4, Font(200))", "\"help.Backups\"", "SettingsSection.Account",
            "AutomationProperties.SetName(button, title)", "Help.SelectTopic", "AutomationProperties.SetHelpText(button",
        }) Test(help.Contains(fragment, StringComparison.Ordinal), "missing accessible, paged Help behavior: " + fragment);
        foreach (var fragment in new[] { "AutomationProperties.SetName(text, pages[page])",
            "AutomationProperties.SetName(status, $\"{page + 1} / {pages.Count}\")",
            "AutomationProperties.SetHelpText(previous, previousHelp)", "AutomationProperties.SetHelpText(next, nextHelp)" })
            Test(shell.Contains(fragment, StringComparison.Ordinal), "missing accessible Help pager behavior: " + fragment);
        Test(!help.Contains("ToolTipService.SetToolTip", StringComparison.Ordinal), "instructions must be visible on click, not only on hover");
        Test(!Regex.IsMatch(help, @"\b(?:ScrollViewer|Viewbox|ScaleTransform)\b|\bMaxLines\s*="),
            "expanded Help must not add scrolling, shrink text or truncate guidance");
        Test(!Regex.IsMatch(help, @"\b(?:BeginGoogleSignInAsync|CloudSaveAsync|CloudLoadAsync|ImportBackupAsync|ExportBackupAsync)\s*\("),
            "reading Help may navigate to a screen but must not start authentication or a backup operation");
        Test(shell.Contains("AutomationProperties.SetAutomationId(previous, id + \".Previous\")", StringComparison.Ordinal) &&
            shell.Contains("AutomationProperties.SetAutomationId(next, id + \".Next\")", StringComparison.Ordinal),
            "Help pagers need stable Previous/Next IDs for keyboard and runtime checks");

        string[] keys =
        [
            "Help.Intro", "Help.SelectTopic", "Help.ScreenTitle", "Help.Body", "Help.Shortcuts", "Help.Games", "Help.GamesHint", "Help.WordsHint",
            "Help.Availability", "Help.Settings", "Help.Statistics", "Help.BackupsTitle", "Help.Backups",
            "Help.OpenPage", "Help.Close", "Help.OpenStatus", "Help.ClosedStatus", "Kids.Help.Intro", "Kids.Help.Cards", "Kids.Cards.WordListSync", "Kids.Help.Quiz", "Kids.Help.Words",
            "Kids.Help.Games", "Kids.Help.Backups", "Kids.Help.GrownUp", "About.Body",
        ];
        var references = new Dictionary<string, string[]>
        {
            ["Help.Body"] = ["Shell.Windowed", "Shell.FullScreen"],
            ["Kids.Help.Cards"] = ["Kids.Cards.Reveal", "Kids.Rating.Again", "Kids.Rating.Hard", "Kids.Rating.Good", "Kids.Rating.Easy", "Kids.Cards.Undo", "Kids.Cards.WordList"],
            ["Kids.Help.Quiz"] = ["Kids.Quiz.AnswerDetails", "Quiz.Continue"],
            ["Kids.Help.Words"] = ["Library.Previous", "Library.Next", "Library.ClearFilters", "Cards.Listen"],
            ["Kids.Help.Games"] = ["Games.PreviousPage", "Games.NextPage", "Games.HowToPlay", "Kids.Games.Next"],
            ["Help.Statistics"] = ["Stats.Overview", "Stats.AnswersTab", "Stats.CardsTab"],
        };
        Test(Localizer.UiLanguages.Select(language => language.Code).Order().SequenceEqual(new[] { "de", "en", "es", "fr", "nl", "pt", "tr" }),
            "Help must cover exactly the seven supported interface languages");
        foreach (var language in Localizer.UiLanguages)
        {
            Test(ExperienceStrings.MissingKeys(language.Code, keys).Count == 0, language.Code + ": every Help key needs an explicit translation");
            foreach (var key in keys)
            {
                var text = ExperienceStrings.Get(language.Code, key, "__missing__", "__missing__");
                Test(!string.IsNullOrWhiteSpace(text) && text != key && !text.Contains("__missing__", StringComparison.Ordinal),
                    language.Code + "/" + key + ": blank text or fallback");
                Test(text == Localizer.Get(language.Code, key), language.Code + "/" + key + ": UI and resource lookup disagree");
                Test(!text.Contains("|", StringComparison.Ordinal), language.Code + "/" + key + ": raw resource delimiter leaked into Help");
            }
            foreach (var (helpKey, actionKeys) in references)
                foreach (var actionKey in actionKeys)
                    Test(Canonical(Localizer.Get(language.Code, helpKey)).Contains(Canonical(Localizer.Get(language.Code, actionKey)), StringComparison.OrdinalIgnoreCase),
                        language.Code + "/" + helpKey + ": must name the actual control " + actionKey);
            foreach (var shortcut in new[] { "F11", "Esc" })
                Test(Localizer.Get(language.Code, "Help.Body").Contains(shortcut, StringComparison.Ordinal), language.Code + ": missing " + shortcut);
            foreach (var shortcut in new[] { "Tab", "Shift+Tab", "1–4" })
                Test(Localizer.Get(language.Code, "Help.Shortcuts").Contains(shortcut, StringComparison.Ordinal), language.Code + ": missing study shortcut " + shortcut);
            Test(Localizer.Get(language.Code, "Kids.Help.Games").Contains("25", StringComparison.Ordinal), language.Code + ": game count is outdated");
            Test(Localizer.Get(language.Code, "Help.Intro") == Localizer.Get(language.Code, "Kids.Help.Intro"), language.Code + ": Help introductions disagree");
        }

        var backupsHelp = Localizer.Get("en", "Help.Backups");
        Test(backupsHelp.Contains("safety backup", StringComparison.Ordinal) &&
            !backupsHelp.Contains("sign in", StringComparison.OrdinalIgnoreCase),
            "Help must explain safe local replacement without sign-in guidance");
        Test(backupsHelp.Contains("Make a copy", StringComparison.Ordinal) &&
            backupsHelp.Contains("Open a copy", StringComparison.Ordinal),
            "Help must route grown-ups to local backup export/import actions");
        Test(Localizer.Get("en", "Help.Statistics").Contains("last seven days", StringComparison.Ordinal),
            "the statistics period is rolling seven days, not the calendar week");

        var root = Path.GetFullPath(Path.Combine(directory, "..", "..", ".."));
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var activeReadme = readme[..readme.IndexOf("## Archived Documents", StringComparison.Ordinal)];
        var improvements = File.ReadAllText(Path.Combine(root, "windows", "IMPROVEMENTS.md"));
        Test(!activeReadme.Contains("are not implemented", StringComparison.Ordinal) &&
            !improvements.Contains("No Google\nsign-in", StringComparison.Ordinal), "current documentation must not call the optional browser sign-in unimplemented");
        Test(activeReadme.Contains("## Help in your language", StringComparison.Ordinal) && improvements.Contains("## Current multilingual Help", StringComparison.Ordinal),
            "current documentation must direct users to translated, paged Help");
        Console.WriteLine($"HELP_UX checks={checks} errors={failures} topics={topics.Length} locales={Localizer.UiLanguages.Count} profile-io=none");
    }

    private static string Canonical(string text) => Regex.Replace(text.Normalize(NormalizationForm.FormC)
        .Replace('’', '\'').Replace('‘', '\''), @"\s+", " ").Trim();
}