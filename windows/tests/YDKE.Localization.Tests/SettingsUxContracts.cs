using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace YDKE_Windows;

// Source contracts only: no WinUI initialization, profile I/O, browser, or cloud access.
internal static class SettingsUxContracts
{
    public static void Verify(string sourceDirectory, Action<bool, string> check)
    {
        var assertions = 0;
        var failures = 0;
        void Test(bool condition, string message)
        {
            assertions++;
            if (!condition) failures++;
            check(condition, "Settings UX: " + message);
        }
        void Has(string text, string fragment, string message) => Test(Compact(text).Contains(Compact(fragment), StringComparison.Ordinal), message);
        var path = Path.Combine(sourceDirectory, "MainPage.Settings.cs");
        Test(File.Exists(path), "the native Settings partial must exist.");
        if (!File.Exists(path)) return;
        var source = File.ReadAllText(path);
        var render = Method(source, "RenderSettingsPage");
        var learning = Method(source, "BuildLearningSettings");
        var appearance = Method(source, "BuildAppearanceSettings");
        var account = Method(source, "BuildAccountSettings");
        var select = Method(render, "SelectSettingsSection");
        var reconcile = Method(source, "GetSettingsDraft");
        var save = Method(source, "SaveSettingsDraftAsync");
        var preview = Method(appearance, "RefreshPreview");
        var goal = Method(source, "TrySettingsGoal");

        Test(Regex.IsMatch(Code(source), @"\bprivate\s+void\s+RenderSettingsPage\s*\(\s*string\s+markedKnownLabel\s*\)"), "keep the private RenderSettingsPage(string markedKnownLabel) hook.");
        var main = File.ReadAllText(Path.Combine(sourceDirectory, "MainPage.xaml.cs"));
        Has(Method(main, "RenderProfile"), "RenderSettingsPage(MarkedKnownLabel)", "RenderProfile must delegate to the new page (parent integration hook).");
        Test(Regex.Matches(Code(render), @"\bAddPageHeader\s*\(").Count == 1, "exactly one page header is allowed.");
        Test(!render.Contains("QuickSettingsLink", StringComparison.Ordinal) && !render.Contains("quickLink", StringComparison.Ordinal),
            "the persistent toolbar must not be repeated as a second settings row.");
        Test(!Regex.IsMatch(Code(source), @"\bnew\s+ComboBox\b"), "do not duplicate language or level editors.");
        Has(source, "private SettingsSection _settingsSection = SettingsSection.Learning;", "default Learning and remember section selection per page instance.");
        Has(source, "private enum SettingsSection { Learning, Appearance, Account }", "keep the parent harness's section enum IDs unchanged.");
        Has(render, "T(\"Profile.Title\")", "retain the existing localized Profile title.");
        foreach (var key in new[] { "Kids.Settings.MyPractice", "Kids.Settings.ColorsText", "Kids.Settings.GrownUps" })
            Has(render, "\"" + key + "\"", "the section radio labels need child/grown-up copy: " + key);
        Test(Regex.Matches(Code(render), @"\bBuild(?:Learning|Appearance|Account)Settings\s*\(").Count == 3, "construct the three panels once per render.");
        foreach (var id in new[] { "learning", "appearance", "account" })
            Has(render, "\"" + id + "\",", "missing section route: " + id);
        Has(render, "new RadioButton", "section selection must expose native checked/selection semantics.");
        Has(render, "GroupName = groupName", "sections must form a single native radio group.");
        Has(render, "IsChecked = section.Section == _settingsSection", "restore the selected radio on rerender.");
        Has(render, "FocusTarget(tab, \"settings.Section.\" + section.Id)", "section AutomationIds must be stable.");
        Has(render, "AutomationProperties.SetName(tab, section.Label)", "section names must be localized and accessible.");
        Has(render, "tab.Checked += (_, _) => SelectSettingsSection(section.Section)", "radio selection must drive the visible section.");
        Has(select, "_settingsSection = selected", "selection must update only view state.");
        Has(select, "section.Panel.Visibility = section.Section == selected ? Visibility.Visible : Visibility.Collapsed", "only the selected panel may be visible.");
        Test(!Regex.IsMatch(Code(select), @"\b(?:Render\w*|Build\w*|Save\w*)\s*\(|\bnew\b|_settings\."), "tab changes must not rebuild controls or write preferences.");
        Test(!Regex.IsMatch(Code(source), @"\bRenderCurrentPage\s*\("), "only the existing persistence/cloud workflows should cause a full page rebuild.");

        Has(learning, "Minimum = 1, Maximum = 10000", "daily goal must retain the model's 1..10000 range.");
        Has(learning, "Value = _settings.DailyGoal", "do not replace the saved/default daily goal with an age-based value.");
        Has(learning, "\"Kids.Settings.DailyGoal\", \"(Daily goal) Words to practise each day\"", "the goal needs a simple label, not interval or all-languages jargon.");
        Has(learning, "ValidationMode = NumberBoxValidationMode.Disabled", "invalid goal text must not be silently replaced by a previous valid value.");
        Has(learning, "AcceptsExpression = false", "the daily goal is a whole number, not an expression editor.");
        Has(learning, "goalEditor?.Text ?? goal.Text", "validate the live editor text, not a potentially stale NumberBox.Value.");
        Has(learning, "goalEditor.TextChanged += (_, _) => CaptureGoal()", "capture unfinished goal input across account refreshes.");
        Has(learning, "draft.Learning = draft.Learning with { GoalText = GoalText() }", "goal text must remain in the in-memory draft.");
        Has(learning, "goal.Text = draft.Learning.GoalText", "rerenders must restore goal text.");
        Test(WholeGoalGuard(goal), "validate parse success, finiteness, integrality and bounds before casting the goal.");
        Has(goal, "(input.NumberFormatter as INumberParser)?.ParseDouble(text.Trim())", "goal parsing must use the NumberBox's actual number format.");
        Has(learning, "Live(error)", "inline goal validation must be an accessible live region.");
        Has(learning, "goal.Focus(FocusState.Programmatic)", "invalid submission must focus the goal.");
        Has(learning, "draft.GoalValidationShown = true", "a save attempt must expose validation errors.");
        Has(learning, "SaveSettingsDraftAsync(draft, SettingsSection.Learning", "learning must save through the existing guarded preferences path.");
        foreach (var id in new[] { "settings.DailyGoal", "settings.DailyGoal.Error", "settings.ReduceMotion", "settings.UntimedPractice", "settings.SaveLearning" })
            Has(learning, "\"" + id + "\"", "missing learning AutomationId: " + id);
        foreach (var key in new[] { "Kids.Settings.LearningSummary", "Kids.Settings.GoalHint", "Kids.Settings.MotionHint", "Kids.Settings.UntimedHint" })
            Has(learning, "\"" + key + "\"", "missing accurate saved-summary/purpose copy: " + key);

        var writes = Regex.Matches(Code(source), @"\b_settings\.(?<name>\w+)\s*=(?!=)")
            .Select(match => match.Groups["name"].Value).Order(StringComparer.Ordinal).ToArray();
        Test(writes.SequenceEqual(new[] { "AppBackgroundColor", "BoxColor", "ButtonColor", "DailyGoal", "FontScale", "ReduceMotion", "UntimedPractice" }), "only the seven existing learning/appearance fields may be assigned, once each.");
        Test(!Regex.IsMatch(Code(source), @"\b_storage\s*\.|\bJsonSerializer\b|\b(?:File|Directory)\s*\."), "the new UI must not bypass storage, add serialized fields, or perform profile I/O.");
        Has(save, "await SaveUiSettingsAsync", "delegate persistence, validation and rollback to SaveUiSettingsAsync.");
        Has(save, "if (_storageBlocked || _studyBusy || _dialogOpen || _navigationBusy) return", "respect all existing operation/recovery guards.");
        Has(save, "finally", "clear the apply marker even on failure.");
        Has(save, "draft.Applying = null", "a failed save must not leave future renders in apply mode.");
        Has(save, "draft.SavedLearning = savedLearning", "recover the saved learning baseline after a failed render/save.");
        Has(save, "draft.SavedAppearance = savedAppearance", "recover the saved appearance baseline after a failed render/save.");
        Has(save, "draft.Learning = learning", "retain attempted learning edits after a render failure.");
        Has(save, "draft.Appearance = appearance", "retain attempted appearance edits after a render failure.");
        Has(reconcile, "if (_settingsDraftState is null)", "do not replace drafts unconditionally on rerender.");
        Has(reconcile, "!ReferenceEquals(draft.SettingsSource, _settings)", "detect a settings replacement without reading storage.");
        Has(reconcile, "!ReferenceEquals(draft.ProgressSource, _progress)", "paired backup/cloud reloads must invalidate stale baselines, even when saved values match.");
        Has(reconcile, "draft.LearningNeedsReview = draft.Learning != learning", "preserve dirty learning edits for review after external replacement.");
        Has(reconcile, "draft.AppearanceNeedsReview = draft.Appearance != appearance", "preserve dirty appearance edits for review after external replacement.");
        Has(learning, "save.IsEnabled = !draft.LearningNeedsReview", "require an explained resolution of stale learning drafts, not silent overwrite.");
        Has(preview, "apply.IsEnabled = dirty && !draft.AppearanceNeedsReview", "apply only changed, reviewed appearance drafts.");
        var review = Method(source, "SettingsDraftReview");
        Has(review, "\"Kids.Settings.Unsaved\", \"Changes not saved\"", "draft conflicts need a friendly, honest unsaved label.");
        Has(review, "\"Kids.Settings.DraftChangedHint\"", "explain preserved edits and the need for Save after resolving a conflict.");
        Has(review, "Settings.UseSaved", "provide an explicit saved-settings choice after replacement.");
        Has(review, "Settings.KeepDraft", "provide an explicit keep-edits choice after replacement.");
        Test(!Regex.IsMatch(Code(review), @"\bSave\w*\s*\(|_settings\."), "resolving a draft conflict must not save preferences.");

        Has(appearance, "Minimum = 85, Maximum = 140", "text-size range must remain 85..140 percent.");
        Has(source, "public static AppearanceSettingsDraft Defaults => new(1,", "keep the existing 100% appearance default, not an age-specific persisted preference.");
        Has(preview, "values.FontScale * 100:0.##", "show a readable percentage alongside the slider.");
        foreach (var reference in new[] { "fontSlider.ValueChanged", "appColor.Picker.ColorChanged", "buttonColor.Picker.ColorChanged", "boxColor.Picker.ColorChanged" })
            Has(appearance, reference + " += (_, _) => CaptureAppearance()", "live preview must respond to " + reference);
        Test(IsolatedPreview(source), "draft preview must never mutate the global AppearancePalette or run ApplyAppearance.");
        Has(source, "AppearancePalette.ContrastRatio(background, Microsoft.UI.Colors.White)", "choose preview text against white locally.");
        Has(source, "AppearancePalette.ContrastRatio(background, Microsoft.UI.Colors.Black)", "choose preview text against black locally.");
        Has(preview, "previewWord.FontSize = Math.Max(18, 28 * values.FontScale)", "sample word size must use the draft once with an 18px floor, not globally scaled Font().");
        Has(preview, "previewMeaning.FontSize = Math.Max(18, 20 * values.FontScale)", "sample meaning size must use the draft with an 18px floor.");
        Has(preview, "previewButtonLabel.FontSize = Math.Max(18, 18 * values.FontScale)", "sample button size must use the draft with an 18px floor.");
        Has(preview, "previewTitle.FontSize = Math.Max(18, 20 * values.FontScale)", "the preview title must stay readable at the smallest draft scale.");
        Has(preview, "previewHint.FontSize = Math.Max(18, 18 * values.FontScale)", "the preview's save hint must stay readable at the smallest draft scale.");
        Has(preview, "text.LineHeight = text.FontSize * 1.4", "preview line spacing must track its draft size.");
        Has(preview, "AppearancePalette.ContrastRatio(box, boxInk)", "the reported contrast must include the panel text.");
        Has(preview, "AppearancePalette.ContrastRatio(button, buttonInk)", "the reported contrast must include the button text.");
        Has(appearance, "\"Kids.Settings.TryItHere\", \"Try it here\"", "the preview needs a clear child-facing label.");
        Has(appearance, "\"Kids.Settings.PreviewHint\", \"Nothing changes until you press Save.\"", "clearly distinguish the isolated preview from saved settings.");
        Has(preview, "\"Kids.Settings.PreviewPending\", \"Looks good? Save your colors.\"", "use friendly save feedback instead of contrast jargon.");
        foreach (var key in new[] { "Kids.Settings.SampleWord", "Kids.Settings.SampleMeaning" })
            Has(appearance, "\"" + key + "\"", "missing simple localized preview sample: " + key);
        Test(!Regex.IsMatch(Code(appearance), @"\b_words\b|\bLocalizedPart\s*\("), "preview examples must not pull potentially advanced words from the study data.");
        Has(appearance, "var card = Card(panel, 16);", "apply the saved section styling before recoloring the isolated preview.");
        Test(Regex.IsMatch(Code(appearance), @"RefreshPreview\s*\(\s*\)\s*;\s*return\s+card\s*;"), "the preview must be recolored after Card's foreground traversal.");
        Has(Method(source, "SettingsText"), "TextWrapping = TextWrapping.Wrap", "all sample/control copy must wrap.");
        Has(Method(source, "SettingsText"), "TextTrimming = TextTrimming.None", "do not truncate sample fields.");
        Test(ReadableSettingsText(Method(source, "SettingsText")), "settings copy must use the parent's ReadingSize floor and 1.4 line spacing.");
        Test(!Regex.IsMatch(Code(appearance), @"\b(?:Viewbox|MaxLines|MaxHeight|Height)\b"), "preview content must not be scaled down or vertically clipped.");
        foreach (var button in new[] { "dark", "light", "reset" })
        {
            var handler = ClickHandler(appearance, button);
            Has(handler, "draft.Appearance =", button + " must change the local draft.");
            Test(!Regex.IsMatch(Code(handler), @"_settings\.|\b(?:Save\w*|ApplyAppearance)\s*\("), button + " must not persist or change global appearance.");
        }
        Has(appearance, "draft.Appearance.FontScale, \"#F8FAFC\", AppearancePalette.DefaultButton, \"#FFFFFF\"", "the light preset must have an explicit supported palette.");
        Test(!appearance.Contains("Settings.PresetSystem", StringComparison.Ordinal), "do not advertise an unimplemented system appearance mode.");
        Has(appearance, "StudyPopupButton(advancedLabel, advancedContent, \"settings.AdvancedColors\")", "advanced colors must open outside the fixed page instead of expanding it.");
        Test(!source.Contains("AdvancedColorsExpanded", StringComparison.Ordinal), "popup color tools must not retain obsolete inline-expansion state.");
        var advanced = Initializer(appearance, "advancedContent");
        foreach (var field in new[] { "appColor.Row", "buttonColor.Row", "boxColor.Row", "previewBackground", "previewBoxColor", "previewButtonColor", "contrast" })
            Has(advanced, field, "technical color controls/details belong inside Advanced: " + field);
        var previewContent = Initializer(appearance, "preview") + Initializer(appearance, "previewBox");
        Test(!Regex.IsMatch(Code(previewContent), @"\b(?:previewBackground|previewBoxColor|previewButtonColor|contrast)\b"), "do not expose hex codes or contrast numbers in the main sample.");
        Test(!Regex.IsMatch(Code(appearance), @"\b(?:editor|panel)\.Children\.Add\s*\(\s*(?:previewBackground|previewBoxColor|previewButtonColor|contrast)\s*\)"), "do not add technical color values directly to the simple editor.");
        Has(appearance, "ApplyReadableForeground(advancedContent, AppearancePalette.Current.BoxForegroundBrush)", "advanced details must use the saved panel foreground, not the isolated preview colors.");
        foreach (var id in new[] { "settings.FontScale", "settings.FontScale.Value", "settings.Preview", "settings.Preview.Word", "settings.Preview.Meaning", "settings.Preview.Button", "settings.Preview.Contrast", "settings.Appearance.Status", "settings.ApplyAppearance", "settings.ResetAppearanceDraft", "settings.Preset.Dark", "settings.Preset.Light", "settings.AdvancedColors" })
            Has(appearance, "\"" + id + "\"", "preserve existing appearance AutomationId: " + id);
        var fit = Method(source, "FitSettingsColorPicker");
        Has(fit, "picker.MinWidth = 0", "remove the shared picker's 300px minimum locally.");
        Has(fit, "picker.MaxWidth = Math.Max(0, Math.Min(340, width - 40))", "flyout width must remain nonnegative and viewport bounded.");
        Has(fit, "picker.MaxHeight = Math.Max(240, Math.Min(400, height - 180))", "color picker flyouts must remain vertically bounded without page scrolling.");
        Has(fit, "picker.IsHexInputVisible = true", "compact color pickers must keep a usable text entry.");
        Has(fit, "SettingsButton(swatch, \"settings.Color.\" + id)", "keep swatch IDs while enlarging the existing buttons.");
        Has(fit, "swatch.ClearValue(FrameworkElement.HeightProperty)", "the inherited 44px swatch height must not constrain its touch target.");

        Has(account, "AddCloudSection(cloud)", "reuse the existing guarded sign-in/cloud controls.");
        Has(render, "\"Kids.Settings.GrownUps\", \"For grown-ups\"", "the account tab must clearly mark cloud and backups for grown-ups.");
        Has(account, "\"Kids.Settings.GrownUpsHint\", \"Help with Google sign-in and copies of learning progress.\"", "give grown-ups a single short section explanation.");
        Has(account, "export.Click += async (_, _) => await ExportBackupAsync()", "export must use the existing local backup workflow.");
        Has(account, "import.Click += async (_, _) => await ImportBackupAsync()", "import must retain the existing confirmation/recovery workflow.");
        Has(account, "SettingsText(T(\"Backup.Hint\"), 18)", "keep the existing backup scope/replacement explanation at a readable size.");
        Has(account, "_progress.KnownWords.Count:N0} {markedKnownLabel}", "known counts must use the caller's manual-bookmark label.");
        Has(account, "_progress.FavoriteWords.Count", "show local favorite counts.");
        foreach (var section in new[] { learning, appearance })
            Test(!Regex.IsMatch(Code(section), @"\b(?:AddCloudSection|ExportBackupAsync|ImportBackupAsync)\s*\("), "keep account and backup controls out of the child-facing sections.");
        Test(!Regex.IsMatch(Code(source), @"\bnew\s+(?:PasswordBox|DatePicker|CalendarDatePicker|HttpClient|TelemetryClient)\b|\b(?:DateOfBirth|ChildAge)\b"), "do not collect age/passwords or introduce network/analytics clients.");
        Test(!Regex.IsMatch(Code(source), @"\b(?:CloudLoadAsync|CloudDeleteAsync|BeginGoogleSignInAsync|ApplyImportAsync|DeleteCloudProfileAsync|AddProfileLearningControls)\s*\("), "do not duplicate cloud/import/destructive logic or the old combined settings block.");

        Has(save, "if (Notice.IsOpen && Notice.Severity == InfoBarSeverity.Error)", "disclose failures already handled by the shared save path without bypassing rollback.");
        Has(save, "Notice.Content as UIElement ?? SettingsText(Notice.Message ?? \"\", 18)", "preserve the shared save path's original error body, including long structured details.");
        Has(save, "ShowSettingsSaveFailure(SettingsText(ex.Message, 18))", "an escaped save exception must also use the readable disclosure.");
        var failure = Method(source, "ShowSettingsSaveFailure");
        Has(failure, "StudyPopupButton(label, details, \"settings.SaveFailure.Details\")", "error details must open in a popup without modifying their body or growing the page.");
        Has(failure, "Notice.Content = advanced", "place the details outside the bare notice summary.");
        Has(failure, "var summary = _storageBlocked", "recovery-locked storage must get restart guidance instead of a retry prompt.");
        Has(failure, "\"Kids.Settings.SaveRecoveryHint\"", "a recovery failure must offer readable restart guidance.");
        Has(failure, "ShowNotice(title, summary, InfoBarSeverity.Error)", "show the short failure summary, not raw JSON.");
        Test(!Regex.IsMatch(Code(failure), @"_storageBlocked\s*=|\.IsClosable\s*=|\bSetStudyBusy\s*\("), "the disclosure must never unlock or dismiss storage recovery protections.");

        foreach (var (body, variable) in new[] { (render, "tab"), (learning, "goal"), (appearance, "fontSlider"), (appearance, "previewButton"), (Method(source, "SettingsCheckBox"), "check") })
        {
            var control = Initializer(body, variable);
            Has(control, "MinWidth = 48", "control touch width must be at least 48px: " + variable);
            Has(control, "MinHeight = 48", "control touch height must be at least 48px: " + variable);
            Has(control, "FontSize = ReadingSize(18)", "control text must honor the reading floor: " + variable);
        }
        var buttonStyle = Method(source, "SettingsButton");
        Has(buttonStyle, "button.MinWidth = 48", "settings action and preset widths must be at least 48px.");
        Has(buttonStyle, "button.MinHeight = 48", "settings action and preset heights must be at least 48px.");
        Has(buttonStyle, "button.FontSize = ReadingSize(18)", "settings action and preset text must be at least 18px.");
        Has(buttonStyle, "SettingsReadableContent(button)", "shared button labels must not retain smaller explicit font sizes.");
        var readableContent = Method(source, "SettingsReadableContent");
        Has(readableContent, "text.FontSize = Math.Max(ReadingSize(18), text.FontSize)", "locally reused text must retain any larger existing size.");
        Has(readableContent, "text.LineHeight = text.FontSize * 1.4", "shared copy must have readable line spacing.");

        var kidsKeys = Regex.Matches(source, "\\bU\\s*\\(\\s*\"(?<key>Kids\\.Settings\\.[^\"]+)\"");
        var kidsPairs = Regex.Matches(source, "\\bU\\s*\\(\\s*\"(?<key>Kids\\.Settings\\.[^\"]+)\"\\s*,\\s*\"(?<en>(?:\\\\.|[^\"\\\\])+)\"\\s*,\\s*\"(?<tr>(?:\\\\.|[^\"\\\\])+)\"\\s*\\)");
        Test(kidsKeys.Count > 0 && kidsKeys.Count == kidsPairs.Count, "every Kids.Settings key needs literal English and Turkish fallbacks for the parent localization pass.");
        foreach (var group in kidsPairs.GroupBy(match => match.Groups["key"].Value))
            Test(group.Select(match => (match.Groups["en"].Value, match.Groups["tr"].Value)).Distinct().Count() == 1, "repeated fallback pairs must agree: " + group.Key);
        foreach (var key in new[] { "Kids.Settings.GoalHint", "Kids.Settings.MotionHint", "Kids.Settings.UntimedHint" })
        {
            var copy = kidsPairs.FirstOrDefault(match => match.Groups["key"].Value == key);
            Test(copy is not null && new[] { "en", "tr" }.All(language => Regex.Matches(copy.Groups[language].Value, @"[.!?]").Count <= 1), "learning instructions should be at most one sentence: " + key);
        }
        foreach (var fragment in new[] { "ConfigureResponsiveGrid(navigation, 3", "ConfigureResponsiveGrid(rows, 2", "ConfigureResponsiveGrid(layout, 2", "ConfigureResponsiveGrid(actions, 2", "ConfigureResponsiveGrid(presets, 2" })
            Has(source, fragment, "missing responsive native row: " + fragment);
        Test(!Regex.IsMatch(Code(source), @"\bWidth\s*="), "new root/section controls must not impose a fixed width.");

        // Negative controls mutate the real helper strings, never production files or copied UI fixtures.
        Test(!WholeGoalGuard(goal.Replace("!double.IsFinite(value)", "false", StringComparison.Ordinal)), "goal contract must reject a missing finite-value guard.");
        Test(!WholeGoalGuard(goal.Replace("value is < 1 or > 10000", "value < 0", StringComparison.Ordinal)), "goal contract must reject weakened model bounds.");
        Test(!IsolatedPreview(source + "\nAppearancePalette.SetCurrent(_settings);"), "preview isolation contract must reject a global palette write.");
        Test(!ReadableSettingsText(Method(source, "SettingsText").Replace("ReadingSize(size)", "Font(size)", StringComparison.Ordinal)), "readability contract must reject bypassing the shared 18px floor.");
        Console.WriteLine($"SETTINGS_UX checks={assertions} errors={failures} sections=3 profile-io=none");
    }

    private static bool WholeGoalGuard(string source) => new[]
    {
        "parsed is not double value", "!double.IsFinite(value)", "value != Math.Truncate(value)", "value is < 1 or > 10000",
    }.All(fragment => Compact(source).Contains(Compact(fragment), StringComparison.Ordinal));

    private static bool IsolatedPreview(string source) => !Regex.IsMatch(Code(source), @"\b(?:SetCurrent|ApplyAppearance)\s*\(");
    private static bool ReadableSettingsText(string source) => new[]
    {
        "FontSize = ReadingSize(size)", "LineHeight = ReadingSize(size) * 1.4",
    }.All(fragment => Compact(source).Contains(Compact(fragment), StringComparison.Ordinal));
    private static string Compact(string source) => Regex.Replace(source, @"\s+", "");
    private static string Code(string source) => Regex.Replace(source,
        "//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/|@?\"(?:\"\"|\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'",
        match => new string(' ', match.Length));

    private static string Method(string source, string name)
    {
        var code = Code(source);
        var matches = Regex.Matches(code, @"\b(?:void|bool|Task|UIElement|FrameworkElement|SettingsDraftState|TextBlock|TextBox\?|Color|CheckBox|Button)\s+" + Regex.Escape(name) + @"\s*\([^;{}]*?\)\s*(?:=>|\{)");
        if (matches.Count != 1) throw new InvalidOperationException($"Expected one real {name} method, found {matches.Count}.");
        var start = matches[0].Index + matches[0].Length;
        return code[start - 1] == '{' ? source[start..Close(code, start - 1)] : source[start..code.IndexOf(';', start)];
    }

    private static string Initializer(string source, string name)
    {
        var code = Code(source);
        var match = Regex.Match(code, @"\bvar\s+" + Regex.Escape(name) + @"\s*=\s*new\s+\w+\s*\{");
        if (!match.Success) throw new InvalidOperationException("Missing Settings control initializer: " + name);
        var start = match.Index + match.Length;
        return source[start..Close(code, start - 1)];
    }

    private static string ClickHandler(string source, string name)
    {
        var code = Code(source);
        var match = Regex.Match(code, @"\b" + Regex.Escape(name) + @"\.Click\s*\+=\s*\([^)]*\)\s*=>\s*\{");
        if (!match.Success) throw new InvalidOperationException("Missing real draft handler: " + name);
        var start = match.Index + match.Length;
        return source[start..Close(code, start - 1)];
    }

    private static int Close(string code, int start)
    {
        var depth = 0;
        for (var index = start; index < code.Length; index++)
        {
            if (code[index] == '{') depth++;
            else if (code[index] == '}' && --depth == 0) return index;
        }
        throw new InvalidOperationException("Unbalanced Settings method body.");
    }
}