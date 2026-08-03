using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace TopWords.Windows;

/// <summary>
/// Base window for every surface in the app: replaces the system title bar with one
/// the app draws itself, and tells DWM to render the surrounding frame dark.
/// <para>
/// <b>Why this exists.</b> The three windows previously used the stock Win32 caption.
/// On a <c>#0F172A</c> app that produced a light title bar sitting on top of a dark
/// document — the single most visible sign that a web app has been wrapped rather
/// than built. Drawing the caption in WPF lets the chrome share the page's palette
/// and carry the navigation affordances a wrapped multi-page site actually needs.
/// </para>
/// <para>
/// Every P/Invoke here is advisory. Windows 10 builds that predate a given DWM
/// attribute return a failure HRESULT which is discarded, so the window still opens —
/// it just keeps the default frame.
/// </para>
/// </summary>
public abstract class ChromeWindow : Window
{
    /// <summary>Height of the drawn caption. Must match <c>CaptionHeight</c> in Theme.xaml.</summary>
    private const double CaptionHeightPx = 40;

    /// <summary>
    /// Padding a maximised window must apply to its own content so nothing spills past
    /// the work area. Zero at every other window state.
    /// <para>
    /// Windows deliberately makes a maximised window larger than the work area by the
    /// non-client frame, because on an ordinary window that frame is the part that
    /// hangs off screen. <see cref="WindowChrome"/> converts the frame into client
    /// area, so without this inset the caption buttons, the border and the last rows of
    /// the page are drawn off screen and under the taskbar instead.
    /// </para>
    /// </summary>
    public static readonly DependencyProperty ChromeInsetProperty = DependencyProperty.Register(
        nameof(ChromeInset),
        typeof(Thickness),
        typeof(ChromeWindow),
        new PropertyMetadata(default(Thickness)));

    public Thickness ChromeInset
    {
        get => (Thickness)GetValue(ChromeInsetProperty);
        private set => SetValue(ChromeInsetProperty, value);
    }

    protected ChromeWindow()
    {
        WindowStyle = WindowStyle.SingleBorderWindow;
        AllowsTransparency = false;
        Background = System.Windows.Media.Brushes.Transparent;

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = CaptionHeightPx,
            ResizeBorderThickness = new Thickness(6),
            CornerRadius = new CornerRadius(0),

            // A zero-thickness glass frame also removes the drop shadow and the
            // Windows 11 rounded corner, because DWM stops treating the window as
            // having a frame at all. One pixel along the bottom keeps both while
            // staying invisible against the caption.
            GlassFrameThickness = new Thickness(0, 0, 0, 1),
            UseAeroCaptionButtons = false,
        });

        SourceInitialized += OnSourceInitialized;
        StateChanged += (_, _) => UpdateChromeInset();
    }

    /// <summary>Dragging a maximised window to a monitor with a different scale changes the frame.</summary>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdateChromeInset();
    }

    private void UpdateChromeInset()
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        if (WindowState != WindowState.Maximized || hwnd == IntPtr.Zero)
        {
            ChromeInset = default;
            return;
        }

        // The frame metrics are physical pixels; WPF lays out in device-independent
        // units, so a hard-coded value would be wrong on every scaled display.
        var dpi = VisualTreeHelper.GetDpi(this);
        var horizontal = NativeChrome.HorizontalFrame(hwnd) / dpi.DpiScaleX;
        var vertical = NativeChrome.VerticalFrame(hwnd) / dpi.DpiScaleY;

        ChromeInset = new Thickness(horizontal, vertical, horizontal, vertical);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        SourceInitialized -= OnSourceInitialized;

        var helper = new WindowInteropHelper(this);
        var hwnd = helper.Handle;

        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeChrome.ApplyDarkFrame(hwnd);
        HwndSource.FromHwnd(hwnd)?.AddHook(WindowProc);
    }

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeChrome.WmGetMinMaxInfo)
        {
            NativeChrome.ClampMaximizeToWorkArea(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Toggles between maximised and restored. Bound to the caption's maximise button.
    /// <para>
    /// Dragging, double-click-to-maximise and the right-click system menu are
    /// deliberately <i>not</i> reimplemented here: <see cref="WindowChrome"/> reports
    /// the caption strip as non-client area, so <c>DefWindowProc</c> already provides
    /// all three with the correct snap, shake and Aero Snap behaviour. Handling them
    /// manually with <c>DragMove</c> would replace working system behaviour with a
    /// worse copy.
    /// </para>
    /// </summary>
    protected void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}

/// <summary>Win32 and DWM calls needed to make a WPF window look native on Windows 10 and 11.</summary>
internal static class NativeChrome
{
    internal const int WmGetMinMaxInfo = 0x0024;

    // Build 22000 is Windows 11 RTM; 19041 is Windows 10 2004, the first release
    // where DWMWA_USE_IMMERSIVE_DARK_MODE settled on attribute 20.
    private const int Windows11Build = 22000;
    private const int DarkModeStableBuild = 19041;

    private const int DwmwaUseImmersiveDarkModeLegacy = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;

    private const int DwmwcpRound = 2;

    /// <summary>Slate-800 (#1E293B) as a COLORREF (0x00BBGGRR).</summary>
    private const int BorderColorRef = 0x003B291E;

    private const int MonitorDefaultToNearest = 0x00000002;

    private const int SmCxFrame = 32;
    private const int SmCyFrame = 33;
    private const int SmCxPaddedBorder = 92;

    internal static void ApplyDarkFrame(IntPtr hwnd)
    {
        var build = Environment.OSVersion.Version.Build;

        var enabled = 1;
        TrySet(hwnd, build >= DarkModeStableBuild ? DwmwaUseImmersiveDarkMode : DwmwaUseImmersiveDarkModeLegacy, ref enabled);

        if (build < Windows11Build)
        {
            // Rounded corners and a custom border colour are Windows 11 features.
            // Windows 10 keeps square corners and the system border, which is the
            // correct look for that OS rather than a degraded one.
            return;
        }

        var corner = DwmwcpRound;
        TrySet(hwnd, DwmwaWindowCornerPreference, ref corner);

        var border = BorderColorRef;
        TrySet(hwnd, DwmwaBorderColor, ref border);
    }

    private static void TrySet(IntPtr hwnd, int attribute, ref int value)
    {
        try
        {
            _ = DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // dwmapi.dll is present on every supported OS; guard anyway so a
            // missing composition stack cannot stop the window from opening.
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    /// <summary>
    /// A <see cref="WindowChrome"/> window maximises to the full monitor rectangle and
    /// covers the taskbar. Reporting the work area here is what keeps the taskbar
    /// visible and the window edges on screen.
    /// <para>
    /// Windows then inflates this rectangle by the non-client frame on every side, so
    /// the window really ends up slightly larger than the work area. That is normal —
    /// every native maximised window does it, and the taskbar stays visible because it
    /// is topmost. What is <i>not</i> normal is that <see cref="WindowChrome"/> has
    /// turned that frame into client area, so the app has to inset its own content by
    /// the same amount; see <see cref="ChromeWindow.ChromeInset"/>. Deflating here
    /// instead does not work: Windows only applies the inflation when the requested
    /// size matches the work area exactly, so a pre-deflated request is honoured
    /// verbatim and leaves a gap of desktop around a "maximised" window.
    /// </para>
    /// </summary>
    internal static void ClampMaximizeToWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);

        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };

        if (!GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var mmi = Marshal.PtrToStructure<MinMaxInfo>(lParam);

        mmi.ptMaxPosition.X = info.rcWork.Left - info.rcMonitor.Left;
        mmi.ptMaxPosition.Y = info.rcWork.Top - info.rcMonitor.Top;
        mmi.ptMaxSize.X = info.rcWork.Right - info.rcWork.Left;
        mmi.ptMaxSize.Y = info.rcWork.Bottom - info.rcWork.Top;

        Marshal.StructureToPtr(mmi, lParam, fDeleteOld: true);
    }

    internal static int HorizontalFrame(IntPtr hwnd) => FrameThickness(hwnd, SmCxFrame);

    internal static int VerticalFrame(IntPtr hwnd) => FrameThickness(hwnd, SmCyFrame);

    /// <summary>
    /// Width of one side of the resizable frame, in physical pixels for the monitor the
    /// window is on. The padded border is a separate metric from the sizing frame and
    /// both are part of the inflation, so both have to be counted.
    /// </summary>
    private static int FrameThickness(IntPtr hwnd, int sizingMetric)
    {
        try
        {
            var dpi = GetDpiForWindow(hwnd);

            if (dpi != 0)
            {
                return GetSystemMetricsForDpi(sizingMetric, dpi) + GetSystemMetricsForDpi(SmCxPaddedBorder, dpi);
            }
        }
        catch (EntryPointNotFoundException)
        {
            // Pre-1607 has no per-monitor metrics; the system-DPI values below are
            // correct there because the OS could not mix scale factors anyway.
        }

        return GetSystemMetrics(sizingMetric) + GetSystemMetrics(SmCxPaddedBorder);
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point32
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect32
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect32 rcMonitor;
        public Rect32 rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point32 ptReserved;
        public Point32 ptMaxSize;
        public Point32 ptMaxPosition;
        public Point32 ptMinTrackSize;
        public Point32 ptMaxTrackSize;
    }
}
