using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace YDKE_Windows;

public sealed partial class MainPage
{
    private void OnWindowModeButtonClick(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is not MainWindow window) return;
        window.ToggleWindowMode();
        UpdateWindowModeButton();
    }

    private void OnWindowModeChanged(object? sender, EventArgs e) => UpdateWindowModeButton();

    private void UpdateWindowModeButton()
    {
        var window = App.MainWindow as MainWindow;
        var fullScreen = window?.IsFullScreen == true;
        var label = fullScreen
            ? U("Shell.Windowed", "Windowed", "Pencereli")
            : U("Shell.FullScreen", "Full screen", "Tam ekran");
        WindowModeButton.IsEnabled = window?.CanChangeWindowMode != false;
        WindowModeIcon.Glyph = fullScreen ? "\uE73F" : "\uE740";
        WindowModeText.Text = label;
        AutomationProperties.SetName(WindowModeButton, label);
        AutomationProperties.SetHelpText(WindowModeButton, $"{label} (F11)");
        ToolTipService.SetToolTip(WindowModeButton, $"{label} (F11)");
    }

    private static void ConfigureReadingComboBox(ComboBox control)
    {
        control.FontSize = ReadingSize(18);
        control.MinHeight = 48;
        control.MinWidth = 48;
        var items = new Style(typeof(ComboBoxItem));
        items.Setters.Add(new Setter(Control.FontSizeProperty, ReadingSize(18)));
        items.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 48d));
        items.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 48d));
        control.ItemContainerStyle = items;
    }

    private static void ConfigureReadingDialog(ContentDialog dialog)
    {
        dialog.FontSize = ReadingSize(18);
        dialog.Opened += (_, _) =>
        {
            // Keep native button styles and default/cancel behavior; enlarge only reading and touch targets.
            foreach (var button in GameDescendants(dialog).OfType<Button>())
            {
                button.MinHeight = Math.Max(48, button.MinHeight);
                button.MinWidth = Math.Max(48, button.MinWidth);
                button.FontSize = ReadingSize(18);
                foreach (var label in GameDescendants(button).OfType<TextBlock>())
                {
                    label.FontSize = Math.Max(ReadingSize(18), label.FontSize);
                    label.TextWrapping = TextWrapping.Wrap;
                    label.TextTrimming = TextTrimming.None;
                }
            }
        };
    }

    private UIElement PagedTextContent(string message, string id, int pageLength = 360)
    {
        var pages = new List<string>();
        var remaining = message.Trim();
        while (remaining.Length > pageLength)
        {
            var split = remaining.LastIndexOfAny([' ', '\n'], pageLength);
            if (split < pageLength / 2) split = pageLength;
            pages.Add(remaining[..split].Trim());
            remaining = remaining[split..].TrimStart();
        }
        if (remaining.Length > 0 || pages.Count == 0) pages.Add(remaining);

        var page = 0;
        var text = StudyText("", 18, selectable: true);
        AutomationProperties.SetAutomationId(text, id + ".Text");
        var status = StudyText("", 18, emphasis: true);
        StudyLive(status, id + ".Page");
        var previous = CompactStudyAction(SecondaryButton(U("Shell.PreviousPage", "Previous page", "Önceki sayfa"), ""));
        var next = CompactStudyAction(SecondaryButton(U("Shell.NextPage", "Next page", "Sonraki sayfa"), ""));
        AutomationProperties.SetAutomationId(previous, id + ".Previous");
        AutomationProperties.SetAutomationId(next, id + ".Next");
        void Refresh()
        {
            text.Text = pages[page];
            Announce(status, $"{page + 1} / {pages.Count}");
            previous.IsEnabled = page > 0;
            next.IsEnabled = page + 1 < pages.Count;
        }
        previous.Click += (_, _) => { if (page > 0) { page--; Refresh(); } };
        next.Click += (_, _) => { if (page + 1 < pages.Count) { page++; Refresh(); } };
        var navigation = new Grid { ColumnSpacing = 8, RowSpacing = 8, Children = { previous, next } };
        ConfigureResponsiveGrid(navigation, 2, 150);
        if (pages.Count == 1) navigation.Visibility = Visibility.Collapsed;
        var panel = new StackPanel { Spacing = 12, Children = { status, text, navigation } };
        Refresh();
        return panel;
    }

    private void RestoreDialogFocus(Control? control, string page, string context)
    {
        if (_currentPage != page || StudyContext != context || _studyBusy || _navigationBusy || _dialogOpen || _storageBlocked) return;
        if (control is { IsLoaded: true, IsEnabled: true } && IsWithin(control, this)) control.Focus(FocusState.Programmatic);
    }

    private void RenderHelpPage()
    {
        AddPageHeader(T("Help.Title"), T("Kids.Help.Intro"));
        var topics = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        void Topic(string id, string title, string instruction, Action? openScreen = null)
        {
            var button = CompactStudyAction(SecondaryButton(title, "\uE897"));
            AutomationProperties.SetAutomationId(button, "help." + id);
            button.MinHeight = 64;
            AutomationProperties.SetItemStatus(button, T("Help.ClosedStatus"));
            button.Click += async (_, _) => await ShowHelpTopicAsync(id, title, instruction, button, openScreen);
            topics.Children.Add(button);
        }
        Topic("Screen", T("Help.ScreenTitle"), T("Help.Body") + "\n\n" + T("Help.Shortcuts"));
        Topic("Cards", T("Nav.Cards"), T("Kids.Help.Cards"), () => NavigateTo("cards", CardsItem));
        Topic("Quiz", T("Nav.Quiz"), T("Kids.Help.Quiz"), () => NavigateTo("quiz", QuizItem));
        Topic("Words", T("Words.Title"), T("Kids.Help.Words"), () => NavigateTo("words", WordsItem));
        Topic("Games", T("Help.Games"), T("Kids.Help.Games") + "\n\n" + T("Help.Availability"), () => NavigateTo("simple-games", SimpleGamesItem));
        Topic("Settings", T("Profile.Title"), T("Help.Settings"), () => { _settingsSection = SettingsSection.Learning; NavigateTo("profile", ProfileItem); });
        Topic("Statistics", T("Stats.Title"), T("Help.Statistics"), () => NavigateTo("stats", StatsItem));
        Topic("Cloud", T("Help.CloudTitle"), T("Help.Google") + "\n\n" + T("Help.CloudBackup") + "\n\n" + T("Kids.Help.Backups"),
            () => { _settingsSection = SettingsSection.Account; NavigateTo("profile", ProfileItem); });
        ConfigureResponsiveGrid(topics, 4, Font(200));
        PageContent.Children.Add(topics);
        PageContent.Children.Add(StudyText(CloudOfflineHint, 18, AppearancePalette.Current.BackgroundForegroundBrush));
        var manage = CompactStudyAction(SecondaryButton(T("Kids.Help.GrownUp"), "\uE950"));
        AutomationProperties.SetAutomationId(manage, "help.Backups");
        AutomationProperties.SetHelpText(manage, T("Kids.Help.Backups"));
        manage.Click += (_, _) => { _settingsSection = SettingsSection.Account; NavigateTo("profile", ProfileItem); };
        PageContent.Children.Add(manage);
    }

    private async Task ShowHelpTopicAsync(string id, string title, string instruction, Button source, Action? openScreen)
    {
        if (!IsLoaded || _dialogOpen || _studyBusy || _navigationBusy || LoadingRing.IsActive) return;
        var page = _currentPage;
        var context = StudyContext;
        var open = false;
        _dialogOpen = true;
        SyncAccountStatus();
        try
        {
            // Short pages preserve readable type. An explicit dialog does not vanish
            // when a child switches away to another window while reading.
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot, RequestedTheme = RequestedTheme, Title = title,
                Content = PagedTextContent(instruction, "help." + id, pageLength: 220),
                PrimaryButtonText = openScreen is null ? "" : T("Help.OpenPage"),
                CloseButtonText = T("Help.Close"), DefaultButton = ContentDialogButton.Close,
            };
            AutomationProperties.SetAutomationId(dialog, "help." + id + ".Dialog");
            dialog.Opened += (_, _) => AutomationProperties.SetItemStatus(source, T("Help.OpenStatus"));
            ConfigureReadingDialog(dialog);
            open = await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        catch (Exception ex) { ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error); }
        finally
        {
            _dialogOpen = false;
            AutomationProperties.SetItemStatus(source, T("Help.ClosedStatus"));
            SyncAccountStatus();
            RestoreDialogFocus(source, page, context);
        }
        if (open && IsLoaded && _currentPage == page && StudyContext == context) openScreen?.Invoke();
    }

    private void RenderAboutPage()
    {
        AddPageHeader(T("About.Title"), "YDKE - Yabancı Dil Kelime Ezberleme");
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(StudyText(U("Kids.About", "Learn words with cards, quizzes and games. You can learn without the internet. Keep trying, one word at a time!", "Kartlar, testler ve oyunlarla kelime öğren. İnternet olmadan da öğrenebilirsin. Her seferinde bir kelime, denemeye devam!"), 20, selectable: true));
        content.Children.Add(StudyText($"YDKE {typeof(App).Assembly.GetName().Version}", 18, emphasis: true, selectable: true));
        content.Children.Add(StudyText(CloudOfflineHint, 15));
        var help = SecondaryButton(T("Help.Title"), "\uE897");
        AutomationProperties.SetAutomationId(help, "about.Help");
        help.Click += (_, _) => NavigateTo("help", HelpItem);
        content.Children.Add(help);
        PageContent.Children.Add(StudySurface(content));
    }
}