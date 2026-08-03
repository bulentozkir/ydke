using System.IO;
using System.Text.Json;
using System.Windows;

namespace TopWords.Windows;

/// <summary>
/// Remembers where the main window was and puts it back on the next launch.
/// <para>
/// An app that reopens centred at its default size every time is the classic tell of
/// a wrapped web page. The state lives beside the WebView2 profile, so the packaged
/// build writes it inside the MSIX package-local path and uninstall removes it with
/// everything else — see <see cref="AppConfig.UserDataFolder"/>.
/// </para>
/// <para>
/// Every failure is swallowed. Window placement is a convenience, and a corrupt or
/// unreadable state file must never be the reason the app will not start.
/// </para>
/// </summary>
internal static class WindowPlacement
{
    private sealed record Placement(double Left, double Top, double Width, double Height, bool Maximized);

    private static string FilePath => Path.Combine(AppConfig.StateFolder, "window.json");

    internal static void Restore(Window window)
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return;
            }

            var saved = JsonSerializer.Deserialize<Placement>(File.ReadAllText(FilePath));

            if (saved is null || !IsUsable(saved, window))
            {
                return;
            }

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = saved.Left;
            window.Top = saved.Top;
            window.Width = saved.Width;
            window.Height = saved.Height;

            if (saved.Maximized)
            {
                window.WindowState = WindowState.Maximized;
            }
        }
        catch (Exception)
        {
            // Unreadable state is the same as no state.
        }
    }

    internal static void Save(Window window)
    {
        try
        {
            // RestoreBounds carries the pre-maximise rectangle, which is what should
            // be restored when the user un-maximises later. Reading Left/Top/Width/
            // Height while maximised would persist the monitor rectangle instead and
            // the window could never be restored to its old size.
            var bounds = window.WindowState == WindowState.Normal
                ? new Rect(window.Left, window.Top, window.Width, window.Height)
                : window.RestoreBounds;

            if (bounds.IsEmpty || double.IsNaN(bounds.Width) || double.IsNaN(bounds.Height))
            {
                return;
            }

            var placement = new Placement(
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,

                // Minimised is deliberately not persisted: an app that starts
                // minimised looks like it failed to launch.
                window.WindowState == WindowState.Maximized);

            Directory.CreateDirectory(AppConfig.StateFolder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(placement));
        }
        catch (Exception)
        {
            // Losing the placement is not worth interrupting shutdown.
        }
    }

    /// <summary>
    /// Rejects saved rectangles that would put the window somewhere the user cannot
    /// reach it — the usual cause being a monitor that has since been unplugged.
    /// </summary>
    private static bool IsUsable(Placement placement, Window window)
    {
        if (placement.Width < window.MinWidth || placement.Height < window.MinHeight)
        {
            return false;
        }

        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        var saved = new Rect(placement.Left, placement.Top, placement.Width, placement.Height);

        // A sliver on screen is not enough to grab: require the caption strip to be
        // genuinely visible before trusting the stored position.
        var caption = new Rect(saved.Left, saved.Top, saved.Width, 40);

        return virtualScreen.IntersectsWith(caption);
    }
}
