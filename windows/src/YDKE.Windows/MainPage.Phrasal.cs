using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace YDKE_Windows;

public sealed partial class MainPage
{
    private readonly List<Button> _phrasalOptionButtons = [];
    private IReadOnlyList<VocabularyEntry> _phrasalWords = [];
    private string _phrasalLanguage = "";
    private int _phrasalRound;
    private int _phrasalScore;
    private int _phrasalStreak;
    private int _phrasalTotalAnswers;
    private int _phrasalCorrectAnswers;
    private int? _phrasalSelectedOption;
    private PhrasalChallenge? _phrasalChallenge;
    private string _phrasalFeedbackMessage = "";
    private Button? _phrasalNextButton;
    private Button? _phrasalHintButton;
    private bool _phrasalSentenceExpanded;

    private sealed record PhrasalChallenge(VocabularyEntry Entry, IReadOnlyList<VocabularyEntry> Options, int CorrectOption);

    private async Task EnsurePhrasalWordsAsync()
    {
        var language = EffectivePhrasalLanguage();
        if (language == _phrasalLanguage && _phrasalWords.Count > 0) return;

        var words = await _repository.LoadPhrasalAsync(language);
        _phrasalLanguage = language;
        _phrasalWords = words
            .Where(word => !string.IsNullOrWhiteSpace(word.Word))
            .DistinctBy(word => word.Key)
            .ToArray();

        ResetPhrasalSession();
    }

    private string EffectivePhrasalLanguage() =>
        VocabularyRepository.PhrasalFiles.ContainsKey(_settings.StudyLanguage) ? _settings.StudyLanguage : "en";

    private string PhrasalLanguageName(string languageCode) =>
        VocabularyRepository.Languages.FirstOrDefault(language => language.Code == languageCode)?.NativeName
        ?? languageCode.ToUpperInvariant();

    private void ResetPhrasalSession()
    {
        _phrasalRound = 0;
        _phrasalScore = 0;
        _phrasalStreak = 0;
        _phrasalTotalAnswers = 0;
        _phrasalCorrectAnswers = 0;
        _phrasalSelectedOption = null;
        _phrasalChallenge = null;
        _phrasalSentenceExpanded = false;
        _phrasalFeedbackMessage = U("Phrasal.StartHint", "Choose one answer. Use 1-4 keys, H for hint, and Enter for the next challenge.", "Bir cevap secin. 1-4 tuslarini, ipucu icin H tusunu ve sonraki meydan okuma icin Enter tusunu kullanin.");
        BuildNextPhrasalChallenge();
    }

    private void BuildNextPhrasalChallenge()
    {
        var options = PickDistinctPhrasalOptions(4);
        if (options.Count < 4)
        {
            _phrasalChallenge = null;
            _phrasalSelectedOption = null;
            return;
        }

        var answer = options[_random.Next(options.Count)];
        var shuffled = options.OrderBy(_ => _random.Next()).ToArray();
        var correct = Array.FindIndex(shuffled, option => option.Key == answer.Key);
        _phrasalChallenge = new PhrasalChallenge(answer, shuffled, Math.Max(0, correct));
        _phrasalSelectedOption = null;
        _phrasalSentenceExpanded = false;
        _phrasalRound++;
        _phrasalFeedbackMessage = U("Phrasal.StartHint", "Choose one answer. Use 1-4 keys, H for hint, and Enter for the next challenge.", "Bir cevap secin. 1-4 tuslarini, ipucu icin H tusunu ve sonraki meydan okuma icin Enter tusunu kullanin.");
    }

    private List<VocabularyEntry> PickDistinctPhrasalOptions(int targetCount)
    {
        var picks = new List<VocabularyEntry>(targetCount);
        var seenWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var maxAttempts = Math.Max(16, _phrasalWords.Count * 6);

        for (var attempt = 0; attempt < maxAttempts && picks.Count < targetCount; attempt++)
        {
            var candidate = _phrasalWords[_random.Next(_phrasalWords.Count)];
            var key = candidate.Word.Trim();
            if (key.Length == 0 || !seenWords.Add(key)) continue;
            picks.Add(candidate);
        }

        return picks;
    }

    private void AnswerPhrasalOption(int selectedOption)
    {
        if (_phrasalChallenge is null || _phrasalSelectedOption is not null) return;
        if (selectedOption < 0 || selectedOption >= _phrasalChallenge.Options.Count) return;

        _phrasalSelectedOption = selectedOption;
        _phrasalTotalAnswers++;

        var correct = selectedOption == _phrasalChallenge.CorrectOption;
        if (correct)
        {
            _phrasalCorrectAnswers++;
            _phrasalStreak++;
            _phrasalScore += 10 + Math.Min(10, _phrasalStreak);
            _phrasalFeedbackMessage = string.Format(CultureInfo.CurrentCulture,
                U("Phrasal.CorrectFeedback", "Correct. \"{0}\" fits this meaning.", "Dogru. \"{0}\" bu anlama uyuyor."),
                _phrasalChallenge.Entry.Word);
        }
        else
        {
            _phrasalStreak = 0;
            _phrasalScore = Math.Max(0, _phrasalScore - 3);
            _phrasalFeedbackMessage = string.Format(CultureInfo.CurrentCulture,
                U("Phrasal.WrongFeedback", "Good try. You chose \"{0}\", but the correct answer is \"{1}\".", "Guzel deneme. \"{0}\" secildi, ancak dogru cevap \"{1}\"."),
                _phrasalChallenge.Options[selectedOption].Word,
                _phrasalChallenge.Entry.Word);
        }

        if (IsPhrasalPage) RenderCurrentPage();
    }

    private void ShowPhrasalHint()
    {
        if (_phrasalChallenge is null || _phrasalSelectedOption is not null) return;

        _phrasalFeedbackMessage = string.Format(CultureInfo.CurrentCulture,
            U("Phrasal.HintMessage", "Hint: {0}", "Ipucu: {0}"),
            BuildPhrasalHint(_phrasalChallenge.Entry));

        if (IsPhrasalPage) RenderCurrentPage();
    }

    private string BuildPhrasalHint(VocabularyEntry entry)
    {
        var tokens = entry.Word.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var firstToken = tokens.Length == 0 ? entry.Word : tokens[0];
        var opening = firstToken.Length <= 2 ? firstToken : firstToken[..2] + "...";
        var letterCount = entry.Word.Count(char.IsLetter);
        return string.Format(CultureInfo.CurrentCulture,
            U("Phrasal.HintPattern", "starts with \"{0}\" and has {1} letters.", "\"{0}\" ile baslar ve {1} harf icerir."),
            opening,
            letterCount);
    }

    private void AdvancePhrasalChallenge()
    {
        if (_phrasalWords.Count < 4) return;
        BuildNextPhrasalChallenge();
        if (IsPhrasalPage) RenderCurrentPage();
    }

    private void RenderPhrasalVerbs()
    {
        _phrasalOptionButtons.Clear();
        _phrasalNextButton = null;
        _phrasalHintButton = null;

        var activeLanguage = EffectivePhrasalLanguage();
        var languageName = PhrasalLanguageName(activeLanguage);
        AddPageHeader(T("Nav.Phrasal"),
            string.Format(CultureInfo.CurrentCulture,
                U("Phrasal.Subtitle", "{0} dataset · {1:N0} phrasal verbs. Definition and example are always shown in the original language and Turkish.", "{0} veri seti · {1:N0} ogek fiil. Tanim ve ornek her zaman orijinal dilde ve Turkce gorunur."),
                languageName,
                _phrasalWords.Count));

        if (_settings.StudyLanguage != activeLanguage)
        {
            var fallback = StudyText(
                string.Format(CultureInfo.CurrentCulture,
                    U("Phrasal.UnsupportedLanguage", "Your current study language does not have a phrasal-verbs dataset yet. Showing {0}.", "Mevcut ogrenme dilinde henuz ogek fiil veri seti yok. {0} gosteriliyor."),
                    languageName),
                18,
                emphasis: true,
                selectable: true);
            PageContent.Children.Add(StudySurface(fallback, 14));
        }

        if (_phrasalChallenge is null && _phrasalWords.Count >= 4) BuildNextPhrasalChallenge();
        if (_phrasalWords.Count < 4 || _phrasalChallenge is null)
        {
            var empty = StudyText(U("Phrasal.Empty", "Not enough phrasal-verb entries. Add more records and try again.", "Yeterli ogek fiil kaydi yok. Daha fazla kayit ekleyip tekrar deneyin."),
                20,
                emphasis: true,
                selectable: true);
            PageContent.Children.Add(StudySurface(empty, 18));
            return;
        }

        var accuracy = _phrasalTotalAnswers == 0
            ? "-"
            : $"{100.0 * _phrasalCorrectAnswers / _phrasalTotalAnswers:0}%";

        var stats = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        AddStat(stats, 0, T("Game.Round"), _phrasalRound.ToString("N0", CultureInfo.CurrentCulture), "");
        AddStat(stats, 1, T("Game.Score"), _phrasalScore.ToString("N0", CultureInfo.CurrentCulture), "");
        AddStat(stats, 2, T("Game.Streak"), _phrasalStreak.ToString("N0", CultureInfo.CurrentCulture), "");
        AddStat(stats, 3, T("Stats.Success"), accuracy, "");
        ConfigureResponsiveGrid(stats, 4, 170);
        PageContent.Children.Add(stats);

        var definition = SplitPhrasalBilingual(_phrasalChallenge.Entry.Definition);
        var example = SplitPhrasalBilingual(_phrasalChallenge.Entry.Example);

        var promptPanel = new StackPanel { Spacing = 12 };
        promptPanel.Children.Add(StudyLabel(U("Phrasal.Prompt", "Which phrasal verb matches this meaning?", "Bu anlama hangi ogek fiil uyuyor?")));
        promptPanel.Children.Add(StudyText(U("Phrasal.Shortcuts", "Keyboard: 1-4 choose · H hint · Enter next", "Klavye: 1-4 sec · H ipucu · Enter sonraki"), 18));
        promptPanel.Children.Add(PhrasalDetails(T("Cards.Meaning"), definition, "phrasal.Definition"));
        promptPanel.Children.Add(PhrasalSentenceDetails(T("Cards.Example") + "?", example, "phrasal.Example"));

        if (_phrasalSelectedOption is not null)
        {
            var answerLabel = StudyText(
                string.Format(CultureInfo.CurrentCulture,
                    U("Phrasal.Answer", "Answer: {0}", "Cevap: {0}"),
                    _phrasalChallenge.Entry.Word),
                22,
                emphasis: true,
                selectable: true);
            AutomationProperties.SetAutomationId(answerLabel, "phrasal.Answer");
            AutomationProperties.SetName(answerLabel, answerLabel.Text);
            promptPanel.Children.Add(answerLabel);
        }

        var answered = _phrasalSelectedOption is not null;
        var optionsPanel = new StackPanel { Spacing = 12 };
        optionsPanel.Children.Add(StudyLabel(U("Phrasal.Choose", "Choose the phrasal verb", "Ogek fiili sec")));

        var optionsGrid = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        for (var index = 0; index < _phrasalChallenge.Options.Count; index++)
        {
            var visual = PhrasalOptionVisual(index, answered);
            var status = PhrasalOptionStatus(index);
            var foreground = new SolidColorBrush(visual.Foreground);
            var optionLabel = StudyText(_phrasalChallenge.Options[index].Word, 24, foreground, emphasis: true);
            var content = StudyOptionContent(
                index + 1,
                optionLabel,
                status,
                new SolidColorBrush(visual.Background),
                foreground);
            var button = StudyOptionButton(content, $"{index + 1}: {_phrasalChallenge.Options[index].Word}", visual);
            button.IsEnabled = !answered;
            var captured = index;
            button.Click += (_, _) => AnswerPhrasalOption(captured);
            AutomationProperties.SetAutomationId(button, $"phrasal.Option.{index}");
            AutomationProperties.SetHelpText(button, U("Phrasal.Choose", "Choose the phrasal verb", "Ogek fiili sec"));
            ToolTipService.SetToolTip(button, _phrasalChallenge.Options[index].Word);
            if (!answered && index == 0) FocusTarget(button, "phrasal.Option.0");
            _phrasalOptionButtons.Add(button);
            optionsGrid.Children.Add(button);
        }
        ConfigureStudyChoices(optionsGrid, 2, 220);
        optionsPanel.Children.Add(optionsGrid);

        var actions = new Grid { ColumnSpacing = 10, RowSpacing = 10 };

        _phrasalHintButton = CompactStudyAction(SecondaryButton(T("Game.Hint"), ""));
        _phrasalHintButton.IsEnabled = !answered;
        _phrasalHintButton.Click += (_, _) => ShowPhrasalHint();
        AutomationProperties.SetAutomationId(_phrasalHintButton, "phrasal.Hint");

        _phrasalNextButton = ReadableStudyAction(AccentButton(U("Phrasal.Next", "Next challenge", "Sonraki meydan okuma"), ""));
        _phrasalNextButton.IsEnabled = answered;
        _phrasalNextButton.Click += (_, _) => AdvancePhrasalChallenge();
        AutomationProperties.SetAutomationId(_phrasalNextButton, "phrasal.Next");
        if (answered) FocusTarget(_phrasalNextButton, "phrasal.Next");

        var reset = CompactStudyAction(SecondaryButton(U("Phrasal.Reset", "Restart score", "Skoru sifirla"), ""));
        reset.Click += (_, _) =>
        {
            ResetPhrasalSession();
            RenderCurrentPage();
        };
        AutomationProperties.SetAutomationId(reset, "phrasal.Reset");

        actions.Children.Add(_phrasalHintButton);
        actions.Children.Add(_phrasalNextButton);
        actions.Children.Add(reset);
        ConfigureResponsiveGrid(actions, 3, 170);
        optionsPanel.Children.Add(actions);

        var feedback = StudyText(_phrasalFeedbackMessage, 20, emphasis: true, selectable: true);
        StudyLive(feedback, "phrasal.Feedback");
        optionsPanel.Children.Add(StudySurface(feedback, 14));

        var workspace = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        workspace.Children.Add(StudySurface(promptPanel, 18));
        workspace.Children.Add(StudySurface(optionsPanel, 18));
        ConfigureStudyChoices(workspace, 2, 360);
        PageContent.Children.Add(workspace);
    }

    private UIElement PhrasalDetails(string heading, (string Original, string Turkish) text, string automationPrefix)
    {
        var detail = new StackPanel { Spacing = 10 };
        detail.Children.Add(StudyLabel(heading));
        detail.Children.Add(PhrasalDetailRows(text, automationPrefix));
        return detail;
    }

    private UIElement PhrasalSentenceDetails(string heading, (string Original, string Turkish) text, string automationPrefix)
    {
        var detail = new StackPanel { Spacing = 10 };
        var reveal = PhrasalSentenceRevealButton(heading, _phrasalSentenceExpanded);
        reveal.Click += (_, _) =>
        {
            _phrasalSentenceExpanded = !_phrasalSentenceExpanded;
            if (IsPhrasalPage) RenderCurrentPage();
        };
        AutomationProperties.SetAutomationId(reveal, "phrasal.Example.Toggle");
        AutomationProperties.SetName(reveal, heading);
        ToolTipService.SetToolTip(reveal, heading);
        detail.Children.Add(reveal);

        if (_phrasalSentenceExpanded)
            detail.Children.Add(PhrasalDetailRows(text, automationPrefix));

        return detail;
    }

    private static Button PhrasalSentenceRevealButton(string text, bool expanded)
    {
        var palette = AppearancePalette.Current;
        var preferredBackground = expanded ? palette.ButtonTint : palette.Button;
        var background = AppearancePalette.EnsureFillContrast(
            palette.Box,
            preferredBackground,
            expanded ? 3.5 : 4.5);
        var preferredForeground = expanded ? palette.ButtonTintForeground : palette.ButtonForeground;
        var foreground = EnsureStrongTextContrast(background, preferredForeground, 4.5);
        var border = AppearancePalette.EnsureBoundaryContrast(background, palette.ButtonBorder);

        var label = new TextBlock
        {
            Text = text,
            FontSize = ReadingSize(18),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
            LineHeight = ReadingSize(18) * 1.35,
            LineStackingStrategy = LineStackingStrategy.MaxHeight,
        };

        var button = new Button
        {
            Content = label,
            MinHeight = 48,
            MinWidth = 48,
            Padding = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            IsTabStop = true,
            UseSystemFocusVisuals = true,
            HighContrastAdjustment = ElementHighContrastAdjustment.Auto,
        };

        ApplyAccessibleButtonVisuals(button, background, foreground, border);
        button.BorderThickness = new Thickness(2);
        return button;
    }

    private UIElement PhrasalDetailRows((string Original, string Turkish) text, string automationPrefix)
    {
        var palette = AppearancePalette.Current;
        var rowBackground = AppearancePalette.EnsureFillContrast(palette.Box, palette.Background, 5.0);
        var rowForeground = EnsureStrongTextContrast(rowBackground, palette.BoxForeground);
        var rowBorder = AppearancePalette.EnsureBoundaryContrast(rowBackground, palette.Border);
        var tagBackground = AppearancePalette.EnsureFillContrast(rowBackground, palette.ButtonTint, 3.5);
        var tagForeground = EnsureStrongTextContrast(tagBackground, palette.ButtonForeground, 4.5);
        var tagBorder = AppearancePalette.EnsureBoundaryContrast(tagBackground, palette.ButtonBorder);

        var rowBackgroundBrush = new SolidColorBrush(rowBackground);
        var rowForegroundBrush = new SolidColorBrush(rowForeground);
        var rowBorderBrush = new SolidColorBrush(rowBorder);
        var tagBackgroundBrush = new SolidColorBrush(tagBackground);
        var tagForegroundBrush = new SolidColorBrush(tagForeground);
        var tagBorderBrush = new SolidColorBrush(tagBorder);

        UIElement DetailRow(string label, string value, double textSize, string automationId)
        {
            var tag = StudyText(label, 17, tagForegroundBrush, emphasis: true);
            tag.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
            var tagBadge = new Border
            {
                Child = tag,
                Padding = new Thickness(10, 4, 10, 4),
                CornerRadius = new CornerRadius(8),
                Background = tagBackgroundBrush,
                BorderBrush = tagBorderBrush,
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                HighContrastAdjustment = ElementHighContrastAdjustment.Auto,
            };

            var body = StudyText(value, textSize, rowForegroundBrush, selectable: true);
            AutomationProperties.SetAutomationId(body, automationId);
            AutomationProperties.SetName(body, body.Text);

            // Badge above text (not beside it) so text keeps the full column width when columns are narrow.
            var column = new StackPanel { Spacing = 6 };
            column.Children.Add(tagBadge);
            column.Children.Add(body);

            var surface = StudySurface(column, 10, rowBackgroundBrush, rowBorderBrush);
            surface.BorderThickness = new Thickness(2);
            surface.CornerRadius = new CornerRadius(12);
            surface.HighContrastAdjustment = ElementHighContrastAdjustment.Auto;
            return surface;
        }

        var detailRows = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        detailRows.Children.Add(DetailRow(U("Phrasal.Original", "Original", "Orijinal"), text.Original, 20, automationPrefix + ".Original"));
        detailRows.Children.Add(DetailRow(U("Phrasal.Turkish", "Turkish", "Turkce"), text.Turkish, 18, automationPrefix + ".Turkish"));
        // Side-by-side (not stacked) so the fixed no-scroll viewport still fits the answer/options below.
        ConfigureResponsiveGrid(detailRows, 2, Font(180));
        return detailRows;
    }

    private StudyChoiceVisual PhrasalOptionVisual(int optionIndex, bool answered)
    {
        if (_phrasalChallenge is null || !answered) return QuizChoiceVisual(optionIndex, _settings.QuizChoicePalette);
        if (optionIndex == _phrasalChallenge.CorrectOption) return RatingVisual(RecallRating.Good);
        if (_phrasalSelectedOption == optionIndex) return RatingVisual(RecallRating.Again);
        return UniversalDisabledChoiceVisual(QuizChoiceVisual(optionIndex, _settings.QuizChoicePalette));
    }

    private string PhrasalOptionStatus(int optionIndex)
    {
        if (_phrasalChallenge is null || _phrasalSelectedOption is null) return "";
        if (optionIndex == _phrasalChallenge.CorrectOption) return T("Common.Correct");
        return _phrasalSelectedOption == optionIndex ? T("Common.Wrong") : "";
    }

    private (string Original, string Turkish) SplitPhrasalBilingual(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            var fallback = U("Phrasal.NoTurkish", "No text available.", "Metin bulunamadi.");
            return (fallback, fallback);
        }

        var separator = text.LastIndexOf(" - ", StringComparison.Ordinal);
        if (separator < 0)
        {
            var single = text.Trim();
            return (single, single);
        }

        var original = text[..separator].Trim();
        var turkish = text[(separator + 3)..].Trim();
        if (original.Length == 0) original = text.Trim();
        if (turkish.Length == 0) turkish = U("Phrasal.NoTurkish", "No Turkish translation available.", "Turkce ceviri bulunamadi.");
        return (original, turkish);
    }
}
