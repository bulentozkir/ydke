using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace YDKE_Windows;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly nint _windowHandle;
    private readonly DispatcherTimer _windowModeTransitionTimer = new();
    private bool _isActivated;
    private bool _isMinimized;
    private bool _windowModeChanging;

    internal event EventHandler? WindowModeChanged;

    internal bool IsFullScreen => AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;

    internal bool CanChangeWindowMode => !_windowModeChanging;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.Title = "YDKE - Yabancı Dil Kelime Ezberleme";
        AppWindow.SetIcon("Assets/AppIcon.ico");
        ApplyAppearance(AppearancePalette.Current);
        _windowModeTransitionTimer.Interval = TimeSpan.FromMilliseconds(500);
        _windowModeTransitionTimer.Tick += OnWindowModeTransitionFinished;

        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(_windowHandle) / 96.0;
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var width = Math.Min((int)Math.Round(1180 * scale), displayArea.WorkArea.Width);
        var height = Math.Min((int)Math.Round(760 * scale), displayArea.WorkArea.Height);
        AppWindow.Resize(new SizeInt32(width, height));
        if (AppWindow.Presenter is OverlappedPresenter windowed)
        {
            windowed.PreferredMinimumWidth = Math.Min((int)Math.Round(800 * scale), displayArea.WorkArea.Width);
            windowed.PreferredMinimumHeight = Math.Min((int)Math.Round(680 * scale), displayArea.WorkArea.Height);
        }

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
        Activated += OnWindowActivated;
        AppWindow.Changed += OnAppWindowChanged;
        EnterFullScreen();
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        _isActivated = args.WindowActivationState != WindowActivationState.Deactivated;
        UpdatePageActivity();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidPresenterChange) return;
        AppTitleBar.Visibility = sender.Presenter.Kind == AppWindowPresenterKind.FullScreen
            ? Visibility.Collapsed
            : Visibility.Visible;
        WindowModeChanged?.Invoke(this, EventArgs.Empty);
        if (sender.Presenter is not OverlappedPresenter presenter)
        {
            _isMinimized = false;
            UpdatePageActivity();
            return;
        }
        var wasMinimized = _isMinimized;
        _isMinimized = presenter.State == OverlappedPresenterState.Minimized;
        if (wasMinimized && !_isMinimized) _isActivated = true;
        UpdatePageActivity();
    }

    private void OnWindowRootPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.F11)
        {
            ToggleWindowMode();
            args.Handled = true;
        }
        else if (args.Key == VirtualKey.Escape && AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
        {
            ExitFullScreen();
            args.Handled = true;
        }
    }

    private void EnterFullScreen() => ChangeWindowMode(AppWindowPresenterKind.FullScreen);

    internal void ToggleWindowMode()
    {
        if (IsFullScreen) ExitFullScreen();
        else EnterFullScreen();
    }

    private void ExitFullScreen()
    {
        ChangeWindowMode(AppWindowPresenterKind.Default);
    }

    private void ChangeWindowMode(AppWindowPresenterKind mode)
    {
        if (_windowModeChanging || mode == AppWindow.Presenter.Kind) return;
        _windowModeChanging = true;
        WindowModeChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            AppWindow.SetPresenter(mode);
            _windowModeTransitionTimer.Start();
        }
        catch
        {
            _windowModeChanging = false;
            WindowModeChanged?.Invoke(this, EventArgs.Empty);
            throw;
        }
    }

    private void OnWindowModeTransitionFinished(object? sender, object e)
    {
        _windowModeTransitionTimer.Stop();
        _windowModeChanging = false;
        WindowModeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdatePageActivity()
    {
        if (RootFrame.Content is MainPage page)
        {
            page.SetWindowActive(_isActivated && !_isMinimized);
        }
    }

    internal void ApplyAppearance(AppearancePalette palette)
    {
        WindowRoot.Background = palette.BackgroundBrush;
        AppTitleBar.Background = palette.BackgroundBrush;
        AppTitleBar.Foreground = palette.BackgroundForegroundBrush;
        AppTitleBar.RequestedTheme = palette.Theme;
        RootFrame.RequestedTheme = palette.Theme;
    }

    internal bool IsWindowMinimized => IsIconic(_windowHandle);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint windowHandle);
}
