using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Img2Pdf.App.Models;
using Img2Pdf.App.Services;

namespace Img2Pdf.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private CancellationTokenSource? _cts;

    public ObservableCollection<ImageFileItem> Files { get; } = new();

    [ObservableProperty]
    private ImageFileItem? _selectedItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndividualMode))]
    [NotifyPropertyChangedFor(nameof(IsMergedMode))]
    private OutputMode _outputMode = OutputMode.IndividualPdfs;

    public bool IsIndividualMode
    {
        get => OutputMode == OutputMode.IndividualPdfs;
        set
        {
            if (value) OutputMode = OutputMode.IndividualPdfs;
        }
    }

    public bool IsMergedMode
    {
        get => OutputMode == OutputMode.MergedPdf;
        set
        {
            if (value) OutputMode = OutputMode.MergedPdf;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOriginalSize))]
    [NotifyPropertyChangedFor(nameof(IsA4Size))]
    [NotifyPropertyChangedFor(nameof(IsLetterSize))]
    private PageSizeMode _pageSizeMode = PageSizeMode.OriginalImageSize;

    public bool IsOriginalSize
    {
        get => PageSizeMode == PageSizeMode.OriginalImageSize;
        set
        {
            if (value) PageSizeMode = PageSizeMode.OriginalImageSize;
        }
    }

    public bool IsA4Size
    {
        get => PageSizeMode == PageSizeMode.A4_AutoOrientation;
        set
        {
            if (value) PageSizeMode = PageSizeMode.A4_AutoOrientation;
        }
    }

    public bool IsLetterSize
    {
        get => PageSizeMode == PageSizeMode.Letter_AutoOrientation;
        set
        {
            if (value) PageSizeMode = PageSizeMode.Letter_AutoOrientation;
        }
    }

    [ObservableProperty]
    private string _mergedFileName = "Bilder_Samlet.pdf";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseSourceFolder))]
    private bool _useCustomOutputFolder = false;

    public bool UseSourceFolder
    {
        get => !UseCustomOutputFolder;
        set => UseCustomOutputFolder = !value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoFiles))]
    private bool _hasFiles;

    public bool HasNoFiles => !HasFiles;

    [ObservableProperty]
    private string _customOutputFolder = string.Empty;

    [ObservableProperty]
    private bool _openFolderWhenDone = true;

    [ObservableProperty]
    private bool _enablePasswordProtection;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConvert))]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _progressText = "Klar til å konvertere";

    [ObservableProperty]
    private string _statusSummary = "Ingen bilder lagt til";

    public bool CanConvert => Files.Count > 0 && !IsBusy;

    public MainViewModel()
    {
        Files.CollectionChanged += (_, _) =>
        {
            HasFiles = Files.Count > 0;
            UpdateStatusSummary();
            OnPropertyChanged(nameof(CanConvert));
        };
    }

    public async Task AddPathsAsync(IEnumerable<string> paths)
    {
        IsBusy = true;
        ProgressText = "Søker etter bildefiler...";
        try
        {
            var discovered = await ImageDiscoveryService.DiscoverImagesAsync(paths);
            foreach (var item in discovered)
            {
                // Unngå duplikater
                if (!Files.Any(f => string.Equals(f.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    Files.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            ProgressText = $"Feil ved lesing av filer: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ProgressText = Files.Count > 0 ? $"{Files.Count} bilder klare" : "Klar til å konvertere";
            UpdateStatusSummary();
        }
    }

    private void UpdateStatusSummary()
    {
        if (Files.Count == 0)
        {
            StatusSummary = "Ingen bilder valgt";
            return;
        }

        long totalBytes = Files.Sum(f => f.FileSizeBytes);
        StatusSummary = $"{Files.Count} bilde{(Files.Count > 1 ? "r" : "")} ({ImageFileItem.FormatBytes(totalBytes)})";
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedItem != null)
        {
            Files.Remove(SelectedItem);
            SelectedItem = Files.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        Files.Clear();
        SelectedItem = null;
        ProgressValue = 0;
        ProgressText = "Klar til å konvertere";
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedItem == null) return;
        int index = Files.IndexOf(SelectedItem);
        if (index > 0)
        {
            Files.Move(index, index - 1);
            SelectedItem = Files[index - 1];
        }
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedItem == null) return;
        int index = Files.IndexOf(SelectedItem);
        if (index >= 0 && index < Files.Count - 1)
        {
            Files.Move(index, index + 1);
            SelectedItem = Files[index + 1];
        }
    }

    [RelayCommand]
    private async Task StartConversionAsync()
    {
        if (Files.Count == 0 || IsBusy) return;

        IsBusy = true;
        ProgressValue = 0;
        _cts = new CancellationTokenSource();

        var settings = new ConversionSettings
        {
            OutputMode = OutputMode,
            PageSizeMode = PageSizeMode,
            MergedFileName = MergedFileName,
            UseCustomOutputFolder = UseCustomOutputFolder,
            CustomOutputFolder = CustomOutputFolder,
            OpenFolderWhenDone = OpenFolderWhenDone,
            EnablePasswordProtection = EnablePasswordProtection,
            Password = Password
        };

        // Nullstill status for alle elementer
        foreach (var file in Files)
        {
            file.Status = ConversionStatus.Venter;
            file.ErrorMessage = null;
        }

        var progress = new Progress<ConversionProgressReport>(report =>
        {
            ProgressValue = report.Percentage;
            ProgressText = $"Konverterer {report.CurrentIndex} av {report.TotalCount}: {report.CurrentFile}";
        });

        try
        {
            var stopwatch = Stopwatch.StartNew();
            await PdfConversionService.ConvertAsync(Files.ToList(), settings, progress, _cts.Token);
            stopwatch.Stop();

            ProgressValue = 100;
            ProgressText = $"Fullført på {stopwatch.Elapsed.TotalSeconds:F1} sekunder!";

            if (settings.OpenFolderWhenDone)
            {
                string targetFolder = settings.UseCustomOutputFolder && !string.IsNullOrWhiteSpace(settings.CustomOutputFolder)
                    ? settings.CustomOutputFolder
                    : Files[0].Directory;

                if (Directory.Exists(targetFolder))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = targetFolder,
                        UseShellExecute = true
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Konvertering avbrutt av bruker.";
        }
        catch (Exception ex)
        {
            ProgressText = $"Feil under konvertering: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void CancelConversion()
    {
        _cts?.Cancel();
    }
}
