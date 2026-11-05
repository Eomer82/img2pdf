using System.IO;
using System.Windows;

namespace Img2Pdf.App;

public partial class App : Application
{
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            LogCrash(e.ExceptionObject as Exception);
        };

        DispatcherUnhandledException += (s, e) =>
        {
            LogCrash(e.Exception);
            e.Handled = false;
        };
    }

    private static void LogCrash(Exception? ex)
    {
        if (ex == null) return;
        string message = $"Oppstartsfeil i Img2Pdf:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
        if (ex.InnerException != null)
        {
            message += $"\n\nInner Exception:\n{ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n\n{ex.InnerException.StackTrace}";
        }
        
        MessageBox.Show(message, "Img2Pdf - Feilmelding", MessageBoxButton.OK, MessageBoxImage.Error);
        try
        {
            File.WriteAllText("crash_log.txt", message);
        }
        catch { }
    }
}
