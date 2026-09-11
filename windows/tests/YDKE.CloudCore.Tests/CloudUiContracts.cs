using System.Text.RegularExpressions;
using System.Xml.Linq;
using YDKE_Windows;

internal static partial class Program
{
    private static void AddCloudUiTests(List<(string Name, Func<Task> Run)> tests)
    {
        void Add(string name, Action verify) => tests.Add((name, () => { verify(); return Task.CompletedTask; }));

        Add("Cloud UI: profile isolation is explicit and never probes real user credentials", () =>
        {
            var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YDKE");
            Equal(expected, AppDataPaths.ResolveFolder([]));
            Equal(expected, AppDataPaths.ResolveFolder(["--page=home"]));
            var isolated = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "scratch", "isolated profile"));
            Equal(isolated, AppDataPaths.ResolveFolder(["--test-mode", "--data-dir=" + isolated]));
            foreach (var args in new[]
            {
                new[] { "--test-mode" }, new[] { "--data-dir=" + isolated },
                new[] { "--test-mode", "--data-dir=relative" }, new[] { "--test-mode", "--data-dir=" },
                new[] { "--test-mode", "--data-dir=" + isolated, "--data-dir=" + isolated },
            })
            {
                var rejected = false;
                try { AppDataPaths.ResolveFolder(args); }
                catch (ArgumentException) { rejected = true; }
                Check(rejected, "Unsafe or ambiguous profile arguments were accepted.");
            }
            var main = CloudUiSource("MainPage.xaml.cs");
            Check(main.Contains("AppStorage _storage = new(AppDataPaths.CurrentFolder)", StringComparison.Ordinal));
            Check(main.Contains("new CloudCredentialStore(AppDataPaths.CurrentFolder)", StringComparison.Ordinal));
            Check(CloudUiSource("App.xaml.cs").Contains("var folder = AppDataPaths.CurrentFolder;", StringComparison.Ordinal));
        });

        Add("Cloud UI: Home Profile and toolbar route one click to the same account action", () =>
        {
            var source = CloudUiSource("MainPage.Library.cs");
            var action = CloudUiMember(source, "CloudAccountAction");
            Check(action.Contains("var connected = _cloud.IsConnected;", StringComparison.Ordinal));
            Check(action.Contains("primary: !connected", StringComparison.Ordinal));
            Check(action.Contains("button.Click += OnLocalStatusButtonClick;", StringComparison.Ordinal));
            Check(CloudUiMember(source, "AddHomeAccountCard").Contains("CloudAccountAction(\"home.AccountAction\")", StringComparison.Ordinal));
            Check(CloudUiMember(source, "AddCloudSection").Contains("CloudAccountAction(\"profile.AccountAction\")", StringComparison.Ordinal));
            var toggle = CloudUiMember(source, "OnLocalStatusButtonClick");
            Check(toggle.Contains("if (_cloud.IsConnected) await DisconnectCloudAsync();", StringComparison.Ordinal));
            Check(toggle.Contains("else await BeginGoogleSignInAsync();", StringComparison.Ordinal));
            CloudUiForbids(toggle, "NavigateTo", "LastGoogle", "ConfirmAsync", "ShowAsync");
            var xaml = XDocument.Parse(CloudUiSource("MainPage.xaml"));
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var toolbar = xaml.Descendants().Single(e => (string?)e.Attribute(x + "Name") == "LocalStatusButton");
            Equal("Button", toolbar.Name.LocalName);
            Equal("cloud.AccountToggle", (string?)toolbar.Attribute("AutomationProperties.AutomationId"));
            Equal("OnLocalStatusButtonClick", (string?)toolbar.Attribute("Click"));
            Equal("False", (string?)toolbar.Attribute("IsEnabled"));
        });

        Add("Cloud UI: Home has one compact account row and a direct account management link", () =>
        {
            var source = CloudUiSource("MainPage.Library.cs");
            var home = CloudUiMember(source, "RenderHome");
            CloudUiBefore(home, "AddPageHeader(", "AddHomeAccountCard();");
            CloudUiBefore(home, "home.Play", "AddHomeAccountCard();");
            Check(home.Contains("AddHomeAccountCard();", StringComparison.Ordinal));
            var card = CloudUiMember(source, "AddHomeAccountCard");
            Check(card.Contains("CloudAccountAction(\"home.AccountAction\")", StringComparison.Ordinal));
            Check(card.Contains("Children = { copy, actions }", StringComparison.Ordinal));
            Equal(1, Regex.Matches(card, @"PageContent\.Children\.Add\(").Count);
            Check(card.Contains("CloudDialogText(CloudFamilyHint)", StringComparison.Ordinal));
            Check(card.Contains("home.ManageSync", StringComparison.Ordinal));
            CloudUiBefore(card, "_settingsSection = SettingsSection.Account;", "NavigateTo(\"profile\", ProfileItem);");
            Check(card.Contains("else actions.Children.Add(CloudBrowserButton(\"home.OtherBrowser\"));", StringComparison.Ordinal));
            var account = CloudUiMember(source, "CloudAccountName");
            CloudUiBefore(account, "!string.IsNullOrWhiteSpace(_cloud.DisplayName)", "!string.IsNullOrWhiteSpace(_cloud.Email)");
            Check(account.Contains("Cloud.GoogleAccount", StringComparison.Ordinal));
            CloudUiForbids(card, "CloudDeleteAsync", "CloudSaveAsync", "CloudLoadAsync");
        });

        Add("Cloud UI: deletion stays behind grown-up tools separate from sign-out", () =>
        {
            var source = CloudUiSource("MainPage.Library.cs");
            var profile = CloudUiMember(source, "AddCloudSection");
            Check(profile.Contains("CloudAccountAction(\"profile.AccountAction\")", StringComparison.Ordinal));
            Check(profile.Contains("StudyPopupButton(U(\"Cloud.GrownUpTools\"", StringComparison.Ordinal));
            Check(profile.Contains("), deleteCloud, \"cloud.GrownUpTools\")", StringComparison.Ordinal));
            Check(profile.Contains("deleteCloud.Click += async (_, _) => await CloudDeleteAsync();", StringComparison.Ordinal));
            CloudUiBefore(profile, "CloudAccountAction(\"profile.AccountAction\")", "var tools = StudyPopupButton(");
            CloudUiForbids(profile, "panel.Children.Add(deleteCloud)", "actions.Children.Add(deleteCloud)");
            var toolsPopup = CloudUiMember(CloudUiSource("MainPage.Study.cs"), "StudyPopupButton");
            Check(toolsPopup.Contains("button.Flyout = flyout;", StringComparison.Ordinal));
            CloudUiForbids(toolsPopup, "ShowAt", "CloudDeleteAsync");
            CloudUiBefore(CloudUiMember(source, "CloudDeleteAsync"), "await ConfirmAsync(", "await _cloud.DeleteCloudProfileAsync();");
            CloudUiForbids(CloudUiMember(source, "CloudAccountAction"), "Delete", "ConfirmAsync");
        });

        Add("Cloud UI: initialization loading busy and closing states disable all account controls", () =>
        {
            var source = CloudUiSource("MainPage.Library.cs");
            var guard = CloudUiMember(source, "CloudUiUnavailable");
            foreach (var required in new[] { "!IsLoaded", "!_initialized", "QuickUiLanguage.ItemsSource is null", "_studyBusy", "_dialogOpen", "_dialogClosed is not null", "_navigationBusy", "LoadingRing.IsActive" })
                Check(guard.Contains(required, StringComparison.Ordinal), "Missing cloud action guard: " + required);
            foreach (var member in new[] { "BeginGoogleSignInAsync", "DisconnectCloudAsync", "OnLocalStatusButtonClick", "CloudSaveAsync", "CloudLoadAsync", "CloudDeleteAsync" })
                Check(CloudUiMember(source, member).Contains("if (CloudUiUnavailable", StringComparison.Ordinal), "Unguarded entry: " + member);
            var binding = CloudUiMember(source, "BindCloudAction");
            Check(binding.Contains("SetBinding(Control.IsEnabledProperty", StringComparison.Ordinal));
            Check(binding.Contains("Source = LocalStatusButton", StringComparison.Ordinal));
            Check(binding.Contains("nameof(Control.IsEnabled)", StringComparison.Ordinal));
            Check(binding.Contains("BindingMode.OneWay", StringComparison.Ordinal));
            Check(CloudUiMember(source, "CloudActionButton").Contains("BindCloudAction(button);", StringComparison.Ordinal));
            Check(CloudUiMember(source, "AddHomeAccountCard").Contains("BindCloudAction(manage);", StringComparison.Ordinal));
            Check(CloudUiMember(CloudUiSource("MainPage.Study.cs"), "SetStudyBusy")
                .Contains("LocalStatusButton.IsEnabled = !CloudUiUnavailable;", StringComparison.Ordinal));
            var main = CloudUiSource("MainPage.xaml.cs");
            Check(CloudUiMember(main, "SyncQuickSettingsBar").Contains("SyncAccountStatus();", StringComparison.Ordinal));
            var navigation = CloudUiMember(main, "OnNavigationSelectionChanged");
            Check(Regex.IsMatch(navigation, @"_navigationBusy = true;\s+SyncAccountStatus\(\);"));
            Check(navigation.Contains("finally { _navigationBusy = false; SyncAccountStatus(); }", StringComparison.Ordinal));
        });

        Add("Cloud UI: normal sign-in uses the saved browser or default without a question", () =>
        {
            var source = CloudUiSource("MainPage.Library.cs");
            var begin = CloudUiMember(source, "BeginGoogleSignInAsync");
            Check(begin.Contains("GoogleSignInBrowser? preferredBrowser = null", StringComparison.Ordinal));
            Check(begin.Contains("var browser = preferredBrowser ?? _cloud.LastGoogleBrowser;", StringComparison.Ordinal));
            Check(begin.Contains("if (!Enum.IsDefined(browser)) browser = GoogleSignInBrowser.Default;", StringComparison.Ordinal));
            CloudUiBefore(begin, "!Enum.IsDefined(browser)", "await ConnectCloudAsync(browser);");
            Equal(1, Regex.Matches(begin, @"await ConnectCloudAsync\(").Count);
            CloudUiForbids(begin, "ConfirmAsync", "ShowAsync", "LastGoogleClientId", "LastGoogleClientSecret");
        });

        Add("Cloud UI: optional secondary browser menu directly starts Edge or Chrome", () =>
        {
            var source = CloudUiSource("MainPage.Library.cs");
            var menu = CloudUiMember(source, "CloudBrowserButton");
            Check(menu.Contains("new MenuFlyout()", StringComparison.Ordinal));
            Check(menu.Contains("button.Flyout = menu;", StringComparison.Ordinal));
            Equal(2, Regex.Matches(menu, @"new MenuFlyoutItem\b").Count);
            Check(menu.Contains("CloudActionButton(U(\"Cloud.UseAnotherBrowser\"", StringComparison.Ordinal));
            Check(CloudUiMember(source, "CloudActionButton").Contains("bool primary = false", StringComparison.Ordinal));
            CloudUiForbids(menu, "primary: true", "ConfirmAsync", "ShowAsync", "SelectedItem");
            Check(menu.Contains("edge.Click += async (_, _) => await BeginGoogleSignInAsync(GoogleSignInBrowser.Edge);", StringComparison.Ordinal));
            Check(menu.Contains("chrome.Click += async (_, _) => await BeginGoogleSignInAsync(GoogleSignInBrowser.Chrome);", StringComparison.Ordinal));
            Check(menu.Contains("BindCloudAction(edge);", StringComparison.Ordinal) && menu.Contains("BindCloudAction(chrome);", StringComparison.Ordinal));
            Check(menu.Contains("Tag = button", StringComparison.Ordinal));
            Check(CloudUiMember(source, "BeginGoogleSignInAsync").Contains("MenuFlyoutItem { Tag: Control owner }", StringComparison.Ordinal));
        });

        Add("Cloud UI: no forms setup identifiers credential handling or raw sign-in diagnostics remain", () =>
        {
            var source = CloudUiSource("MainPage.Library.cs");
            var cloud = CloudUiRegion(source);
            Check(!Regex.IsMatch(cloud, @"\b(?:TextBox|PasswordBox|RichEditBox|AutoSuggestBox|ComboBox|NumberBox|DatePicker|CalendarDatePicker)\b"), "Cloud UI must not collect any sign-in input or birth date.");
            CloudUiForbids(source, "_cloudClientIdDraft", "_cloudClientSecretDraft", "_cloudBrowserDraft", "_cloudDraftInitialized");
            CloudUiForbids(cloud, "ConfigureGoogleSignInAsync", "EnsureCloudSignInDraft", "CloudSettingsButton", "CloudBrowserName",
                "GoogleAuthService", "GoogleBrowserLauncher", "GoogleLoopbackListener", "new HttpClient", "new CloudCredentialStore",
                "Cloud.ClientId", "Cloud.ClientSecret", "cloud.ClientId", "cloud.ClientSecret", "cloud.Settings", "cloud.Browser\"",
                "cloud.ConfigurationError", "cloud.SignInDialog", "console.cloud.google.com", "Google Cloud Console", "NavigateUri", "WebView", "Cookie");
            var ui = string.Join('\n', new[] { "MainPage.Library.cs", "MainPage.xaml.cs", "MainPage.Settings.cs", "MainPage.Study.cs", "MainPage.Shell.cs" }.Select(CloudUiSource));
            CloudUiForbids(ui, "_cloud.SignInAsync(", ".LastGoogleClientId", ".LastGoogleClientSecret");
            var auth = string.Join('\n', new[] { "BeginGoogleSignInAsync", "ConnectCloudAsync", "CloudSignInFailureMessage", "ShowCloudSignOutFailure", "DisconnectCloudAsync" }
                .Select(name => CloudUiMember(source, name)));
            Check(!Regex.IsMatch(auth, @"\b(?:Console|Debug|Trace|ILogger)\."));
            Check(!Regex.IsMatch(auth, @"ShowNotice\([^;]*(?:ErrorMessage|ex\.Message|IdToken|RefreshToken)"));
            var help = CloudUiMember(source, "CloudSignInFailureMessage");
            Check(help.Contains("CloudSignInFailureMessage()", StringComparison.Ordinal));
            Check(help.Contains("We could not sign in. Try again, or ask a grown-up for help.", StringComparison.Ordinal));
            CloudUiForbids(help, "invalid_client", "redirect", "Desktop", "ErrorMessage", "client secret");
        });

        Add("Cloud UI: one readable Cancel-only dialog explains the external browser handoff", () =>
        {
            var source = CloudUiSource("MainPage.Library.cs");
            var connect = CloudUiMember(source, "ConnectCloudAsync");
            Equal(1, Regex.Matches(connect, @"new ContentDialog\b").Count);
            Check(connect.Contains("Finish in your browser", StringComparison.Ordinal));
            Check(connect.Contains("Google will open in your browser. Sign in there, then come back.", StringComparison.Ordinal));
            Check(connect.Contains("If the browser shows Continue with Google, choose it.", StringComparison.Ordinal));
            Equal(1, Regex.Matches(connect, @"content\.Children\.Add\(CloudDialogText\(").Count);
            Check(connect.Contains("CloseButtonText = U(\"Dialog.Cancel\", \"Cancel\", \"İptal\")", StringComparison.Ordinal));
            Check(connect.Contains("DefaultButton = ContentDialogButton.Close", StringComparison.Ordinal));
            Check(connect.Contains("XamlRoot = XamlRoot, RequestedTheme = RequestedTheme", StringComparison.Ordinal));
            Check(connect.Contains("Content = content, FontSize = Math.Max(18, Font(18))", StringComparison.Ordinal));
            Check(connect.Contains("ConfigureReadingDialog(waitDialog)", StringComparison.Ordinal));
            CloudUiForbids(connect, "ScrollableDialogContent", "new ScrollViewer");
            Check(connect.Contains("\"cloud.WaitDialog\"", StringComparison.Ordinal));
            CloudUiForbids(connect, "PrimaryButtonText", "SecondaryButtonText", "PrimaryButtonClick", "SecondaryButtonClick");
            Check(CloudUiMember(source, "CloudDialogText").Contains("FontSize = Math.Max(18, Font(18))", StringComparison.Ordinal));
            var auth = connect + CloudUiMember(source, "BeginGoogleSignInAsync");
            Check(!Regex.IsMatch(auth, @"\b(?:while|for|foreach)\s*\("), "Sign-in failure/cancel must not open a retry or setup loop.");
        });

        Add("Cloud UI: browser launch happens only after Opened and receives cancellation and language", () =>
        {
            var connect = CloudUiMember(CloudUiSource("MainPage.Library.cs"), "ConnectCloudAsync");
            Check(connect.Contains("waitDialog.Opened += (_, _) => opened.TrySetResult(true);", StringComparison.Ordinal));
            CloudUiBefore(connect, "dialogTask = waitDialog.ShowAsync().AsTask();", "await Task.WhenAny(opened.Task, dialogTask, abandonedSignal.Task);");
            CloudUiBefore(connect, "await Task.WhenAny(opened.Task, dialogTask, abandonedSignal.Task);", "await opened.Task;");
            Check(connect.Contains("if (opened.Task.IsCompletedSuccessfully && !abandoned && IsLoaded && !dialogTask.IsCompleted)", StringComparison.Ordinal));
            CloudUiBefore(connect, "await opened.Task;", "_cloud.SignInWithBrowserAsync(cts.Token, browser, _settings.UiLanguage)");
            Equal(1, Regex.Matches(connect, @"_cloud\.SignInWithBrowserAsync\(").Count);
        });

        Add("Cloud UI: Cancel implicit close and unload all cancel even before the dialog opens", () =>
        {
            var connect = CloudUiMember(CloudUiSource("MainPage.Library.cs"), "ConnectCloudAsync");
            Check(connect.Contains("using var cts = new CancellationTokenSource();", StringComparison.Ordinal));
            Check(connect.Contains("waitDialog.CloseButtonClick += (_, _) => Abandon();", StringComparison.Ordinal));
            Check(connect.Contains("waitDialog.Closing += (_, _) => { if (!closingForResult) Abandon(); };", StringComparison.Ordinal));
            Check(connect.Contains("void UnloadSignIn(object sender, RoutedEventArgs args) => Abandon();", StringComparison.Ordinal));
            CloudUiBefore(connect, "Unloaded += UnloadSignIn;", "waitDialog.ShowAsync()");
            Check(connect.Contains("await Task.WhenAny(signInTask, dialogTask, abandonedSignal.Task);", StringComparison.Ordinal));
            Check(connect.Contains("if (dialogTask.IsCompleted || !IsLoaded) Abandon();", StringComparison.Ordinal));
            var abandon = connect[connect.IndexOf("void Abandon()", StringComparison.Ordinal)..connect.IndexOf("waitDialog.Opened +=", StringComparison.Ordinal)];
            CloudUiBefore(abandon, "abandoned = true;", "CancelRequest();");
            Check(abandon.Contains("abandonedSignal.TrySetResult(true);", StringComparison.Ordinal));
            CloudUiForbids(abandon, "closingForResult");
            Check(connect.Contains("try { cts.Cancel(); }", StringComparison.Ordinal));
        });

        Add("Cloud UI: teardown waits for both dialog and request before releasing busy state", () =>
        {
            var source = CloudUiSource("MainPage.Library.cs");
            var connect = CloudUiMember(source, "ConnectCloudAsync");
            var cleanup = connect[connect.IndexOf("\n        finally", StringComparison.Ordinal)..];
            CloudUiBefore(cleanup, "CancelRequest();", "waitDialog.Hide();");
            CloudUiBefore(cleanup, "waitDialog.Hide();", "await dialogTask;");
            CloudUiBefore(cleanup, "await dialogTask;", "result = await signInTask;");
            CloudUiBefore(cleanup, "result = await signInTask;", "Unloaded -= UnloadSignIn;");
            Check(cleanup.Contains("if (!IsLoaded) Abandon();", StringComparison.Ordinal));
            Check(cleanup.Contains("catch (OperationCanceledException) when (cts.IsCancellationRequested) { Abandon(); }", StringComparison.Ordinal));
            Check(cleanup.Contains("catch (Exception) { uiFailed = true; Abandon(); }", StringComparison.Ordinal));
            CloudUiForbids(connect, "SetStudyBusy(false)", "_dialogOpen = false", "_dialogClosed = null", "Task.Run(");
            var begin = CloudUiMember(source, "BeginGoogleSignInAsync");
            CloudUiBefore(begin, "await ConnectCloudAsync(browser);", "SetStudyBusy(false);");
            CloudUiBefore(begin, "RefreshCloudUi(focus);", "closed.TrySetResult(true);");
        });

        Add("Cloud UI: cancellation racing a successful commit signs out before reporting any result", () =>
        {
            var source = CloudUiSource("MainPage.Library.cs");
            var connect = CloudUiMember(source, "ConnectCloudAsync");
            CloudUiBefore(connect, "if (abandoned && result?.Success == true)", "await _cloud.SignOutAsync();");
            CloudUiBefore(connect, "await _cloud.SignOutAsync();", "if (abandoned || result is null) return new(false, null);");
            CloudUiBefore(connect, "if (abandoned || result is null) return new(false, null);", "return result.Success && _cloud.IsConnected");
            Check(connect.Contains("catch (Exception) { ShowCloudSignOutFailure(); return new(false, null); }", StringComparison.Ordinal));
            CloudUiForbids(connect, "InfoBarSeverity.Success");
            var begin = CloudUiMember(source, "BeginGoogleSignInAsync");
            Check(Regex.IsMatch(begin, @"else if \(result\.ErrorMessage is not null\)\s+ShowNotice\([^;]*CloudSignInFailureMessage\(\)"));
        });

        Add("Cloud UI: success and connected state require a verified persisted Firebase session", () =>
        {
            foreach (var sample in new (string? Uid, string? Token, bool Connected)[]
            {
                (null, null, false), ("test-uid", null, false), (null, "test-refresh", false),
                (" ", "test-refresh", false), ("test-uid", "\t", false), ("test-uid", "test-refresh", true),
            })
                Equal(sample.Connected, new CloudCredentials { FirebaseUid = sample.Uid, FirebaseRefreshToken = sample.Token }.HasFirebaseSession);
            var core = CloudUiSource("CloudSyncCoordinator.cs");
            Check(CloudUiMember(core, "IsConnected").Contains("_credentials.HasFirebaseSession", StringComparison.Ordinal));
            var signIn = CloudUiMember(core, "SignInWithBrowserAsync");
            CloudUiBefore(signIn, "await _browserSignIn(", "await _firebaseAuthService.SignInWithGoogleIdTokenAsync(");
            CloudUiBefore(signIn, "await _firebaseAuthService.SignInWithGoogleIdTokenAsync(", "await CommitCredentialsAsync(");
            Check(signIn.Contains("FirebaseRefreshToken = firebase.RefreshToken", StringComparison.Ordinal));
            Check(signIn.Contains("FirebaseUid = firebase.LocalId", StringComparison.Ordinal));
            var source = CloudUiSource("MainPage.Library.cs");
            Check(CloudUiMember(source, "ConnectCloudAsync").Contains("return result.Success && _cloud.IsConnected ? result : new(false, CloudSignInFailureMessage());", StringComparison.Ordinal));
            Check(Regex.IsMatch(CloudUiMember(source, "BeginGoogleSignInAsync"), @"if \(result\.Success && _cloud\.IsConnected\)\s+ShowNotice\([^;]*InfoBarSeverity\.Success"));
            CloudUiForbids(CloudUiRegion(source), "CloudConnected =", "new CloudResult(true", "new(true,", "_credentials =");
        });

        Add("Cloud UI: the sign-in modal preserves game feedback timer focus and completion barrier", () =>
        {
            var begin = CloudUiMember(CloudUiSource("MainPage.Library.cs"), "BeginGoogleSignInAsync");
            foreach (var text in new[] { "FocusManager.GetFocusedElement(XamlRoot)", "var page = _currentPage;", "var context = StudyContext;",
                "var timer = _gameTimer;", "var wasRunning = timer?.IsEnabled == true;", "var game = _activeGame;", "_dialogOpen = true;",
                "_dialogClosed = closed;", "timer?.Stop();", "_dialogOpen = false;", "_dialogClosed = null;", "closed.TrySetResult(true);" })
                Check(begin.Contains(text, StringComparison.Ordinal), "Missing modal lifetime contract: " + text);
            Check(begin.Contains("if (IsLoaded && wasRunning && !_storageBlocked && _activeGame == game && ReferenceEquals(timer, _gameTimer)) timer?.Start();", StringComparison.Ordinal));
            Check(begin.Contains("RestoreDialogFocus(focus, page, context);", StringComparison.Ordinal));
            Check(begin.Contains("TaskCreationOptions.RunContinuationsAsynchronously", StringComparison.Ordinal));
            CloudUiForbids(begin, "StopGameTimer()", "RenderCurrentPage()", "_activeGame = null");
            foreach (var pending in new[] { "_gameLocalBusy", "_gameRoundClosed", "_resolvingGame is not null", "_completingGame is not null" })
                CloudUiBefore(begin, pending, "_dialogOpen = true;");
            CloudUiBefore(begin, "_dialogClosed = closed;", "await ConnectCloudAsync(browser);");
        });

        Add("Cloud UI: refreshing status keeps games intact and a real session alone gets a green check", () =>
        {
            var source = CloudUiSource("MainPage.Library.cs");
            var refresh = CloudUiMember(source, "RefreshCloudUi");
            foreach (var guard in new[] { "IsLoaded", "!_dialogOpen", "_dialogClosed is null", "!_studyBusy", "!_navigationBusy", "!LoadingRing.IsActive", "_activeGame is null", "_currentPage is \"home\" or \"profile\"" })
                CloudUiBefore(refresh, guard, "RenderCurrentPage();");
            Check(Regex.IsMatch(refresh, @"finally\s*\{\s*SyncAccountStatus\(\);"));
            Check(refresh.Contains("RequestUiFocus(id);", StringComparison.Ordinal));
            Check(refresh.Contains("\"home.OtherBrowser\" when _cloud.IsConnected => \"home.AccountAction\"", StringComparison.Ordinal));
            Check(refresh.Contains("when !_cloud.IsConnected => \"profile.AccountAction\"", StringComparison.Ordinal));
            Check(refresh.Contains("LocalStatusButton.Focus(FocusState.Programmatic);", StringComparison.Ordinal));
            foreach (var member in new[] { "ConnectCloudAsync", "DisconnectCloudAsync" })
                CloudUiForbids(CloudUiMember(source, member), "RenderCurrentPage()");
            var xaml = XDocument.Parse(CloudUiSource("MainPage.xaml"));
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var toggle = xaml.Descendants().Single(e => (string?)e.Attribute(x + "Name") == "LocalStatusButton");
            var badge = toggle.Descendants().Single(e => (string?)e.Attribute(x + "Name") == "LocalStatusBadge");
            Check(badge.Descendants().Any(e => (string?)e.Attribute(x + "Name") == "LocalStatusIcon"));
            var status = CloudUiMember(source, "SyncAccountStatus");
            Check(status.Contains("var connected = _cloud.IsConnected;", StringComparison.Ordinal));
            Check(status.Contains("LocalStatusBadge.Background = connected ? new SolidColorBrush(Color.FromArgb(255, 16, 124, 16))", StringComparison.Ordinal));
            Check(status.Contains("LocalStatusIcon.Foreground = connected ? new SolidColorBrush(Microsoft.UI.Colors.White)", StringComparison.Ordinal));
            Check(status.Contains("LocalStatusIcon.Glyph = connected ? \"\\uE73E\"", StringComparison.Ordinal));
            Check(status.Contains("LocalStatusText.Foreground = palette.BoxForegroundBrush;", StringComparison.Ordinal));
            Check(status.Contains("LocalStatusText.FontSize = Math.Max(18, Font(18));", StringComparison.Ordinal));
            Check(status.Contains("UpdateCloudLiveText(LocalStatusText, label)", StringComparison.Ordinal));
            CloudUiForbids(status, "CloudConnected", "ErrorMessage", "LastGoogle");
        });

        Add("Cloud UI: sign-out failure copy is truthful and every cloud operation refreshes revocation", () =>
        {
            var source = CloudUiSource("MainPage.Library.cs");
            var signOut = CloudUiMember(source, "DisconnectCloudAsync");
            Check(signOut.Contains("await _cloud.SignOutAsync();", StringComparison.Ordinal));
            Check(signOut.Contains("catch (Exception) { ShowCloudSignOutFailure(); }", StringComparison.Ordinal));
            foreach (var method in new[] { "DisconnectCloudAsync", "CloudSaveAsync", "CloudLoadAsync", "CloudDeleteAsync" })
                Check(Regex.IsMatch(CloudUiMember(source, method), @"finally\s*\{\s*SetStudyBusy\(false\);\s*RefreshCloudUi\("), "Missing authoritative refresh: " + method);
            var failure = CloudUiMember(source, "ShowCloudSignOutFailure");
            Check(failure.Contains("Signed out here, but we could not save that change. Ask a grown-up to close the app and try again.", StringComparison.Ordinal));
            CloudUiForbids(failure, "still connected", "hâlâ bağlı", "ex.Message");
            var clear = CloudUiMember(CloudUiSource("CloudSyncCoordinator.cs"), "ClearSessionAsync");
            Check(clear.StartsWith("    private async Task ClearSessionAsync(", StringComparison.Ordinal));
            CloudUiBefore(clear, "_credentials = preserved;", "await _credentialStore.SaveAsync(preserved)");
            foreach (var method in new[] { "CloudSaveAsync", "CloudDeleteAsync" })
                CloudUiForbids(CloudUiMember(source, method), "result.ErrorMessage", "ex.Message");
        });

        Add("Cloud UI: every in-use cloud key has seven explicit translations including the new child-friendly copy", () =>
        {
            var keys = Regex.Matches(CloudUiRegion(CloudUiSource("MainPage.Library.cs")), "U\\(\"(Cloud\\.[^\"]+)\"")
                .Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
            Check(keys.Count > 0, "No cloud copy was audited.");
            var table = CloudUiSource("ExperienceStrings.cs");
            var rows = table.Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith("Cloud.", StringComparison.Ordinal))
                .Select(line => line.Split('|')).ToArray();
            var missing = keys.Except(rows.Select(row => row[0]), StringComparer.Ordinal).Order().ToArray();
            Check(missing.Length == 0, "Missing cloud translations (parent resource handoff): " + string.Join(", ", missing));
            foreach (var key in keys)
            {
                var matches = rows.Where(row => row[0] == key).ToArray();
                Equal(1, matches.Length);
                var row = matches[0];
                Check(row.Length == 8, "Expected key and seven translations: " + key);
                Check(row.Skip(1).All(value => !string.IsNullOrWhiteSpace(value) && !Regex.IsMatch(value, @"\b(?:TODO|FIXME)\b")), key);
                if (Regex.Matches(row[1], @"\p{L}+").Count >= 5)
                    Check(row.Skip(2).All(value => value != row[1]), "English fallback copied into: " + key);
            }
        });
    }

    private static string CloudUiRegion(string source)
    {
        const string start = "    private bool CloudUiUnavailable";
        const string end = "    private async Task ExportBackupAsync()";
        CloudUiBefore(source, start, end);
        return source[source.IndexOf(start, StringComparison.Ordinal)..source.IndexOf(end, StringComparison.Ordinal)];
    }

    private static void CloudUiBefore(string source, string first, string later)
    {
        var start = source.IndexOf(first, StringComparison.Ordinal);
        var end = source.IndexOf(later, StringComparison.Ordinal);
        Check(start >= 0 && end > start, "Expected source order: " + first + " before " + later);
    }

    private static void CloudUiForbids(string source, params string[] forbidden)
    {
        foreach (var text in forbidden)
            Check(!source.Contains(text, StringComparison.Ordinal), "Unexpected cloud UI path: " + text);
    }

    private static string CloudUiSource(string file)
    {
        foreach (var root in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
            {
                var path = Path.Combine(directory.FullName, "windows", "src", "YDKE.Windows", file);
                if (File.Exists(path)) return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
            }
        throw new InvalidOperationException("Run the source contracts from the YDKE repository or its test output folder.");
    }

    private static string CloudUiMember(string source, string member)
    {
        // Do not confuse an expression-bodied caller with the member's declaration.
        var match = Regex.Match(source, @"(?m)^    (?:private|public|internal) [^=;{}\r\n]*\b" + Regex.Escape(member) + @"\s*(?:\(|=>)");
        Check(match.Success, "Source member missing: " + member);
        var next = Regex.Match(source[(match.Index + match.Length)..], @"(?m)^    (?:private|public|internal) ");
        var end = next.Success ? match.Index + match.Length + next.Index : source.Length;
        return source[match.Index..end];
    }
}