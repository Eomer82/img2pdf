using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    [NotifyPropertyChangedFor(nameof(HasSelectedItem))]
    private ImageFileItem? _selectedItem;

    public bool HasSelectedItem => SelectedItem != null;

    // Full-Size Preview (Quick Look)
    [ObservableProperty]
    private bool _isPreviewOpen;

    [ObservableProperty]
    private ImageFileItem? _previewItem;

    [ObservableProperty]
    private ImageSource? _previewImage;

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
    [NotifyPropertyChangedFor(nameof(HasMarginsConfigurable))]
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

    public bool HasMarginsConfigurable => !IsOriginalSize;

    // Images per page (1, 2, 4)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOneImagePerPage))]
    [NotifyPropertyChangedFor(nameof(IsTwoImagesPerPage))]
    [NotifyPropertyChangedFor(nameof(IsFourImagesPerPage))]
    private ImagesPerPage _imagesPerPage = ImagesPerPage.One;

    public bool IsOneImagePerPage
    {
        get => ImagesPerPage == ImagesPerPage.One;
        set
        {
            if (value) ImagesPerPage = ImagesPerPage.One;
        }
    }

    public bool IsTwoImagesPerPage
    {
        get => ImagesPerPage == ImagesPerPage.Two;
        set
        {
            if (value) ImagesPerPage = ImagesPerPage.Two;
        }
    }

    public bool IsFourImagesPerPage
    {
        get => ImagesPerPage == ImagesPerPage.Four;
        set
        {
            if (value) ImagesPerPage = ImagesPerPage.Four;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLosslessQuality))]
    [NotifyPropertyChangedFor(nameof(IsCompressedQuality))]
    private QualityMode _qualityMode = QualityMode.Lossless;

    public bool IsLosslessQuality
    {
        get => QualityMode == QualityMode.Lossless;
        set
        {
            if (value) QualityMode = QualityMode.Lossless;
        }
    }

    public bool IsCompressedQuality
    {
        get => QualityMode == QualityMode.Compressed;
        set
        {
            if (value) QualityMode = QualityMode.Compressed;
        }
    }

    [ObservableProperty]
    private int _jpegQuality = 80;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMarginNone))]
    [NotifyPropertyChangedFor(nameof(IsMarginCompact))]
    [NotifyPropertyChangedFor(nameof(IsMarginNormal))]
    private PageMarginMode _marginMode = PageMarginMode.Compact;

    public bool IsMarginNone
    {
        get => MarginMode == PageMarginMode.None;
        set
        {
            if (value) MarginMode = PageMarginMode.None;
        }
    }

    public bool IsMarginCompact
    {
        get => MarginMode == PageMarginMode.Compact;
        set
        {
            if (value) MarginMode = PageMarginMode.Compact;
        }
    }

    public bool IsMarginNormal
    {
        get => MarginMode == PageMarginMode.Normal;
        set
        {
            if (value) MarginMode = PageMarginMode.Normal;
        }
    }

    [ObservableProperty]
    private string _mergedFileName;

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
    private bool _openPdfWhenDone = false;

    [ObservableProperty]
    private bool _enablePasswordProtection;

    [ObservableProperty]
    private string _password = string.Empty;

    // PDF Document Metadata
    [ObservableProperty]
    private string _documentTitle = string.Empty;

    [ObservableProperty]
    private string _documentAuthor = string.Empty;

    [ObservableProperty]
    private string _documentSubject = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConvert))]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _progressText;

    [ObservableProperty]
    private string _statusSummary;

    public bool CanConvert => Files.Count > 0 && !IsBusy;

    public MainViewModel()
    {
        _mergedFileName = LocalizationService.GetString("DefaultMergedFileName");
        _progressText = LocalizationService.GetString("ReadyToConvert");
        _statusSummary = LocalizationService.GetString("NoImagesSelected");

        Files.CollectionChanged += (_, _) =>
        {
            HasFiles = Files.Count > 0;
            UpdateStatusSummary();
            OnPropertyChanged(nameof(CanConvert));
        };

        LocalizationService.Instance.LanguageChanged += (_, _) =>
        {
            UpdateStatusSummary();
            if (!IsBusy)
            {
                ProgressText = Files.Count > 0
                    ? LocalizationService.GetString("ImagesReady", Files.Count)
                    : LocalizationService.GetString("ReadyToConvert");
            }
            foreach (var file in Files)
            {
                file.RefreshLocalization();
            }
        };
    }

    public async Task AddPathsAsync(IEnumerable<string> paths)
    {
        IsBusy = true;
        ProgressText = LocalizationService.GetString("SearchingImages");
        try
        {
            var discovered = await ImageDiscoveryService.DiscoverImagesAsync(paths);
            foreach (var item in discovered)
            {
                if (!Files.Any(f => string.Equals(f.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    Files.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            ProgressText = LocalizationService.GetString("ErrorReadingFiles", ex.Message);
        }
        finally
        {
            IsBusy = false;
            ProgressText = Files.Count > 0 
                ? LocalizationService.GetString("ImagesReady", Files.Count) 
                : LocalizationService.GetString("ReadyToConvert");
            UpdateStatusSummary();
        }
    }

    private void UpdateStatusSummary()
    {
        if (Files.Count == 0)
        {
            StatusSummary = LocalizationService.GetString("NoImagesSelected");
            return;
        }

        long totalBytes = Files.Sum(f => f.FileSizeBytes);
        string plural = Files.Count > 1 ? LocalizationService.GetString("PluralSuffix") : "";
        StatusSummary = LocalizationService.GetString("ImagesCount", Files.Count, plural, ImageFileItem.FormatBytes(totalBytes));
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
    private void RotateSelected()
    {
        var target = (IsPreviewOpen && PreviewItem != null) ? PreviewItem : SelectedItem;
        if (target != null)
        {
            target.ManualRotation = (target.ManualRotation + 90) % 360;
            OnPropertyChanged(nameof(PreviewItem));
        }
    }

    [RelayCommand]
    public void OpenPreview(ImageFileItem? item = null)
    {
        var target = item ?? SelectedItem;
        if (target != null)
        {
            SelectedItem = target;
            PreviewItem = target;
            LoadPreviewImage(target);
            IsPreviewOpen = true;
        }
    }

    [RelayCommand]
    public void ClosePreview()
    {
        IsPreviewOpen = false;
        PreviewItem = null;
        PreviewImage = null;
    }

    [RelayCommand]
    public void NextPreview()
    {
        if (PreviewItem == null || Files.Count <= 1) return;
        int idx = Files.IndexOf(PreviewItem);
        if (idx >= 0)
        {
            int nextIdx = (idx + 1) % Files.Count;
            OpenPreview(Files[nextIdx]);
        }
    }

    [RelayCommand]
    public void PreviousPreview()
    {
        if (PreviewItem == null || Files.Count <= 1) return;
        int idx = Files.IndexOf(PreviewItem);
        if (idx >= 0)
        {
            int prevIdx = (idx - 1 + Files.Count) % Files.Count;
            OpenPreview(Files[prevIdx]);
        }
    }

    private void LoadPreviewImage(ImageFileItem item)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(item.FilePath);
            bmp.DecodePixelWidth = 1920;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.EndInit();
            bmp.Freeze();
            PreviewImage = bmp;
        }
        catch
        {
            PreviewImage = item.Thumbnail;
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        Files.Clear();
        SelectedItem = null;
        ProgressValue = 0;
        ProgressText = LocalizationService.GetString("ReadyToConvert");
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

    public void MoveItem(int oldIndex, int newIndex)
    {
        if (oldIndex >= 0 && oldIndex < Files.Count && newIndex >= 0 && newIndex < Files.Count && oldIndex != newIndex)
        {
            Files.Move(oldIndex, newIndex);
        }
    }

    public void SortFiles(int columnIndex, bool ascending)
    {
        if (Files.Count <= 1) return;

        IEnumerable<ImageFileItem> sorted = columnIndex switch
        {
            0 => ascending ? Files.OrderBy(f => f.FileName) : Files.OrderByDescending(f => f.FileName),
            1 => ascending ? Files.OrderBy(f => f.Dimensions) : Files.OrderByDescending(f => f.Dimensions),
            2 => ascending ? Files.OrderBy(f => f.FileSizeBytes) : Files.OrderByDescending(f => f.FileSizeBytes),
            3 => ascending ? Files.OrderBy(f => f.Status) : Files.OrderByDescending(f => f.Status),
            4 => ascending ? Files.OrderBy(f => f.Directory) : Files.OrderByDescending(f => f.Directory),
            _ => Files
        };

        var list = sorted.ToList();
        Files.Clear();
        foreach (var item in list)
        {
            Files.Add(item);
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
            QualityMode = QualityMode,
            JpegQuality = JpegQuality,
            MarginMode = MarginMode,
            ImagesPerPage = ImagesPerPage,
            MergedFileName = MergedFileName,
            UseCustomOutputFolder = UseCustomOutputFolder,
            CustomOutputFolder = CustomOutputFolder,
            OpenFolderWhenDone = OpenFolderWhenDone,
            OpenPdfWhenDone = OpenPdfWhenDone,
            EnablePasswordProtection = EnablePasswordProtection,
            Password = Password,
            DocumentTitle = DocumentTitle,
            DocumentAuthor = DocumentAuthor,
            DocumentSubject = DocumentSubject
        };

        foreach (var file in Files)
        {
            file.Status = ConversionStatus.Venter;
            file.ErrorMessage = null;
        }

        var progress = new Progress<ConversionProgressReport>(report =>
        {
            ProgressValue = report.Percentage;
            ProgressText = LocalizationService.GetString("ConvertingProgress", report.CurrentIndex, report.TotalCount, report.CurrentFile);
        });

        string targetFolder = settings.UseCustomOutputFolder && !string.IsNullOrWhiteSpace(settings.CustomOutputFolder)
            ? settings.CustomOutputFolder
            : Files[0].Directory;

        string primaryPdfFile = settings.OutputMode == OutputMode.MergedPdf
            ? Path.Combine(targetFolder, settings.MergedFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? settings.MergedFileName : $"{settings.MergedFileName}.pdf")
            : Path.Combine(targetFolder, $"{Path.GetFileNameWithoutExtension(Files[0].FilePath)}.pdf");

        try
        {
            var stopwatch = Stopwatch.StartNew();
            await PdfConversionService.ConvertAsync(Files.ToList(), settings, progress, _cts.Token);
            stopwatch.Stop();

            ProgressValue = 100;
            ProgressText = LocalizationService.GetString("CompletedInSeconds", stopwatch.Elapsed.TotalSeconds);

            if (settings.OpenPdfWhenDone && File.Exists(primaryPdfFile))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = primaryPdfFile,
                    UseShellExecute = true
                });
            }
            else if (settings.OpenFolderWhenDone && Directory.Exists(targetFolder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetFolder,
                    UseShellExecute = true
                });
            }
        }
        catch (OperationCanceledException)
        {
            ProgressText = LocalizationService.GetString("ConversionCancelled");
        }
        catch (Exception ex)
        {
            ProgressText = LocalizationService.GetString("ConversionFailed", ex.Message);
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
