using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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
        var previousHelp = U("Shell.PreviousPage", "Previous page", "Önceki sayfa");
        var nextHelp = U("Shell.NextPage", "Next page", "Sonraki sayfa");
        AutomationProperties.SetHelpText(previous, previousHelp);
        AutomationProperties.SetHelpText(next, nextHelp);
        void Refresh()
        {
            text.Text = pages[page];
            AutomationProperties.SetName(text, pages[page]);
            Announce(status, $"{page + 1} / {pages.Count}");
            AutomationProperties.SetName(status, $"{page + 1} / {pages.Count}");
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
        var document = HelpContent.Get(_settings.UiLanguage);
        string Detail(params string[] ids) => string.Join("\n\n", ids.Select(id =>
            document.Sections.First(section => section.Id == id) is { } section
                ? $"{section.Title}\n{section.Body}"
                : ""));
        var topics = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        void Topic(string id, string title, string instruction, Action? openScreen = null)
        {
            var button = CompactStudyAction(SecondaryButton(title, "\uE897"));
            ApplyHelpTopicButtonVisuals(button);
            AutomationProperties.SetAutomationId(button, "help." + id);
            button.MinHeight = 64;
            AutomationProperties.SetItemStatus(button, T("Help.ClosedStatus"));
            AutomationProperties.SetName(button, title);
            AutomationProperties.SetHelpText(button, U("Help.SelectTopic",
                "Open help for this topic.", "Bu konu için yardımı aç."));
            button.Click += async (_, _) => await ShowHelpTopicAsync(id, title, instruction, button, openScreen);
            topics.Children.Add(button);
        }
        Topic("Screen", T("Help.ScreenTitle"), Detail("start", "accessibility"));
        Topic("Cards", T("Nav.Cards"), Detail("cards"), () => NavigateTo("cards", CardsItem));
        Topic("Quiz", T("Nav.Quiz"), Detail("quiz"), () => NavigateTo("quiz", QuizItem));
        Topic("Words", T("Words.Title"), Detail("words"), () => NavigateTo("words", WordsItem));
        Topic("Games", T("Help.Games"), Detail("games"), () => NavigateTo("simple-games", SimpleGamesItem));
        Topic("Settings", T("Profile.Title"), Detail("settings"), () => { _settingsSection = SettingsSection.Learning; NavigateTo("profile", ProfileItem); });
        Topic("Statistics", T("Stats.Title"), Detail("statistics"), () => NavigateTo("stats", StatsItem));
        Topic("Backups", T("Help.BackupsTitle"), Detail("cloud", "audio", "data"),
            () => { _settingsSection = SettingsSection.Account; NavigateTo("profile", ProfileItem); });
        ConfigureResponsiveGrid(topics, 4, Font(200));
        PageContent.Children.Add(topics);
        var manage = CompactStudyAction(SecondaryButton(T("Kids.Help.GrownUp"), "\uE950"));
        ApplyHelpTopicButtonVisuals(manage);
        AutomationProperties.SetAutomationId(manage, "help.Backups");
        AutomationProperties.SetHelpText(manage, T("Kids.Help.Backups"));
        manage.Click += (_, _) => { _settingsSection = SettingsSection.Account; NavigateTo("profile", ProfileItem); };
        PageContent.Children.Add(manage);

        var documentPanel = new StackPanel { Spacing = 16, MinWidth = 0 };
        var intro = StudyText(document.Intro, 20, new SolidColorBrush(AppearancePalette.Current.BoxForeground), selectable: true);
        AutomationProperties.SetAutomationId(intro, "help.Document.Intro");
        AutomationProperties.SetName(intro, document.Intro);
        documentPanel.Children.Add(intro);
        foreach (var section in document.Sections)
        {
            var heading = StudyText(section.Title, 24, new SolidColorBrush(AppearancePalette.Current.BoxForeground), emphasis: true);
            AutomationProperties.SetHeadingLevel(heading, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
            AutomationProperties.SetName(heading, section.Title);
            var body = StudyText(section.Body, 18, new SolidColorBrush(AppearancePalette.Current.BoxForeground), selectable: true);
            AutomationProperties.SetName(body, section.Body);
            var copy = new StackPanel { Spacing = 12, MinWidth = 0, Children = { heading, body } };
            var surface = StudySurface(copy, 18, AppearancePalette.Current.BoxBrush, AppearancePalette.Current.BorderBrush);
            surface.HighContrastAdjustment = ElementHighContrastAdjustment.Auto;
            AutomationProperties.SetAutomationId(surface, "help.Document." + section.Id);
            AutomationProperties.SetName(surface, section.Title);
            documentPanel.Children.Add(surface);
        }
        PageContent.Children.Add(documentPanel);
    }

    private async Task ShowHelpTopicAsync(string id, string title, string instruction, Button source, Action? openScreen)
    {
        if (!IsLoaded || _dialogOpen || _studyBusy || _navigationBusy || LoadingRing.IsActive) return;
        var page = _currentPage;
        var context = StudyContext;
        var open = false;
        _dialogOpen = true;
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
            RestoreDialogFocus(source, page, context);
        }
        if (open && IsLoaded && _currentPage == page && StudyContext == context) openScreen?.Invoke();
    }

    private static void ApplyHelpTopicButtonVisuals(Button button)
    {
        var palette = AppearancePalette.Current;
        var background = AppearancePalette.EnsureFillContrast(palette.Box, palette.Button, 4.5);
        var foreground = AppearancePalette.EnsureTextContrast(background, Microsoft.UI.Colors.White);
        var border = AppearancePalette.EnsureBoundaryContrast(background, palette.ButtonBorder);

        ApplyAccessibleButtonVisuals(button, background, foreground, border);
        button.BorderThickness = new Thickness(2);
        button.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        button.Padding = new Thickness(12, 10, 12, 10);

        var pointerBackground = AppearancePalette.EnsureFillContrast(background, palette.ButtonTint, 3);
        var pointerForeground = AppearancePalette.EnsureTextContrast(pointerBackground, foreground);
        var pointerBorder = AppearancePalette.EnsureBoundaryContrast(pointerBackground, palette.ButtonBorder);
        SetButtonResource(button, "ButtonBackgroundPointerOver", new SolidColorBrush(pointerBackground));
        SetButtonResource(button, "ButtonForegroundPointerOver", new SolidColorBrush(pointerForeground));
        SetButtonResource(button, "ButtonBorderBrushPointerOver", new SolidColorBrush(pointerBorder));

        var pressedBackground = AppearancePalette.EnsureFillContrast(background, palette.Background, 3);
        var pressedForeground = AppearancePalette.EnsureTextContrast(pressedBackground, foreground);
        var pressedBorder = AppearancePalette.EnsureBoundaryContrast(pressedBackground, palette.ButtonBorder);
        SetButtonResource(button, "ButtonBackgroundPressed", new SolidColorBrush(pressedBackground));
        SetButtonResource(button, "ButtonForegroundPressed", new SolidColorBrush(pressedForeground));
        SetButtonResource(button, "ButtonBorderBrushPressed", new SolidColorBrush(pressedBorder));

        SetButtonResource(button, "ButtonBackgroundFocused", new SolidColorBrush(pointerBackground));
        SetButtonResource(button, "ButtonForegroundFocused", new SolidColorBrush(pointerForeground));
        SetButtonResource(button, "ButtonBorderBrushFocused", new SolidColorBrush(pointerBorder));
    }

    private void RenderAboutPage()
    {
        AddPageHeader(T("About.Title"), "YDKE - Yabancı Dil Kelime Ezberleme");
        var content = new StackPanel { Spacing = 16, MinWidth = 0 };
        content.Children.Add(AboutLicenseSection());
        content.Children.Add(AboutSection("YDKE nedir · What is YDKE",
            AboutParagraph("YDKE, Türk öğrenciler için hazırlanmış yerel ve çevrimdışı bir yabancı dil kelime öğrenme uygulamasıdır. Kartlar, testler, kelime listeleri, istatistikler ve oyunlarla her seferinde bir kelime çalışabilirsin."),
            AboutParagraph("YDKE is a native Windows vocabulary trainer for Turkish learners. It works offline by default and combines cards, quizzes, word lists, statistics and games in one local application."),
            AboutParagraph($"YDKE {typeof(App).Assembly.GetName().Version}", emphasis: true)));
        content.Children.Add(AboutSection("Diller ve seviyeler · Languages and levels",
            AboutParagraph("İngilizce, Almanca, Fransızca, İtalyanca, İspanyolca, Portekizce ve Hollandaca kelime setleri desteklenir. İngilizce, Almanca, Fransızca, İspanyolca, Portekizce ve Hollandaca için A1–C2 seviyeleri; İtalyanca için mevcut yerel veri setleri kullanılabilir."),
            AboutParagraph("Supported study languages include English, German, French, Italian, Spanish, Portuguese and Dutch. CEFR levels A1–C2 are available across the supported datasets where content exists."),
            AboutBullet("English · phrasal verbs · TOEFL and CEFR vocabulary"),
            AboutBullet("Deutsch · Partikelverben and CEFR vocabulary"),
            AboutBullet("Français, Italiano, Español, Português and Nederlands")));
        content.Children.Add(AboutSection("Çalışma modları · Study modes",
            AboutParagraph("Kartlar: kelimeyi gör, anlamı ve örneği aç, sonra nasıl hatırladığını seç. Biliyordum ve Kolay seçimleri Kelimelerim'deki biliniyor işaretleriyle paylaşılır; yanlış seçimler için Geri al kullanılabilir."),
            AboutParagraph("Test: anlamı oku, doğru kelimeyi seç, geri bildirimi incele ve yanıt kaydedildikten sonra Devam ile ilerle."),
            AboutParagraph("Oyunlar: 25 oyun, kelime listeleri, kategori filtreleri, okuduğunu anlama, sesli çalışma ve seviyeye göre değişen oyun içerikleri."),
            AboutParagraph("Study modes include cards with self-rating, saved-answer quizzes, searchable word lists, 25 games, reading comprehension, local speech playback and progress statistics.")));
        content.Children.Add(AboutSection("Sınavlar ve CEFR · Exams and CEFR",
            AboutParagraph("Kelime içerikleri YDS, YÖKDİL, ÜDS, TOEFL, telc ve CEFR çerçevesiyle çalışan öğrenciler için yardımcı çalışma materyali olarak tasarlanmıştır. YDKE bu sınavların sahibi, düzenleyicisi veya onaylayıcısı değildir."),
            AboutParagraph("The vocabulary content is intended as supplementary practice for learners working with YDS, YÖKDİL, ÜDS, TOEFL, telc and CEFR-aligned goals. YDKE is not affiliated with, endorsed by or an official product of those exam providers.")));
        content.Children.Add(AboutSection("Çevrimdışı kullanım ve veri · Offline use and data",
            AboutParagraph("Öğrenme içeriği uygulamayla birlikte gelir. Ayarlar, ilerleme, favoriler, biliniyor işaretleri ve oturumlar varsayılan olarak bu cihazda saklanır. Öğrenmek için internet gerekmez."),
            AboutParagraph("YDKE yalnızca yerel kullanım için tasarlanmıştır. İlerleme bu cihazda saklanır. Ayarlar > Büyükler için bölümündeki Bir kopya oluştur ve Bir kopya aç işlemleriyle yerel yedek alabilir veya geri yükleyebilirsin."),
            AboutParagraph("Learning content is bundled locally. Settings, progress, bookmarks and sessions stay on this device. Use Make a copy and Open a copy in Settings > For grown-ups for local backup and restore.")));
        content.Children.Add(AboutSection("Kaynaklar ve lisanslar · Sources and licences",
            AboutParagraph("Kelime listelerindeki bazı tanımlar ve örnek cümleler açık topluluk projelerinden alınmıştır. Bu içerikler kendi lisanslarını korur; YDKE uygulamasının kaynak kodu ve arayüz lisansı bu içeriklere uygulanmaz."),
            AboutParagraph("Some definitions and example sentences come from open community projects. Their original licences remain in force and are separate from the licence for YDKE's own source code and interface."),
            AboutBullet("Wiktionary · CC BY-SA 4.0"),
            AboutBullet("Tatoeba Project · CC BY 2.0 FR"),
            AboutParagraph("Tatoeba örnek cümlelerinin Türkçe çevirileri makine üretimi olabilir."),
            AboutExternalLink("PolyForm Noncommercial 1.0.0 · full licence text ↗", "https://polyformproject.org/licenses/noncommercial/1.0.0", "about.LicenseLink")));
        var help = CompactStudyAction(SecondaryButton(T("Help.Title"), "\uE897"));
        AutomationProperties.SetAutomationId(help, "about.Help");
        AutomationProperties.SetHelpText(help, T("Help.Intro"));
        content.Children.Add(AboutSection("Yardım ve devam · Help and continue",
            AboutParagraph("Yardım bölümünde ekranları, klavye kısayollarını, çalışma akışlarını, ayarları ve yedekleri konu konu okuyabilirsin."),
            help));
        help.Click += (_, _) => NavigateTo("help", HelpItem);
        PageContent.Children.Add(content);
    }

    private Border AboutLicenseSection()
    {
        var content = new List<UIElement>
        {
            AboutParagraph("YDKE uygulamasının kaynak kodu ve arayüzü PolyForm Noncommercial License 1.0.0 ile lisanslanmıştır. Bu lisans; telif hakkı, lisans ve atıf bildirimlerinin korunması koşuluyla yazılımın yalnızca ticari olmayan amaçlarla kullanılmasına, değiştirilmesine ve yeniden dağıtılmasına izin verir. Kişisel kullanım, öğrenme, araştırma ve hobi projeleri ile okullar, üniversiteler, kamu kurumları ve kâr amacı gütmeyen kuruluşların kullanımı bu izne dâhildir. Yazılım hiçbir garanti verilmeksizin “OLDUĞU GİBİ” sunulur. Lisans, marka ve ürün adlarını kullanma hakkı vermez."),
            AboutHeading("Ticari kullanım · Commercial use"),
            AboutParagraph("Bu lisans ticari kullanıma izin vermez. Yazılımın satılması, ücretli veya reklam destekli bir hizmet olarak sunulması, ticari bir ürüne dâhil edilmesi ya da kâr amacı güden bir işletmede kullanılması için telif hakkı sahiplerinden önceden yazılı izin alınması gerekir. Aşağıdaki adreslerden herhangi birinden alınan yazılı izin yeterlidir ve tüm telif hakkı sahiplerini bağlar."),
            AboutHeading("Telif hakkı sahipleri · Copyright © 2026"),
            AboutOwner("Bulent Ozkir", ["bulentozkir@gmail.com", "bulentozkir@hotmail.com"]),
            AboutOwner("Ahmet Arda Ozkir", ["ahmetardaozkir@gmail.com"]),
            AboutOwner("Halit Eren Ozkir", ["haliterenozkir@gmail.com"]),
            AboutParagraph("Lisansın tam metnine ve atıf bildirimlerine aşağıdaki bağlantılardan ulaşabilirsiniz:"),
            AboutExternalLink("PolyForm Noncommercial 1.0.0 · polyformproject.org · tam lisans metni ↗", "https://polyformproject.org/licenses/noncommercial/1.0.0", "about.LicenseLink.Top"),
            AboutParagraph("YDKE app source code and interface are licensed under the PolyForm Noncommercial License 1.0.0. Copyright © 2026 Bulent Ozkir, Ahmet Arda Ozkir and Halit Eren Ozkir. You may use, modify and redistribute the software for noncommercial purposes only, provided you keep the copyright, licence and attribution notices. Personal study, research, hobby projects and use by schools, universities, public institutions and nonprofit organisations are permitted purposes. Any commercial use requires prior written permission from the copyright holders — written permission from any one of the listed addresses is sufficient and binding on all of them. The software is distributed on an “AS IS” basis, without warranties or conditions of any kind. The licence grants no trademark rights. The third-party word-list content listed above keeps its own licence and is not covered by this grant.")
        };
        return AboutSection("Uygulama lisansı · Application licence", content.ToArray());
    }

    private static TextBlock AboutParagraph(string text, bool emphasis = false) =>
        StudyText(text, 18, new SolidColorBrush(AppearancePalette.Current.BoxForeground), emphasis, selectable: true);

    private static TextBlock AboutBullet(string text) => AboutParagraph("• " + text);

    private static TextBlock AboutHeading(string text)
    {
        var heading = StudyText(text, 20, new SolidColorBrush(AppearancePalette.Current.BoxForeground), emphasis: true);
        AutomationProperties.SetName(heading, text);
        AutomationProperties.SetHeadingLevel(heading, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level3);
        return heading;
    }

    private Border AboutSection(string title, params UIElement[] children)
    {
        var heading = StudyText(title, 24, new SolidColorBrush(AppearancePalette.Current.BoxForeground), emphasis: true);
        AutomationProperties.SetName(heading, title);
        AutomationProperties.SetHeadingLevel(heading, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
        var panel = new StackPanel { Spacing = 12, MinWidth = 0 };
        panel.Children.Add(heading);
        foreach (var child in children) panel.Children.Add(child);
        var surface = StudySurface(panel, 18, AppearancePalette.Current.BoxBrush, AppearancePalette.Current.BorderBrush);
        surface.HighContrastAdjustment = ElementHighContrastAdjustment.Auto;
        AutomationProperties.SetName(surface, title);
        return surface;
    }

    private StackPanel AboutOwner(string name, IReadOnlyList<string> addresses)
    {
        var owner = new StackPanel { Spacing = 4, MinWidth = 0 };
        owner.Children.Add(AboutParagraph("• " + name, emphasis: true));
        foreach (var address in addresses)
            owner.Children.Add(AboutExternalLink(address, "mailto:" + address, "about.Contact." + address.Replace("@", ".at.", StringComparison.Ordinal)));
        return owner;
    }

    private Button AboutExternalLink(string label, string address, string automationId)
    {
        var link = CompactStudyAction(SecondaryButton(label, ""));
        ApplyHelpTopicButtonVisuals(link);
        AutomationProperties.SetAutomationId(link, automationId);
        AutomationProperties.SetName(link, label);
        AutomationProperties.SetHelpText(link, "Open link: " + label);
        ToolTipService.SetToolTip(link, label);
        link.HorizontalAlignment = HorizontalAlignment.Left;
        link.Click += (_, _) => OpenAboutLink(address);
        return link;
    }

    private void OpenAboutLink(string address)
    {
        try { Process.Start(new ProcessStartInfo(address) { UseShellExecute = true }); }
        catch (Exception ex) { ShowNotice(T("Common.Error"), ex.Message, InfoBarSeverity.Error); }
    }
}