using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace TopWords.Windows;

/// <summary>
/// Restricted viewer for links that point outside the app.
/// <para>
/// External destinations used to be handed to the user's default browser. They now
/// stay inside the app without an editable address bar, context menu, or accelerator
/// keys. Navigation remains on the host reached by the initial URL, which lets course
/// lesson links work without turning this window into a general-purpose browser.
/// </para>
/// <para>
/// <b>The title bar shows the host on purpose.</b> A chromeless window rendering
/// arbitrary remote content with no visible origin is a phishing surface — the user
/// would have no way to tell a bank's real login page from a copy of it. The host
/// label is the cheapest defence that does not reintroduce a toolbar, and unlike an
/// address bar it cannot be typed into.
/// </para>
/// </summary>
public partial class ExternalViewerWindow : ChromeWindow
{
    private readonly Uri _target;
    private bool _initialNavigationStarted;
    private bool _initialNavigationCompleted;
    private string _allowedHost;

    internal ExternalViewerWindow(Uri target)
    {
        InitializeComponent();

        _target = target;
        _allowedHost = target.Host;

        Width = AppConfig.ExternalViewerWidth;
        Height = AppConfig.ExternalViewerHeight;
        Title = target.Host;
    }

    internal async Task InitializeAsync(CoreWebView2Environment environment)
    {
        await Web.EnsureCoreWebView2Async(environment);

        var core = Web.CoreWebView2;
        var settings = core.Settings;

        // Remove the chrome itself...
        settings.AreDefaultContextMenusEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;

        // ...and the keyboard routes back to it. Without this, F5, Ctrl+R, Ctrl+P and
        // Alt+Left still work even though nothing on screen advertises them.
        settings.AreBrowserAcceleratorKeysEnabled = false;

        // Untrusted content: DevTools stay off even in Debug builds.
        settings.AreDevToolsEnabled = false;

        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsSwipeNavigationEnabled = false;

        core.WindowCloseRequested += (_, _) => Close();
        core.SourceChanged += (_, _) => UpdateTitle(core.Source);
        core.NavigationStarting += OnNavigationStarting;
        core.NavigationCompleted += OnNavigationCompleted;
        core.HistoryChanged += (_, _) => UpdateNavigationState(core);

        core.NewWindowRequested += OnNewWindowRequested;

        core.Navigate(_target.AbsoluteUri);
        UpdateNavigationState(core);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // The first navigation is the one we were opened for.
        if (!_initialNavigationStarted)
        {
            _initialNavigationStarted = true;
            return;
        }

        // Redirects are part of arriving at the initial document. Once it has loaded,
        // every navigation must remain on the established course host.
        if (!_initialNavigationCompleted && e.IsRedirected)
        {
            return;
        }

        e.Cancel = !IsAllowedNavigation(e.Uri);
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!_initialNavigationCompleted)
        {
            _initialNavigationCompleted = true;

            if (e.IsSuccess && Uri.TryCreate(Web.CoreWebView2.Source, UriKind.Absolute, out var source))
            {
                _allowedHost = source.Host;
            }
        }

        UpdateNavigationState(Web.CoreWebView2);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;

        if (IsAllowedNavigation(e.Uri))
        {
            Web.CoreWebView2.Navigate(e.Uri);
        }
    }

    private bool IsAllowedNavigation(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed) &&
        (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps) &&
        string.Equals(parsed.Host, _allowedHost, StringComparison.OrdinalIgnoreCase);

    private void UpdateNavigationState(CoreWebView2 core) =>
        BackButton.IsEnabled = core.CanGoBack;

    private void UpdateTitle(string uri) =>
        Title = Uri.TryCreate(uri, UriKind.Absolute, out var parsed) ? parsed.Host : _target.Host;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2 is { CanGoBack: true } core)
        {
            core.GoBack();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
