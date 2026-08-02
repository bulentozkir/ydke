using System.Diagnostics;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace TopWords.Windows;

public partial class MainWindow : Window
{
    private CoreWebView2Environment? _environment;

    public MainWindow()
    {
        InitializeComponent();

        Width = AppConfig.InitialWidth;
        Height = AppConfig.InitialHeight;
        MinWidth = AppConfig.MinWidth;
        MinHeight = AppConfig.MinHeight;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        try
        {
            _environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: AppConfig.UserDataFolder);

            await Web.EnsureCoreWebView2Async(_environment);

            Configure(Web.CoreWebView2);
            Web.CoreWebView2.Navigate(AppConfig.StartUrl);
        }
        catch (Exception ex)
        {
            Splash.Text = "WebView2 başlatılamadı.";
            MessageBox.Show(
                $"WebView2 could not be initialised.\n\n{ex.Message}\n\n" +
                "Install the WebView2 Evergreen Runtime from https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                AppConfig.WindowTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Configure(CoreWebView2 core)
    {
        var settings = core.Settings;

        settings.AreDefaultContextMenusEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.AreDevToolsEnabled = AppConfig.DevToolsEnabled;
        settings.IsSwipeNavigationEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.UserAgent = $"{settings.UserAgent} {AppConfig.UserAgentSuffix}";

        // W4 — ad networks never leave the machine.
        foreach (var pattern in AppConfig.BlockedResourcePatterns)
        {
            core.AddWebResourceRequestedFilter(pattern, CoreWebView2WebResourceContext.All);
        }

        core.WebResourceRequested += OnWebResourceRequested;

        // W4 / W5 / W7 — host-side fixes, injected before any page script runs.
        _ = core.AddScriptToExecuteOnDocumentCreatedAsync(EmbeddedResources.Bootstrap.Value);

        core.NavigationStarting += OnNavigationStarting;
        core.NavigationCompleted += OnNavigationCompleted;
        core.NewWindowRequested += OnNewWindowRequested;
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
            return;
        }

        // Anything outside the app's own origin and the sign-in hosts opens in the
        // read-only viewer, never in this window.
        e.Cancel = true;
        OpenExternalLink(e.Uri);
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        Splash.Visibility = Visibility.Collapsed;

        if (e.IsSuccess || e.WebErrorStatus == CoreWebView2WebErrorStatus.OperationCanceled)
        {
            return;
        }

        Web.CoreWebView2.NavigateToString(EmbeddedResources.OfflineHtml.Value);
    }

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
    /// Opens a link that points outside the app in the read-only
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
