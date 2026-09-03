using System.Windows;
using System.Windows.Threading;

namespace TopWords.Windows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        LowProcessPriority.ApplyToCurrentProcess();
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            e.Exception.Message,
            AppConfig.WindowTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
