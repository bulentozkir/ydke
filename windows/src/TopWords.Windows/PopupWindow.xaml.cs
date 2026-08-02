using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace TopWords.Windows;

/// <summary>
/// Host-owned popup used for the Google sign-in flow (<b>W3</b>).
/// <para>
/// WebView2 populates this window's <see cref="CoreWebView2"/> itself when it is
/// assigned to <c>NewWindowRequestedEventArgs.NewWindow</c>, which preserves the
/// <c>window.opener</c> relationship Firebase's <c>postMessage</c> handshake needs.
/// </para>
/// </summary>
public partial class PopupWindow : Window
{
    public PopupWindow()
    {
        InitializeComponent();

        Width = AppConfig.PopupWidth;
        Height = AppConfig.PopupHeight;
    }

    internal CoreWebView2? Core => Web.CoreWebView2;

    internal async Task InitializeAsync(CoreWebView2Environment environment)
    {
        await Web.EnsureCoreWebView2Async(environment);

        var core = Web.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreDevToolsEnabled = AppConfig.DevToolsEnabled;

        // Firebase closes the popup itself once the handshake completes.
        core.WindowCloseRequested += (_, _) => Close();

        // A popup must never spawn further popups inside the app.
        core.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            MainWindow.OpenExternalLink(args.Uri);
        };
    }
}
