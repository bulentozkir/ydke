using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace TopWords.Windows;

/// <summary>
/// Read-only viewer for links that point outside the app.
/// <para>
/// External destinations used to be handed to the user's default browser. They now
/// stay inside the app in a window that deliberately offers no way to browse: no
/// toolbar, no address bar, no context menu, no accelerator keys, and no further
/// navigation once the target document has loaded. The only affordances are closing
/// the window and reading what is on it.
/// </para>
/// <para>
/// <b>The title bar shows the host on purpose.</b> A chromeless window rendering
/// arbitrary remote content with no visible origin is a phishing surface — the user
/// would have no way to tell a bank's real login page from a copy of it. The host
/// label is the cheapest defence that does not reintroduce a toolbar, and unlike an
/// address bar it cannot be typed into.
/// </para>
/// </summary>
public partial class ExternalViewerWindow : Window
{
    private readonly Uri _target;
    private bool _initialNavigationStarted;

    internal ExternalViewerWindow(Uri target)
    {
        InitializeComponent();

        _target = target;

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

        // A viewer that could spawn viewers would be a browser again.
        core.NewWindowRequested += (_, args) => args.Handled = true;

        core.Navigate(_target.AbsoluteUri);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // The first navigation is the one we were opened for.
        if (!_initialNavigationStarted)
        {
            _initialNavigationStarted = true;
            return;
        }

        // Redirects are part of arriving at that document, not navigation away from
        // it. Blocking them would break every shortened, consent-gated or
        // http-to-https link.
        if (e.IsRedirected)
        {
            return;
        }

        e.Cancel = true;
    }

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
}
