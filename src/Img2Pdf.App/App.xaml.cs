using System.IO;
using System.Windows;
using Img2Pdf.App.Models;
using Img2Pdf.App.Services;

namespace Img2Pdf.App;

public partial class App : Application
{
    public static List<string> InitialFilesToLoad { get; } = new();

    public App()
    {
        WindowsFontResolver.EnsureRegistered();

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

    protected override async void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length > 0)
        {
            bool isSilent = e.Args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase) || a.Equals("-s", StringComparison.OrdinalIgnoreCase));
            string? outputArg = null;
            var inputPaths = new List<string>();

            for (int i = 0; i < e.Args.Length; i++)
            {
                if ((e.Args[i].Equals("--output", StringComparison.OrdinalIgnoreCase) || e.Args[i].Equals("-o", StringComparison.OrdinalIgnoreCase)) && i + 1 < e.Args.Length)
                {
                    outputArg = e.Args[++i];
                }
                else if (e.Args[i].Equals("--input", StringComparison.OrdinalIgnoreCase) || e.Args[i].Equals("-i", StringComparison.OrdinalIgnoreCase))
                {
                    while (i + 1 < e.Args.Length && !e.Args[i + 1].StartsWith("-"))
                    {
                        inputPaths.Add(e.Args[++i]);
                    }
                }
                else if (!e.Args[i].StartsWith("-") && (File.Exists(e.Args[i]) || Directory.Exists(e.Args[i])))
                {
                    inputPaths.Add(e.Args[i]);
                }
            }

            if (isSilent && inputPaths.Count > 0)
            {
                // Headless CLI execution
                try
                {
                    var items = await ImageDiscoveryService.DiscoverImagesAsync(inputPaths);
                    if (items.Count > 0)
                    {
                        var settings = new ConversionSettings
                        {
                            OutputMode = OutputMode.MergedPdf,
                            MergedFileName = !string.IsNullOrWhiteSpace(outputArg) ? Path.GetFileName(outputArg) : "Output.pdf",
                            UseCustomOutputFolder = !string.IsNullOrWhiteSpace(outputArg),
                            CustomOutputFolder = !string.IsNullOrWhiteSpace(outputArg) ? Path.GetDirectoryName(outputArg) : null,
                            OpenFolderWhenDone = false,
                            OpenPdfWhenDone = false
                        };
                        await PdfConversionService.ConvertAsync(items, settings, null, CancellationToken.None);
                    }
                }
                catch { }
                Shutdown(0);
                return;
            }

            // Normal GUI launch with files passed via CLI / Context Menu
            InitialFilesToLoad.AddRange(inputPaths);
        }

        base.OnStartup(e);
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
