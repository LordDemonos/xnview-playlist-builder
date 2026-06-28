using System.Windows;
using System.Windows.Threading;
using XnViewPlaylistBuilder.Cli;
using XnViewPlaylistBuilder.Core.Logging;

namespace XnViewPlaylistBuilder;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Unhandled UI exception.", args.Exception);
            MessageBox.Show(
                $"The application encountered an error:{Environment.NewLine}{Environment.NewLine}{args.Exception.Message}",
                "XnView Playlist Builder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                AppLog.Error("Unhandled domain exception.", ex);
            }
        };

        AppLog.Info("Application starting.");

        if (e.Args.Any(arg => arg.StartsWith("--", StringComparison.Ordinal)))
        {
            var exitCode = CliRunner.Run(e.Args);
            Shutdown(exitCode);
            return;
        }

        try
        {
            var mainWindow = new MainWindow(e.Args);
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            AppLog.Error("Application startup failed.", ex);
            MessageBox.Show(
                $"Startup failed:{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "XnView Playlist Builder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        base.OnStartup(e);
    }
}
