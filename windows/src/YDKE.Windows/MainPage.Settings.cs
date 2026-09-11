using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Globalization.NumberFormatting;
using Windows.UI;

namespace YDKE_Windows;

public sealed partial class MainPage
{
    private SettingsSection _settingsSection = SettingsSection.Learning;
    private SettingsDraftState? _settingsDraftState;

    private enum SettingsSection { Learning, Appearance, Account }

    private sealed record LearningSettingsDraft(string GoalText, bool ReduceMotion, bool UntimedPractice)
    {
        public static LearningSettingsDraft From(UserSettings settings) => new(
            settings.DailyGoal.ToString(CultureInfo.CurrentCulture), settings.ReduceMotion, settings.UntimedPractice);
    }

    private sealed record AppearanceSettingsDraft(double FontScale, string Background, string Button, string Box)
    {
        public static AppearanceSettingsDraft From(UserSettings settings) => new(
            settings.FontScale,
            AppearancePalette.ToHex(AppearancePalette.Parse(settings.AppBackgroundColor, AppearancePalette.DefaultBackground)),
            AppearancePalette.ToHex(AppearancePalette.Parse(settings.ButtonColor, AppearancePalette.DefaultButton)),
            AppearancePalette.ToHex(AppearancePalette.Parse(settings.BoxColor, AppearancePalette.DefaultBox)));

        public static AppearanceSettingsDraft Defaults => new(1,
            AppearancePalette.DefaultBackground, AppearancePalette.DefaultButton, AppearancePalette.DefaultBox);
    }

    // These drafts are view state only; no new preferences are serialized.
    private sealed class SettingsDraftState(UserSettings settings, ProgressState progress)
    {
        public UserSettings SettingsSource = settings;
        public ProgressState ProgressSource = progress;
        public LearningSettingsDraft SavedLearning = LearningSettingsDraft.From(settings);
        public LearningSettingsDraft Learning = LearningSettingsDraft.From(settings);
        public AppearanceSettingsDraft SavedAppearance = AppearanceSettingsDraft.From(settings);
        public AppearanceSettingsDraft Appearance = AppearanceSettingsDraft.From(settings);
        public SettingsSection? Applying;
        public bool LearningNeedsReview;
        public bool AppearanceNeedsReview;
        public bool GoalValidationShown;
    }

    private SettingsDraftState GetSettingsDraft()
    {
        if (_settingsDraftState is null) return _settingsDraftState = new(_settings, _progress);
        var draft = _settingsDraftState;
        var learning = LearningSettingsDraft.From(_settings);
        var appearance = AppearanceSettingsDraft.From(_settings);
        var replacedPair = !ReferenceEquals(draft.SettingsSource, _settings) &&
            !ReferenceEquals(draft.ProgressSource, _progress);

        if (draft.Applying == SettingsSection.Learning)
        {
            draft.Learning = learning;
            draft.LearningNeedsReview = false;
            draft.GoalValidationShown = false;
        }
        else if (replacedPair || learning != draft.SavedLearning)
        {
            // Rebase untouched fields; keep edits for explicit review after a backup/settings replacement.
            draft.Learning = new(
                draft.Learning.GoalText == draft.SavedLearning.GoalText ? learning.GoalText : draft.Learning.GoalText,
                draft.Learning.ReduceMotion == draft.SavedLearning.ReduceMotion ? learning.ReduceMotion : draft.Learning.ReduceMotion,
                draft.Learning.UntimedPractice == draft.SavedLearning.UntimedPractice ? learning.UntimedPractice : draft.Learning.UntimedPractice);
            draft.LearningNeedsReview = draft.Learning != learning;
        }

        if (draft.Applying == SettingsSection.Appearance)
        {
            draft.Appearance = appearance;
            draft.AppearanceNeedsReview = false;
        }
        else if (replacedPair || appearance != draft.SavedAppearance)
        {
            draft.Appearance = new(
                draft.Appearance.FontScale == draft.SavedAppearance.FontScale ? appearance.FontScale : draft.Appearance.FontScale,
                draft.Appearance.Background == draft.SavedAppearance.Background ? appearance.Background : draft.Appearance.Background,
                draft.Appearance.Button == draft.SavedAppearance.Button ? appearance.Button : draft.Appearance.Button,
                draft.Appearance.Box == draft.SavedAppearance.Box ? appearance.Box : draft.Appearance.Box);
            draft.AppearanceNeedsReview = draft.Appearance != appearance;
        }

        draft.SavedLearning = learning;
        draft.SavedAppearance = appearance;
        draft.SettingsSource = _settings;
        draft.ProgressSource = _progress;
        return draft;
    }

    private void RenderSettingsPage(string markedKnownLabel)
    {
        var draft = GetSettingsDraft();
        AddPageHeader(T("Profile.Title"), U("Kids.Settings.Intro",
            "Make practice feel right for you.", "Çalışmanı sana uygun hale getir."));

        var navigation = new Grid { ColumnSpacing = 8, RowSpacing = 6, MinWidth = 0 };
        var content = new Grid { MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var sections = new (SettingsSection Section, string Id, string Label, FrameworkElement Panel)[]
        {
            (SettingsSection.Learning, "learning", U("Kids.Settings.MyPractice", "My practice", "Çalışmam"), BuildLearningSettings(draft)),
            (SettingsSection.Appearance, "appearance", U("Kids.Settings.ColorsText", "Colors & text", "Renkler ve yazılar"), BuildAppearanceSettings(draft)),
            (SettingsSection.Account, "account", U("Kids.Settings.GrownUps", "For grown-ups", "Büyükler için"), BuildAccountSettings(markedKnownLabel)),
        };

        void SelectSettingsSection(SettingsSection selected)
        {
            _settingsSection = selected;
            if (!_storageBlocked && Notice.Severity != InfoBarSeverity.Error) Notice.IsOpen = false;
            foreach (var section in sections)
                section.Panel.Visibility = section.Section == selected ? Visibility.Visible : Visibility.Collapsed;
        }

        // Construct each section once; radio selection only changes visibility, never saves or rebuilds.
        var groupName = "settings.Sections." + Guid.NewGuid().ToString("N");
        foreach (var section in sections)
        {
            var tab = new RadioButton
            {
                Content = SettingsText(section.Label, 18), GroupName = groupName,
                IsChecked = section.Section == _settingsSection, MinWidth = 48, MinHeight = 48,
                FontSize = ReadingSize(18), Padding = new Thickness(8, 10, 8, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Foreground = AppearancePalette.Current.BackgroundForegroundBrush,
            };
            FocusTarget(tab, "settings.Section." + section.Id);
            AutomationProperties.SetName(tab, section.Label);
            tab.Checked += (_, _) => SelectSettingsSection(section.Section);
            navigation.Children.Add(tab);
            AutomationProperties.SetAutomationId(section.Panel, "settings.Content." + section.Id);
            content.Children.Add(section.Panel);
        }
        ConfigureResponsiveGrid(navigation, 3, Font(185));
        SelectSettingsSection(_settingsSection);
        PageContent.Children.Add(navigation);
        PageContent.Children.Add(content);
    }

    private FrameworkElement BuildLearningSettings(SettingsDraftState draft)
    {
        var panel = new StackPanel { Spacing = 12, MinWidth = 0 };
        panel.Children.Add(SettingsText(string.Format(CultureInfo.CurrentCulture,
            U("Kids.Settings.LearningSummary", "Saved goal: {0:N0} words a day", "Kayıtlı hedef: Günde {0:N0} kelime"),
            _settings.DailyGoal), 18));

        var goalLabel = U("Kids.Settings.DailyGoal", "(Daily goal) Words to practise each day", "(Günlük hedef) Her gün çalışılacak kelime sayısı");
        var goalHint = U("Kids.Settings.GoalHint", "Each different word counts once a day.",
            "Her farklı kelime günde bir kez sayılır.");
        var goal = new NumberBox
        {
            Header = SettingsText(goalLabel, 18),
            Minimum = 1, Maximum = 10000, SmallChange = 1, LargeChange = 10,
            Value = _settings.DailyGoal, AcceptsExpression = false,
            ValidationMode = NumberBoxValidationMode.Disabled,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            MinWidth = 48, MinHeight = 48, FontSize = ReadingSize(18), HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        FocusTarget(goal, "settings.DailyGoal");
        AutomationProperties.SetName(goal, goalLabel);
        AutomationProperties.SetHelpText(goal, goalHint);
        var error = SettingsText("", 18);
        error.Visibility = Visibility.Collapsed;
        AutomationProperties.SetAutomationId(error, "settings.DailyGoal.Error");
        Live(error);
        var motion = SettingsCheckBox(U("Kids.Settings.ReduceMotion", "Less movement", "Daha az hareket"), U("Kids.Settings.MotionHint",
            "Use fewer animations.", "Daha az animasyon kullan."),
            draft.Learning.ReduceMotion, "settings.ReduceMotion");
        var untimed = SettingsCheckBox(U("Kids.Settings.UntimedPractice", "Play without a timer", "Süre sınırı olmadan oyna"), U("Kids.Settings.UntimedHint",
            "Take your time in timed games.", "Süreli oyunlarda acele etmeden oyna."),
            draft.Learning.UntimedPractice, "settings.UntimedPractice");
        var save = SettingsButton(AccentButton(U("Kids.Settings.Save", "Save", "Kaydet"), "\uE73E"), "settings.SaveLearning");
        TextBox? goalEditor = null;
        var restoring = true;

        string GoalText() => goalEditor?.Text ?? goal.Text ?? "";

        bool ValidateGoal(bool announce)
        {
            var valid = TrySettingsGoal(goal, GoalText(), out _);
            var message = !valid && draft.GoalValidationShown ? T("Goal.Invalid") : "";
            error.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (announce && message != error.Text) Announce(error, message);
            else error.Text = message;
            AutomationProperties.SetHelpText(goal, message.Length == 0 ? goalHint : message);
            return valid;
        }

        void CaptureGoal()
        {
            if (restoring) return;
            draft.Learning = draft.Learning with { GoalText = GoalText() };
            ValidateGoal(true);
        }

        void RestoreLearningControls()
        {
            restoring = true;
            goal.Text = draft.Learning.GoalText;
            if (goalEditor is not null) goalEditor.Text = draft.Learning.GoalText;
            motion.IsChecked = draft.Learning.ReduceMotion;
            untimed.IsChecked = draft.Learning.UntimedPractice;
            restoring = false;
            save.IsEnabled = !draft.LearningNeedsReview;
            ValidateGoal(false);
        }

        // NumberBox.Value can still be the last valid value while its editor contains invalid text.
        goal.RegisterPropertyChangedCallback(NumberBox.TextProperty, (_, _) => CaptureGoal());
        RoutedEventHandler? loaded = null;
        loaded = (_, _) =>
        {
            goal.Loaded -= loaded;
            goal.ApplyTemplate();
            goalEditor = SettingsNumberEditor(goal);
            if (goalEditor is not null)
            {
                goalEditor.MinHeight = 48;
                goalEditor.FontSize = ReadingSize(18);
                goalEditor.TextChanged += (_, _) => CaptureGoal();
            }
            RestoreLearningControls();
        };
        goal.Loaded += loaded;
        goal.LostFocus += (_, _) =>
        {
            if (restoring) return;
            CaptureGoal();
            draft.GoalValidationShown = true;
            ValidateGoal(true);
        };
        void CaptureChecks()
        {
            if (!restoring) draft.Learning = draft.Learning with
            {
                ReduceMotion = motion.IsChecked == true, UntimedPractice = untimed.IsChecked == true,
            };
        }
        motion.Checked += (_, _) => CaptureChecks();
        motion.Unchecked += (_, _) => CaptureChecks();
        untimed.Checked += (_, _) => CaptureChecks();
        untimed.Unchecked += (_, _) => CaptureChecks();
        save.Click += async (_, _) =>
        {
            CaptureGoal();
            draft.GoalValidationShown = true;
            if (!ValidateGoal(true) || !TrySettingsGoal(goal, GoalText(), out var target))
            {
                goal.Focus(FocusState.Programmatic);
                return;
            }
            if (draft.LearningNeedsReview) return;
            var values = draft.Learning;
            await SaveSettingsDraftAsync(draft, SettingsSection.Learning, () =>
            {
                _settings.DailyGoal = target;
                _settings.ReduceMotion = values.ReduceMotion;
                _settings.UntimedPractice = values.UntimedPractice;
            });
        };

        panel.Children.Add(SettingsDraftReview(draft.LearningNeedsReview, "learning", keep =>
        {
            if (!keep) draft.Learning = draft.SavedLearning;
            draft.LearningNeedsReview = false;
            draft.GoalValidationShown = false;
            RestoreLearningControls();
            goal.Focus(FocusState.Programmatic);
        }));
        var rows = new Grid { ColumnSpacing = 20, RowSpacing = 16, MinWidth = 0 };
        var goalPanel = new StackPanel { Spacing = 6, MinWidth = 0 };
        goalPanel.Children.Add(goal);
        goalPanel.Children.Add(SettingsText(goalHint, 18));
        goalPanel.Children.Add(error);
        rows.Children.Add(goalPanel);
        rows.Children.Add(new StackPanel { Spacing = 16, MinWidth = 0, Children = { motion, untimed } });
        ConfigureResponsiveGrid(rows, 2, Font(300));
        panel.Children.Add(rows);
        var actions = new Grid { MinWidth = 0, Children = { save } };
        ConfigureResponsiveGrid(actions, 1, 200);
        panel.Children.Add(actions);
        save.IsEnabled = !draft.LearningNeedsReview;
        return Card(panel, 16);
    }

    private static bool TrySettingsGoal(NumberBox input, string text, out int goal)
    {
        goal = 0;
        var parsed = (input.NumberFormatter as INumberParser)?.ParseDouble(text.Trim());
        if (parsed is not double value || !double.IsFinite(value) || value != Math.Truncate(value) || value is < 1 or > 10000)
            return false;
        goal = (int)value;
        return true;
    }

    private static TextBox? SettingsNumberEditor(DependencyObject element)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            var child = VisualTreeHelper.GetChild(element, index);
            if (child is TextBox editor) return editor;
            if (SettingsNumberEditor(child) is { } nested) return nested;
        }
        return null;
    }

    private FrameworkElement BuildAppearanceSettings(SettingsDraftState draft)
    {
        var panel = new StackPanel { Spacing = 12, MinWidth = 0 };
        var textSizeLabel = U("Kids.Settings.TextSize", "Text size", "Yazı boyutu");
        var textSizeHint = U("Kids.Settings.TextSizeHint", "Slide to make the words bigger or smaller.", "Kelimeleri büyütmek veya küçültmek için kaydır.");
        var fontValue = SettingsText("", 18);
        AutomationProperties.SetAutomationId(fontValue, "settings.FontScale.Value");
        var fontSlider = new Slider
        {
            Minimum = 85, Maximum = 140, StepFrequency = 5, SmallChange = 5, LargeChange = 10,
            Value = Math.Clamp(draft.Appearance.FontScale * 100, 85, 140),
            MinWidth = 48, MinHeight = 48, FontSize = ReadingSize(18), HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        FocusTarget(fontSlider, "settings.FontScale");
        AutomationProperties.SetName(fontSlider, textSizeLabel);
        AutomationProperties.SetHelpText(fontSlider, textSizeHint);
        var appColor = AppearanceColorSetting(T("Profile.AppColor"), T("Profile.ColorHint"),
            AppearancePalette.Parse(draft.Appearance.Background, AppearancePalette.DefaultBackground));
        var buttonColor = AppearanceColorSetting(T("Profile.ButtonColor"), T("Profile.ColorHint"),
            AppearancePalette.Parse(draft.Appearance.Button, AppearancePalette.DefaultButton));
        var boxColor = AppearanceColorSetting(T("Profile.BoxColor"), T("Profile.ColorHint"),
            AppearancePalette.Parse(draft.Appearance.Box, AppearancePalette.DefaultBox));
        FitSettingsColorPicker(appColor, "background");
        FitSettingsColorPicker(buttonColor, "button");
        FitSettingsColorPicker(boxColor, "panel");

        // Use a small, localized example, not the learner's current (possibly advanced) word list.
        var previewTitle = SettingsText(U("Kids.Settings.TryItHere", "Try it here", "Burada dene"), 20);
        var previewHint = SettingsText(U("Kids.Settings.PreviewHint", "Nothing changes until you press Save.", "Kaydet'e basana kadar hiçbir şey değişmez."), 18);
        var previewWord = SettingsText(U("Kids.Settings.SampleWord", "sun", "güneş"), 28);
        previewWord.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        var previewMeaning = SettingsText(U("Kids.Settings.SampleMeaning",
            "The star that lights our day.", "Gündüz bize ışık veren yıldız."), 20);
        previewWord.IsTextSelectionEnabled = true;
        previewMeaning.IsTextSelectionEnabled = true;
        AutomationProperties.SetAutomationId(previewWord, "settings.Preview.Word");
        AutomationProperties.SetAutomationId(previewMeaning, "settings.Preview.Meaning");
        var previewButtonLabel = SettingsText(U("Kids.Settings.SampleButton", "Hello!", "Merhaba!"), 18);
        var previewButton = new Button
        {
            Content = previewButtonLabel, Padding = new Thickness(12, 8, 12, 8),
            MinWidth = 48, MinHeight = 48, FontSize = ReadingSize(18), HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1),
            IsTabStop = false, IsHitTestVisible = false,
        };
        AutomationProperties.SetAutomationId(previewButton, "settings.Preview.Button");
        AutomationProperties.SetName(previewButton, previewButtonLabel.Text);
        var previewBox = new Border
        {
            Padding = new Thickness(12), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), MinWidth = 0,
            Child = new StackPanel { Spacing = 12, MinWidth = 0, Children = { previewWord, previewMeaning, previewButton } },
        };
        var preview = new Border
        {
            Padding = new Thickness(14), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), MinWidth = 0,
            Child = new StackPanel { Spacing = 12, MinWidth = 0, Children = { previewTitle, previewHint, previewBox } },
        };
        AutomationProperties.SetAutomationId(preview, "settings.Preview");
        // Technical color values remain available, but only inside the collapsed Advanced section.
        var previewBackground = SettingsText("", 18);
        var previewBoxColor = SettingsText("", 18);
        var previewButtonColor = SettingsText("", 18);
        var contrast = SettingsText("", 18);
        AutomationProperties.SetAutomationId(contrast, "settings.Preview.Contrast");
        var status = SettingsText("", 18);
        AutomationProperties.SetAutomationId(status, "settings.Appearance.Status");
        var apply = SettingsButton(AccentButton(U("Kids.Settings.Save", "Save", "Kaydet"), "\uE73E"), "settings.ApplyAppearance");
        var reset = SettingsButton(SecondaryButton(U("Kids.Settings.ResetPreview", "Reset preview", "Önizlemeyi sıfırla"), "\uE894"), "settings.ResetAppearanceDraft");
        var restoring = false;

        void RefreshPreview()
        {
            var values = draft.Appearance;
            var background = AppearancePalette.Parse(values.Background, AppearancePalette.DefaultBackground);
            var button = AppearancePalette.Parse(values.Button, AppearancePalette.DefaultButton);
            var box = AppearancePalette.Parse(values.Box, AppearancePalette.DefaultBox);
            var backgroundInk = SettingsPreviewForeground(background);
            var buttonInk = SettingsPreviewForeground(button);
            var boxInk = SettingsPreviewForeground(box);
            preview.Background = new SolidColorBrush(background);
            preview.BorderBrush = new SolidColorBrush(backgroundInk);
            previewTitle.Foreground = previewHint.Foreground = new SolidColorBrush(backgroundInk);
            previewBox.Background = new SolidColorBrush(box);
            previewBox.BorderBrush = new SolidColorBrush(boxInk);
            foreach (var text in new[] { previewWord, previewMeaning })
                text.Foreground = new SolidColorBrush(boxInk);
            previewButton.Background = new SolidColorBrush(button);
            previewButton.Foreground = previewButtonLabel.Foreground = new SolidColorBrush(buttonInk);
            previewButton.BorderBrush = new SolidColorBrush(buttonInk);
            // Scale this isolated preview once with its draft, keeping the same reading floor as the app.
            previewWord.FontSize = Math.Max(18, 28 * values.FontScale);
            previewMeaning.FontSize = Math.Max(18, 20 * values.FontScale);
            previewButtonLabel.FontSize = Math.Max(18, 18 * values.FontScale);
            previewTitle.FontSize = Math.Max(18, 20 * values.FontScale);
            previewHint.FontSize = Math.Max(18, 18 * values.FontScale);
            foreach (var text in new[] { previewWord, previewMeaning, previewButtonLabel, previewTitle, previewHint })
                text.LineHeight = text.FontSize * 1.4;
            previewBackground.Text = $"{T("Profile.AppColor")}: {values.Background}";
            previewBoxColor.Text = $"{T("Profile.BoxColor")}: {values.Box}";
            previewButtonColor.Text = $"{T("Profile.ButtonColor")}: {values.Button}";
            fontValue.Text = $"{textSizeLabel}: {values.FontScale * 100:0.##}%";
            var minimumContrast = Math.Min(AppearancePalette.ContrastRatio(background, backgroundInk),
                Math.Min(AppearancePalette.ContrastRatio(box, boxInk), AppearancePalette.ContrastRatio(button, buttonInk)));
            contrast.Text = string.Format(CultureInfo.CurrentCulture, U("Settings.PreviewContrast",
                "Preview text contrast: {0:0.0}:1 or better", "Önizleme metin kontrastı: en az {0:0.0}:1"), minimumContrast);
            var dirty = values != draft.SavedAppearance;
            status.Text = dirty ? U("Kids.Settings.PreviewPending", "Looks good? Save your colors.", "Güzel görünüyor mu? Renklerini kaydet.")
                : U("Kids.Settings.PreviewSaved", "These are your saved colors.", "Bunlar kaydettiğin renkler.");
            apply.IsEnabled = dirty && !draft.AppearanceNeedsReview;
        }

        void RestoreAppearanceControls()
        {
            restoring = true;
            fontSlider.Value = draft.Appearance.FontScale * 100;
            appColor.Picker.Color = AppearancePalette.Parse(draft.Appearance.Background, AppearancePalette.DefaultBackground);
            buttonColor.Picker.Color = AppearancePalette.Parse(draft.Appearance.Button, AppearancePalette.DefaultButton);
            boxColor.Picker.Color = AppearancePalette.Parse(draft.Appearance.Box, AppearancePalette.DefaultBox);
            restoring = false;
            RefreshPreview();
        }

        void CaptureAppearance()
        {
            if (restoring) return;
            draft.Appearance = new(fontSlider.Value / 100, AppearancePalette.ToHex(appColor.Picker.Color),
                AppearancePalette.ToHex(buttonColor.Picker.Color), AppearancePalette.ToHex(boxColor.Picker.Color));
            RefreshPreview();
        }
        fontSlider.ValueChanged += (_, _) => CaptureAppearance();
        appColor.Picker.ColorChanged += (_, _) => CaptureAppearance();
        buttonColor.Picker.ColorChanged += (_, _) => CaptureAppearance();
        boxColor.Picker.ColorChanged += (_, _) => CaptureAppearance();
        var dark = SettingsButton(SecondaryButton(U("Settings.PresetDark", "Dark", "Koyu"), ""), "settings.Preset.Dark");
        var light = SettingsButton(SecondaryButton(U("Settings.PresetLight", "Light", "Açık"), ""), "settings.Preset.Light");
        dark.Click += (_, _) =>
        {
            draft.Appearance = AppearanceSettingsDraft.Defaults with { FontScale = draft.Appearance.FontScale };
            RestoreAppearanceControls();
        };
        light.Click += (_, _) =>
        {
            draft.Appearance = new(draft.Appearance.FontScale, "#F8FAFC", AppearancePalette.DefaultButton, "#FFFFFF");
            RestoreAppearanceControls();
        };
        reset.Click += (_, _) =>
        {
            draft.Appearance = AppearanceSettingsDraft.Defaults;
            RestoreAppearanceControls();
        };
        apply.Click += async (_, _) =>
        {
            if (draft.AppearanceNeedsReview || draft.Appearance == draft.SavedAppearance) return;
            var values = draft.Appearance;
            await SaveSettingsDraftAsync(draft, SettingsSection.Appearance, () =>
            {
                _settings.FontScale = values.FontScale;
                _settings.AppBackgroundColor = values.Background;
                _settings.ButtonColor = values.Button;
                _settings.BoxColor = values.Box;
            });
        };

        panel.Children.Add(SettingsDraftReview(draft.AppearanceNeedsReview, "appearance", keep =>
        {
            if (!keep) draft.Appearance = draft.SavedAppearance;
            draft.AppearanceNeedsReview = false;
            RestoreAppearanceControls();
            fontSlider.Focus(FocusState.Programmatic);
        }));
        var presets = new Grid { ColumnSpacing = 8, RowSpacing = 8, MinWidth = 0, Children = { dark, light } };
        ConfigureResponsiveGrid(presets, 2, Font(120));
        var editor = new StackPanel { Spacing = 8, MinWidth = 0 };
        editor.Children.Add(SettingsText(U("Kids.Settings.ChooseColors", "Choose your colors", "Renklerini seç"), 18));
        editor.Children.Add(presets);
        editor.Children.Add(fontValue);
        editor.Children.Add(fontSlider);
        editor.Children.Add(SettingsText(textSizeHint, 18));
        editor.Children.Add(status);
        var layout = new Grid { ColumnSpacing = 16, RowSpacing = 14, MinWidth = 0, Children = { editor, preview } };
        ConfigureResponsiveGrid(layout, 2, Font(310));
        panel.Children.Add(layout);
        var advancedLabel = U("Kids.Settings.Advanced", "Advanced (for grown-ups)", "Gelişmiş (büyükler için)");
        var advancedContent = new StackPanel
        {
            Spacing = 12, MinWidth = 0,
            Children = { appColor.Row, buttonColor.Row, boxColor.Row, previewBackground, previewBoxColor, previewButtonColor, contrast },
        };
        ApplyReadableForeground(advancedContent, AppearancePalette.Current.BoxForegroundBrush);
        var advanced = StudyPopupButton(advancedLabel, advancedContent, "settings.AdvancedColors");
        var actions = new Grid { ColumnSpacing = 8, RowSpacing = 8, MinWidth = 0, Children = { apply, reset } };
        ConfigureResponsiveGrid(actions, 2, Font(200));
        panel.Children.Add(actions);
        panel.Children.Add(advanced);
        var card = Card(panel, 16);
        // Card applies saved foregrounds recursively, so the isolated draft preview is colored last.
        RefreshPreview();
        return card;
    }

    private static Color SettingsPreviewForeground(Color background) =>
        AppearancePalette.ContrastRatio(background, Microsoft.UI.Colors.White) >= AppearancePalette.ContrastRatio(background, Microsoft.UI.Colors.Black)
            ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;

    private void FitSettingsColorPicker((UIElement Row, ColorPicker Picker) setting, string id)
    {
        var picker = setting.Picker;
        picker.MinWidth = 0;
        picker.FontSize = ReadingSize(18);
        SettingsReadableContent(setting.Row);
        AutomationProperties.SetAutomationId(picker, "settings.ColorPicker." + id);
        void Fit()
        {
            var width = ContentScroll.ActualWidth;
            if (width <= 0) width = XamlRoot?.Size.Width ?? 360;
            var height = XamlRoot?.Size.Height ?? 720;
            picker.MaxWidth = Math.Max(0, Math.Min(340, width - 40));
            picker.MaxHeight = Math.Max(240, Math.Min(400, height - 180));
            picker.IsColorSpectrumVisible = picker.MaxWidth >= 280 && picker.MaxHeight >= 320;
            picker.IsColorPreviewVisible = picker.MaxWidth >= 280 && picker.MaxHeight >= 320;
            picker.IsMoreButtonVisible = false;
            picker.IsColorChannelTextInputVisible = true;
            picker.IsHexInputVisible = true;
        }
        picker.Loaded += (_, _) => Fit();
        if (setting.Row is Grid row)
        {
            row.MinWidth = 0;
            row.SizeChanged += (_, _) => Fit();
            foreach (var swatch in row.Children.OfType<Button>())
            {
                swatch.ClearValue(FrameworkElement.HeightProperty);
                SettingsButton(swatch, "settings.Color." + id);
                if (swatch.Flyout is Flyout flyout) flyout.Opening += (_, _) => Fit();
            }
        }
        Fit();
    }

    private FrameworkElement BuildAccountSettings(string markedKnownLabel)
    {
        var panel = new StackPanel { Spacing = 12, MinWidth = 0 };
        var explanation = SettingsText(U("Kids.Settings.GrownUpsHint",
            "Help with Google sign-in and copies of learning progress.",
            "Google ile oturum açma ve öğrenme ilerlemesinin kopyaları için yardım."), 18);
        explanation.Foreground = AppearancePalette.Current.BackgroundForegroundBrush;
        panel.Children.Add(explanation);
        var cloud = new StackPanel { Spacing = 10, MinWidth = 0 };
        AddCloudSection(cloud);
        cloud.Children.Add(SettingsText(CloudStorageNotice, 18));
        SettingsReadableContent(cloud);
        var backups = new StackPanel { Spacing = 10, MinWidth = 0 };
        backups.Children.Add(SettingsHeading(U("Kids.Settings.Backups", "Copies of learning progress", "Öğrenme ilerlemesinin kopyaları")));
        backups.Children.Add(SettingsText($"{_progress.KnownWords.Count:N0} {markedKnownLabel} · {_progress.FavoriteWords.Count:N0} {T("Home.Favorites")}", 18));
        var export = SettingsButton(SecondaryButton(U("Kids.Settings.BackupExport", "Make a copy", "Bir kopya oluştur"), "\uE950"), "settings.Backup.Export");
        export.Click += async (_, _) => await ExportBackupAsync();
        var import = SettingsButton(SecondaryButton(U("Kids.Settings.BackupImport", "Open a copy…", "Bir kopya aç…"), "\uE8B5"), "settings.Backup.Import");
        import.Click += async (_, _) => await ImportBackupAsync();
        var actions = new Grid { ColumnSpacing = 8, RowSpacing = 8, MinWidth = 0, Children = { export, import } };
        ConfigureResponsiveGrid(actions, 2, Font(170));
        backups.Children.Add(actions);
        backups.Children.Add(SettingsText(T("Backup.Hint"), 18));
        var layout = new Grid { ColumnSpacing = 12, RowSpacing = 12, MinWidth = 0 };
        layout.Children.Add(Card(cloud, 16));
        layout.Children.Add(Card(backups, 16));
        ConfigureResponsiveGrid(layout, 2, Font(340));
        panel.Children.Add(layout);
        return panel;
    }

    private UIElement SettingsDraftReview(bool needed, string section, Action<bool> resolve)
    {
        var panel = new StackPanel { Spacing = 8, MinWidth = 0, Visibility = needed ? Visibility.Visible : Visibility.Collapsed };
        AutomationProperties.SetAutomationId(panel, "settings.DraftReview." + section);
        panel.Children.Add(SettingsHeading(U("Kids.Settings.Unsaved", "Changes not saved", "Değişiklikler kaydedilmedi")));
        panel.Children.Add(SettingsText(U("Kids.Settings.DraftChangedHint",
            "Saved settings changed; your edits are still here, so choose what to keep before pressing Save.",
            "Kayıtlı ayarlar değişti; düzenlemelerin hâlâ burada, Kaydet'e basmadan önce hangilerini tutacağını seç."), 18));
        var saved = SettingsButton(SecondaryButton(U("Settings.UseSaved", "Use saved settings", "Kayıtlı ayarları kullan"), ""), "settings.UseSaved." + section);
        var keep = SettingsButton(SecondaryButton(U("Settings.KeepDraft", "Keep my edits", "Düzenlemelerimi koru"), ""), "settings.KeepDraft." + section);
        saved.Click += (_, _) => { resolve(false); panel.Visibility = Visibility.Collapsed; };
        keep.Click += (_, _) => { resolve(true); panel.Visibility = Visibility.Collapsed; };
        var actions = new Grid { ColumnSpacing = 8, RowSpacing = 8, MinWidth = 0, Children = { saved, keep } };
        ConfigureResponsiveGrid(actions, 2, Font(180));
        panel.Children.Add(actions);
        return panel;
    }

    private async Task SaveSettingsDraftAsync(SettingsDraftState draft, SettingsSection section, Action change)
    {
        if (_storageBlocked || _studyBusy || _dialogOpen || _navigationBusy) return;
        var learning = draft.Learning;
        var appearance = draft.Appearance;
        draft.Applying = section;
        try
        {
            await SaveUiSettingsAsync(() =>
            {
                change();
                RequestUiFocus(section == SettingsSection.Learning ? "settings.Section.learning" : "settings.Section.appearance");
            });
            // The shared save path handles rollback/recovery itself and reports failures in Notice.
            // Keep its original body intact, but disclose it only on request, not in the child-facing summary.
            if (Notice.IsOpen && Notice.Severity == InfoBarSeverity.Error)
            {
                var details = Notice.Content as UIElement ?? SettingsText(Notice.Message ?? "", 18);
                ShowSettingsSaveFailure(details);
            }
        }
        catch (Exception ex) { ShowSettingsSaveFailure(SettingsText(ex.Message, 18)); }
        finally
        {
            draft.Applying = null;
            // Even a render failure after persistence must not erase the user's attempted edits.
            var savedLearning = LearningSettingsDraft.From(_settings);
            var savedAppearance = AppearanceSettingsDraft.From(_settings);
            if (savedLearning != draft.SavedLearning) draft.Learning = learning;
            if (savedAppearance != draft.SavedAppearance) draft.Appearance = appearance;
            draft.SavedLearning = savedLearning;
            draft.SavedAppearance = savedAppearance;
            draft.SettingsSource = _settings;
            draft.ProgressSource = _progress;
        }
    }

    private void ShowSettingsSaveFailure(UIElement details)
    {
        var title = U("Kids.Settings.SaveFailedTitle", "Saving needs a grown-up's help", "Kaydetmek için bir büyüğünden yardım al");
        var summary = _storageBlocked
            ? U("Kids.Settings.SaveRecoveryHint", "Close and reopen YDKE with a grown-up before continuing.", "Devam etmeden önce bir büyüğünle YDKE'yi kapatıp yeniden aç.")
            : U("Kids.Settings.SaveFailedHint", "Your choices are still here; ask a grown-up to help you save.", "Seçimlerin hâlâ burada; kaydetmek için bir büyüğünden yardım iste.");
        // ShowNotice releases the previous content; never change recovery locks or the original error text.
        ShowNotice(title, summary, InfoBarSeverity.Error);
        SettingsReadableContent(details);
        var label = U("Kids.Settings.ErrorDetails", "Details for grown-ups", "Büyükler için ayrıntılar");
        var advanced = StudyPopupButton(label, details, "settings.SaveFailure.Details");
        Notice.Content = advanced;
    }

    private static TextBlock SettingsText(string text, double size) => new()
    {
        Text = text, FontSize = ReadingSize(size), LineHeight = ReadingSize(size) * 1.4,
        TextWrapping = TextWrapping.Wrap,
        TextTrimming = TextTrimming.None, HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private static TextBlock SettingsHeading(string text)
    {
        var heading = SettingsText(text, 22);
        heading.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        heading.Foreground = AppearancePalette.Current.BackgroundForegroundBrush;
        AutomationProperties.SetHeadingLevel(heading, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level2);
        return heading;
    }

    // Shared controls retain their behavior; only their local text and touch targets are adjusted.
    private static void SettingsReadableContent(UIElement element)
    {
        if (element is TextBlock text)
        {
            text.FontSize = Math.Max(ReadingSize(18), text.FontSize);
            text.LineHeight = text.FontSize * 1.4;
            text.TextWrapping = TextWrapping.Wrap;
            text.TextTrimming = TextTrimming.None;
        }
        if (element is Control control) control.FontSize = Math.Max(ReadingSize(18), control.FontSize);
        if (element is Microsoft.UI.Xaml.Controls.Primitives.ButtonBase button)
        {
            button.MinWidth = Math.Max(48, button.MinWidth);
            button.MinHeight = Math.Max(48, button.MinHeight);
        }
        if (element is Panel panel)
            foreach (var child in panel.Children) SettingsReadableContent(child);
        else if (element is Border { Child: UIElement child }) SettingsReadableContent(child);
        else if (element is ContentControl { Content: UIElement content }) SettingsReadableContent(content);
    }

    private CheckBox SettingsCheckBox(string label, string detail, bool value, string id)
    {
        var check = new CheckBox
        {
            IsChecked = value, MinWidth = 48, MinHeight = 48, FontSize = ReadingSize(18), HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel { Spacing = 4, MinWidth = 0, Children = { SettingsText(label, 18), SettingsText(detail, 18) } },
        };
        FocusTarget(check, id);
        AutomationProperties.SetName(check, label);
        AutomationProperties.SetHelpText(check, detail);
        return check;
    }

    private Button SettingsButton(Button button, string id)
    {
        button.MinWidth = 48;
        button.MinHeight = 48;
        button.FontSize = ReadingSize(18);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        SettingsReadableContent(button);
        FocusTarget(button, id);
        return button;
    }
}