using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Web.WebView2.Core;

namespace TopWords.Windows;

public partial class MainWindow : ChromeWindow
{
    private CoreWebView2Environment? _environment;

    /// <summary>
    /// Guards the one-way hand-off from splash to page content. Later navigations must
    /// not put the splash back — that would flash the launch screen on every page the
    /// user opens inside a multi-page site.
    /// </summary>
    private bool _contentRevealed;
    private bool _initializationInProgress;
    private bool _webConfigured;
    private bool _showingOfflineFallback;
    private bool _suspendInProgress;
    private bool _wasMinimized;
    private int _windowStateVersion;
    private bool _navigationInProgress;
    private bool _reloadInProgress;
    private bool _allowNextReload;
    private int _reloadPermitVersion;
    private bool _webGameActive;
    private ulong? _activeNavigationId;
    private double? _pendingScrollTop;
    private CancellationTokenSource? _navigationTimeout;

    public MainWindow()
    {
        InitializeComponent();

        Width = AppConfig.InitialWidth;
        Height = AppConfig.InitialHeight;
        MinWidth = AppConfig.MinWidth;
        MinHeight = AppConfig.MinHeight;

        // After the defaults, so a stored size wins and a missing/rejected one falls
        // back to them rather than to zero.
        WindowPlacement.Restore(this);

        AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown), handledEventsToo: true);
        StateChanged += OnWindowStateChanged;
        Loaded += OnLoaded;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        WindowPlacement.Save(this);
        base.OnClosing(e);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        StartSplashAnimations();
        await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        if (_initializationInProgress)
        {
            return;
        }

        _initializationInProgress = true;
        PrepareStartupAttempt();

        try
        {
            _environment ??= await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: AppConfig.UserDataFolder);

            await Web.EnsureCoreWebView2Async(_environment);

            if (!_webConfigured)
            {
                await ConfigureAsync(Web.CoreWebView2);
                _webConfigured = true;
            }

            SplashStatus.Text = "İçerik yükleniyor… · Loading content…";
            Web.CoreWebView2.Navigate(AppConfig.StartUrl);
        }
        catch (Exception ex)
        {
            ShowStartupError(ex);
        }
        finally
        {
            _initializationInProgress = false;
        }
    }

    private async Task ConfigureAsync(CoreWebView2 core)
    {
        await core.AddScriptToExecuteOnDocumentCreatedAsync(EmbeddedResources.Bootstrap.Value);
                await core.AddScriptToExecuteOnDocumentCreatedAsync(
                        $$"""
                            (function () {
                                if (location.origin !== "{{AppConfig.AppOrigin}}") return;
                                window.addEventListener("keydown", function (event) {
                                    if (event.key !== "F6") return;
                                    event.preventDefault();
                                    event.stopImmediatePropagation();
                                    window.chrome.webview.postMessage("topwords:focus-chrome");
                                }, true);

                                function watchPlayState() {
                                    if (!document.body) return;
                                    var previous = null;
                                    function publish() {
                                        var playing = document.body.classList.contains("is-playing");
                                        if (playing === previous) return;
                                        previous = playing;
                                        window.chrome.webview.postMessage(
                                            "topwords:play-state:" + (playing ? "on" : "off"));
                                    }
                                    new MutationObserver(publish).observe(document.body, {
                                        attributes: true,
                                        attributeFilter: ["class"]
                                    });
                                    publish();
                                }

                                if (document.readyState === "loading") {
                                    document.addEventListener("DOMContentLoaded", watchPlayState, { once: true });
                                } else {
                                    watchPlayState();
                                }
                            })();
                            """);

        var settings = core.Settings;

        settings.AreDefaultContextMenusEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsBuiltInErrorPageEnabled = false;
        settings.IsWebMessageEnabled = true;
        settings.AreDevToolsEnabled = AppConfig.DevToolsEnabled;
        settings.IsSwipeNavigationEnabled = false;
        settings.IsZoomControlEnabled = true;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.UserAgent = $"{settings.UserAgent} {AppConfig.UserAgentSuffix}";

        // W4 — ad networks never leave the machine.
        foreach (var pattern in AppConfig.BlockedResourcePatterns)
        {
            core.AddWebResourceRequestedFilter(pattern, CoreWebView2WebResourceContext.All);
        }

        core.WebResourceRequested += OnWebResourceRequested;
        core.WebMessageReceived += OnWebMessageReceived;

        core.NavigationStarting += OnNavigationStarting;
        core.NavigationCompleted += OnNavigationCompleted;
        core.NewWindowRequested += OnNewWindowRequested;
        core.HistoryChanged += OnHistoryChanged;

        UpdateNavigationState();
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!Uri.TryCreate(e.Source, UriKind.Absolute, out var source) ||
            !Uri.TryCreate(AppConfig.AppOrigin, UriKind.Absolute, out var appOrigin) ||
            !string.Equals(
                source.GetLeftPart(UriPartial.Authority),
                appOrigin.GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        switch (e.WebMessageAsJson)
        {
            case "\"topwords:focus-chrome\"":
                HomeButton.Focus();
                break;
            case "\"topwords:play-state:on\"":
                _webGameActive = true;
                UpdateNavigationState();
                break;
            case "\"topwords:play-state:off\"":
                _webGameActive = false;
                UpdateNavigationState();
                break;
        }
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        // Only blocked patterns reach this handler, so everything here is denied.
        if (_environment is null)
        {
            return;
        }

        e.Response = _environment.CreateWebResourceResponse(
            Content: null,
            StatusCode: 204,
            ReasonPhrase: "No Content",
            Headers: string.Empty);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (AppConfig.IsAllowedInApp(e.Uri))
        {
            if (e.NavigationKind == CoreWebView2NavigationKind.Reload)
            {
                if (_webGameActive)
                {
                    _allowNextReload = false;
                    _pendingScrollTop = null;
                    e.Cancel = true;
                    UpdateNavigationState();
                    return;
                }

                if (!_allowNextReload)
                {
                    e.Cancel = true;
                    RequestReload();
                    return;
                }
            }

            _allowNextReload = false;

            if (e.NavigationKind == CoreWebView2NavigationKind.NewDocument)
            {
                _webGameActive = false;
            }

            BeginNavigation(e.NavigationId);

            // Suppressed until the splash has gone: during first load the splash is
            // already saying the same thing, and two indicators for one wait is noise.
            if (_contentRevealed)
            {
                Progress.Visibility = Visibility.Visible;
            }

            return;
        }

        // Anything outside the app's own origin and the sign-in hosts opens in the
        // same-host restricted viewer, never in this window.
        e.Cancel = true;
        OpenExternalLink(e.Uri);
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_activeNavigationId != e.NavigationId)
        {
            return;
        }

        _navigationTimeout?.Cancel();
        _navigationTimeout?.Dispose();
        _navigationTimeout = null;
        _activeNavigationId = null;
        _navigationInProgress = false;
        Progress.Visibility = Visibility.Collapsed;
        UpdateNavigationState();

        if (e.IsSuccess || e.WebErrorStatus == CoreWebView2WebErrorStatus.OperationCanceled)
        {
            _showingOfflineFallback = false;
            RevealContent();
            RestorePendingScroll();
            return;
        }

        if (_showingOfflineFallback)
        {
            RevealContent();
            return;
        }

        _showingOfflineFallback = true;
        SplashStatus.Text = "Çevrimdışı içerik hazırlanıyor… · Preparing offline content…";
        Web.CoreWebView2.NavigateToString(EmbeddedResources.OfflineHtml.Value);
    }

    private void BeginNavigation(ulong navigationId)
    {
        _navigationTimeout?.Cancel();
        _navigationTimeout?.Dispose();
        _navigationTimeout = new CancellationTokenSource();
        _activeNavigationId = navigationId;
        _navigationInProgress = true;
        UpdateNavigationState();
        _ = WatchNavigationAsync(navigationId, _navigationTimeout.Token);
    }

    private async Task WatchNavigationAsync(ulong navigationId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_activeNavigationId != navigationId || Web.CoreWebView2 is not { } core)
        {
            return;
        }

        try
        {
            core.Stop();
            _showingOfflineFallback = true;
            core.NavigateToString(EmbeddedResources.OfflineHtml.Value);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Navigation timeout recovery failed: {ex.Message}");
            _navigationInProgress = false;
            Progress.Visibility = Visibility.Collapsed;
            UpdateNavigationState();
        }
    }

    private void RequestReload()
    {
        var core = Web.CoreWebView2;

        if (core is null || core.IsSuspended || _webGameActive || _reloadInProgress || _navigationInProgress)
        {
            return;
        }

        _reloadInProgress = true;
        UpdateNavigationState();
        Dispatcher.BeginInvoke(new Action(() => _ = ReloadPreservingScrollAsync(core)));
    }

    private async Task ReloadPreservingScrollAsync(CoreWebView2 core)
    {
        var source = core.Source;
        double? scrollTop = null;

        try
        {
            try
            {
                var value = await core.ExecuteScriptAsync(
                        "(function(){var e=document.querySelector('.container');return e?e.scrollTop:0;})()")
                    .WaitAsync(TimeSpan.FromSeconds(1));

                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var captured))
                {
                    scrollTop = Math.Max(0, captured);
                }
            }
            catch (Exception ex) when (ex is TimeoutException or InvalidOperationException)
            {
                Debug.WriteLine($"Scroll snapshot skipped: {ex.Message}");
            }

            if (_webGameActive || _navigationInProgress || source != core.Source)
            {
                return;
            }

            _pendingScrollTop = scrollTop;
            _allowNextReload = true;
            var permitVersion = ++_reloadPermitVersion;
            core.Reload();
            _ = ExpireReloadPermitAsync(permitVersion);
        }
        catch (Exception ex)
        {
            _allowNextReload = false;
            Debug.WriteLine($"Reload skipped: {ex.Message}");
        }
        finally
        {
            _reloadInProgress = false;
            UpdateNavigationState();
        }
    }

    private async Task ExpireReloadPermitAsync(int permitVersion)
    {
        await Task.Delay(TimeSpan.FromSeconds(2));

        if (_allowNextReload && _reloadPermitVersion == permitVersion)
        {
            _allowNextReload = false;
            _pendingScrollTop = null;
            UpdateNavigationState();
        }
    }

    private void RestorePendingScroll()
    {
        if (_pendingScrollTop is not { } scrollTop || scrollTop <= 0 || Web.CoreWebView2 is not { } core)
        {
            _pendingScrollTop = null;
            return;
        }

        _pendingScrollTop = null;
        var target = scrollTop.ToString(CultureInfo.InvariantCulture);
        _ = core.ExecuteScriptAsync(
            $$"""
              (function () {
                var target = {{target}};
                var surface = document.querySelector(".container");
                if (!surface || target <= 0) return;
                var observer;
                var frame = 0;
                var stopped = false;
                var events = ["wheel", "touchstart", "pointerdown", "keydown"];
                function stop() {
                  if (stopped) return;
                  stopped = true;
                  if (observer) observer.disconnect();
                  if (frame) cancelAnimationFrame(frame);
                  events.forEach(function (name) {
                    surface.removeEventListener(name, stop, true);
                  });
                }
                function apply() {
                  frame = 0;
                  if (stopped) return;
                  var max = Math.max(0, surface.scrollHeight - surface.clientHeight);
                  surface.scrollTop = Math.min(target, max);
                  if (max + 1 >= target) stop();
                }
                function schedule() {
                  if (!frame && !stopped) frame = requestAnimationFrame(apply);
                }
                events.forEach(function (name) {
                  surface.addEventListener(name, stop, true);
                });
                observer = new MutationObserver(schedule);
                observer.observe(surface, { childList: true, subtree: true });
                schedule();
                setTimeout(stop, 3000);
              })();
              """);
    }

    private void OnHistoryChanged(object? sender, object e) => UpdateNavigationState();

    /// <summary>
    /// Fades the launch screen out and hands the content area over to the page.
    /// <para>
    /// The order matters. A hosted child HWND paints over WPF content unconditionally,
    /// so showing the WebView first would clip the fade away on its first frame. Fading
    /// to the surface colour and only then revealing reads as one continuous surface,
    /// because the splash background, the window background and the WebView's
    /// <c>DefaultBackgroundColor</c> are all <c>#0F172A</c>.
    /// </para>
    /// </summary>
    private void RevealContent()
    {
        if (_contentRevealed)
        {
            return;
        }

        _contentRevealed = true;
        StopSplashAnimations();

        if (!SystemParameters.ClientAreaAnimation)
        {
            CompleteReveal();
            return;
        }

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        fade.Completed += (_, _) => CompleteReveal();

        Splash.BeginAnimation(OpacityProperty, fade);
    }

    private void CompleteReveal()
    {
        Splash.Visibility = Visibility.Collapsed;

        if (WindowState == WindowState.Minimized)
        {
            Web.Visibility = Visibility.Hidden;
            _ = SuspendWebViewAsync(_windowStateVersion);
            return;
        }

        Web.Visibility = Visibility.Visible;
        Web.Focus();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        var version = ++_windowStateVersion;

        if (WindowState == WindowState.Minimized)
        {
            _wasMinimized = true;
            Web.Visibility = Visibility.Hidden;
            _ = SuspendWebViewAsync(version);
            return;
        }

        if (_wasMinimized)
        {
            _wasMinimized = false;
            ResumeWebView();
        }
    }

    private async Task SuspendWebViewAsync(int version)
    {
        var core = Web.CoreWebView2;

        if (core is null || _suspendInProgress || core.IsSuspended)
        {
            return;
        }

        _suspendInProgress = true;

        try
        {
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);

            if (version != _windowStateVersion || WindowState != WindowState.Minimized)
            {
                return;
            }

            await core.TrySuspendAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebView2 suspension skipped: {ex.Message}");
        }
        finally
        {
            _suspendInProgress = false;

            if (WindowState != WindowState.Minimized)
            {
                ResumeWebView();
            }
            else if (version != _windowStateVersion)
            {
                _ = SuspendWebViewAsync(_windowStateVersion);
            }
        }
    }

    private void ResumeWebView()
    {
        var core = Web.CoreWebView2;

        if (core?.IsSuspended == true)
        {
            core.Resume();
        }

        if (_contentRevealed)
        {
            Web.Visibility = Visibility.Visible;
            Web.Focus();
        }
    }

    private void PrepareStartupAttempt()
    {
        SplashStatus.Text = "WebView2 hazırlanıyor… · Preparing WebView2…";
        SplashErrorDetails.Visibility = Visibility.Collapsed;
        SplashActions.Visibility = Visibility.Collapsed;
        SplashTrack.Visibility = Visibility.Visible;
        StartSplashAnimations();
    }

    private void ShowStartupError(Exception ex)
    {
        StopSplashAnimations();
        SplashTrack.Visibility = Visibility.Collapsed;
        SplashStatus.Text = "Uygulama başlatılamadı · The app could not start";
        SplashErrorDetails.Text = ex.Message;
        SplashErrorDetails.Visibility = Visibility.Visible;
        SplashActions.Visibility = Visibility.Visible;
    }

    private void StartSplashAnimations()
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            SplashLogoScale.ScaleX = 1;
            SplashLogoScale.ScaleY = 1;
            SplashSweep.X = 55;
            return;
        }

        SplashSweep.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(-78, 180, TimeSpan.FromSeconds(1.2))
            {
                RepeatBehavior = RepeatBehavior.Forever,
            });

        var scale = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(550))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        SplashLogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        SplashLogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
    }

    private void StopSplashAnimations()
    {
        SplashSweep.BeginAnimation(TranslateTransform.XProperty, null);
        SplashLogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        SplashLogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        SplashLogoScale.ScaleX = 1;
        SplashLogoScale.ScaleY = 1;
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e) =>
        await InitializeWebViewAsync();

    private void OnInstallRuntimeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                AppConfig.WindowTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F6 || !_contentRevealed)
        {
            return;
        }

        e.Handled = true;

        if (Web.IsKeyboardFocusWithin)
        {
            HomeButton.Focus();
            return;
        }

        Web.Focus();
    }

    /// <summary>
    /// Keeps the history buttons honest. Back and forward spend most of their life
    /// unavailable, and a control that looks live but does nothing is worse than one
    /// that is visibly disabled.
    /// </summary>
    private void UpdateNavigationState()
    {
        var core = Web.CoreWebView2;

        BackButton.IsEnabled = !_navigationInProgress && core is { CanGoBack: true };
        ForwardButton.IsEnabled = !_navigationInProgress && core is { CanGoForward: true };
        ReloadButton.IsEnabled = core is not null &&
            !_navigationInProgress &&
            !_reloadInProgress &&
            !_webGameActive;
        HomeButton.IsEnabled = core is not null && !_navigationInProgress;
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2 is { CanGoBack: true } core)
        {
            core.GoBack();
        }
    }

    private void OnForwardClick(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2 is { CanGoForward: true } core)
        {
            core.GoForward();
        }
    }

    private void OnReloadClick(object sender, RoutedEventArgs e) => RequestReload();

    private void OnHomeClick(object sender, RoutedEventArgs e) =>
        Web.CoreWebView2?.Navigate(AppConfig.StartUrl);

    private void OnMinimizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// <b>W3</b> — the reason this host exists rather than a PWABuilder hosted-web-app
    /// package. Firebase's <c>signInWithPopup</c> needs a genuine popup that keeps
    /// <c>window.opener</c> wired up; handing WebView2 an owned window satisfies that,
    /// so the udsp repo does not have to migrate to <c>signInWithRedirect</c>.
    /// </summary>
    private async void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (!AppConfig.IsAuthUrl(e.Uri))
        {
            e.Handled = true;
            OpenExternalLink(e.Uri);
            return;
        }

        var deferral = e.GetDeferral();
        PopupWindow? popup = null;

        try
        {
            popup = new PopupWindow { Owner = this };

            // The window has to be on screen first: the WPF WebView2 control does not
            // begin creating its CoreWebView2 until the control is loaded, so awaiting
            // initialisation on an unshown window never returns.
            popup.Show();
            await popup.InitializeAsync(_environment!);

            e.NewWindow = popup.Core!;
            e.Handled = true;
        }
        catch (Exception)
        {
            popup?.Close();
            e.Handled = true;
            OpenExternalLink(e.Uri);
        }
        finally
        {
            deferral.Complete();
        }
    }

    /// <summary>
    /// Opens a link that points outside the app in the same-host restricted
    /// <see cref="ExternalViewerWindow"/>.
    /// </summary>
    internal static void OpenExternalLink(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return;
        }

        // Never act on an arbitrary scheme — that would let page content invoke
        // registered protocol handlers on the user's machine.
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            return;
        }

        if (Application.Current?.MainWindow is MainWindow { _environment: not null } host)
        {
            _ = host.ShowExternalViewerAsync(parsed);
            return;
        }

        // The viewer needs the shared WebView2 environment. If the main window is
        // gone the link is dropped rather than silently escaping to the browser,
        // which would contradict the containment this window exists to provide.
        Debug.WriteLine($"External link dropped, no host window available: {parsed.Host}");
    }

    private async Task ShowExternalViewerAsync(Uri target)
    {
        var viewer = new ExternalViewerWindow(target) { Owner = this };

        try
        {
            // Shown before initialising for the same reason as the sign-in popup: an
            // unshown WebView2 never finishes creating its CoreWebView2. It also means
            // the window appears immediately on click rather than after the page loads.
            viewer.Show();
            await viewer.InitializeAsync(_environment!);
        }
        catch (Exception ex)
        {
            viewer.Close();

            MessageBox.Show(
                this,
                $"Bağlantı açılamadı.\n\n{target.Host}\n\n{ex.Message}",
                AppConfig.WindowTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
