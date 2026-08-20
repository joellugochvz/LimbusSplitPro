using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace LimbusSplitPro.App;

public partial class App : Application
{
    private static bool _isShowingError = false;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Catch unhandled exceptions on the WPF UI thread
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Catch unhandled exceptions on non-UI threads
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        // Catch unobserved task exceptions
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        LogAndShowException(e.Exception, "UI Thread Error");
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogAndShowException(ex, "Domain Error");
        }
    }

    private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        if (e.Exception != null)
        {
            LogAndShowException(e.Exception, "Task Error");
        }
    }

    private static void LogAndShowException(Exception ex, string type)
    {
        // Reentrancy guard to prevent infinite error dialog loops
        if (_isShowingError) return;
        _isShowingError = true;

        try
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "limbus_crash.log");
            string message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{type}] {ex.GetType().Name}: {ex.Message}\nStack Trace:\n{ex.StackTrace}\n\n";

            if (ex.InnerException != null)
            {
                message += $"Inner Exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}\n\n";
            }

            File.AppendAllText(logPath, message, Encoding.UTF8);

            MessageBox.Show(
                $"Ha ocurrido un error en Limbus Split Pro:\n\n{ex.Message}\n\nSe ha guardado un registro del error en:\n{logPath}",
                "Error en Limbus Split Pro",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // Ignore logging failures
        }
        finally
        {
            // Terminate process cleanly so WPF dispatcher doesn't loop on broken UI state
            Environment.Exit(1);
        }
    }
}
