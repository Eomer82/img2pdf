using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
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
    [NotifyPropertyChangedFor(nameof(IsSelectedPdf))]
    private ImageFileItem? _selectedItem;

    public bool HasSelectedItem => SelectedItem != null;
    public bool IsSelectedPdf => SelectedItem?.IsPdf == true;

    // Full-Size Preview (Quick Look)
    [ObservableProperty]
    private bool _isPreviewOpen;

    [ObservableProperty]
    private ImageFileItem? _previewItem;

    [ObservableProperty]
    private ImageSource? _previewImage;

    [ObservableProperty]
    private bool _isCropModeActive;

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

    // Document Filters (Original, Grayscale, B&W Document Scan)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOriginalFilter))]
    [NotifyPropertyChangedFor(nameof(IsGrayscaleFilter))]
    [NotifyPropertyChangedFor(nameof(IsBlackAndWhiteFilter))]
    private ImageFilterMode _filterMode = ImageFilterMode.Original;

    partial void OnFilterModeChanged(ImageFilterMode value) => UpdateStatusSummary();

    public bool IsOriginalFilter
    {
        get => FilterMode == ImageFilterMode.Original;
        set { if (value) FilterMode = ImageFilterMode.Original; }
    }

    public bool IsGrayscaleFilter
    {
        get => FilterMode == ImageFilterMode.Grayscale;
        set { if (value) FilterMode = ImageFilterMode.Grayscale; }
    }

    public bool IsBlackAndWhiteFilter
    {
        get => FilterMode == ImageFilterMode.BlackAndWhite;
        set { if (value) FilterMode = ImageFilterMode.BlackAndWhite; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLosslessQuality))]
    [NotifyPropertyChangedFor(nameof(IsCompressedQuality))]
    private QualityMode _qualityMode = QualityMode.Lossless;

    partial void OnQualityModeChanged(QualityMode value) => UpdateStatusSummary();

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

    partial void OnJpegQualityChanged(int value) => UpdateStatusSummary();

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

    // Page Numbers & Watermark
    [ObservableProperty]
    private bool _enablePageNumbers;

    [ObservableProperty]
    private string _pageNumberFormat = "Side {0} av {1}";

    [ObservableProperty]
    private bool _enableWatermark;

    [ObservableProperty]
    private string _watermarkText = "DRAFT";

    // Bildetekst / Captions (Filnavn & Dato)
    [ObservableProperty]
    private bool _showCaptions;

    [ObservableProperty]
    private CaptionMode _captionMode = CaptionMode.FileName;

    // Søkbar PDF (Windows OCR)
    [ObservableProperty]
    private bool _enableOcr;

    public bool IsOcrSupported => OcrService.IsSupported;

    // Windows Explorer Høyreklikkmeny
    [ObservableProperty]
    private bool _enableContextMenuIntegration;

    partial void OnEnableContextMenuIntegrationChanged(bool value)
    {
        ShellIntegrationService.SetContextMenuRegistered(value);
    }

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

    [ObservableProperty]
    private string _estimatedOutputSize = string.Empty;

    public bool CanConvert => Files.Count > 0 && !IsBusy;

    public MainViewModel()
    {
        _mergedFileName = LocalizationService.GetString("DefaultMergedFileName");
        _progressText = LocalizationService.GetString("ReadyToConvert");
        _statusSummary = LocalizationService.GetString("NoImagesSelected");
        _enableContextMenuIntegration = ShellIntegrationService.IsContextMenuRegistered();

        LocalizationService.Instance.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(StatusSummary));
            OnPropertyChanged(nameof(ProgressText));
            UpdateStatusSummary();
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
            var items = await ImageDiscoveryService.DiscoverImagesAsync(paths);
            foreach (var item in items)
            {
                Files.Add(item);
            }

            if (SelectedItem == null && Files.Count > 0)
            {
                SelectedItem = Files[0];
            }

            HasFiles = Files.Count > 0;
            UpdateStatusSummary();
            ProgressText = LocalizationService.GetString("ImagesReady", Files.Count);
        }
        catch (Exception ex)
        {
            ProgressText = LocalizationService.GetString("ErrorReadingFiles", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task PasteFromClipboardAsync()
    {
        if (Clipboard.ContainsImage())
        {
            var image = Clipboard.GetImage();
            if (image != null)
            {
                string tempFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Img2Pdf", "Temp");
                Directory.CreateDirectory(tempFolder);
                string tempPath = Path.Combine(tempFolder, $"Pasted_Image_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                using (var fileStream = new FileStream(tempPath, FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    encoder.Save(fileStream);
                }
                await AddPathsAsync(new[] { tempPath });
            }
        }
    }

    [RelayCommand]
    public async Task ExtractPdfPagesAsync(ImageFileItem? item = null)
    {
        var target = item ?? SelectedItem;
        if (target != null && target.IsPdf)
        {
            string outputFolder = Path.Combine(target.Directory, $"{Path.GetFileNameWithoutExtension(target.FilePath)}_Sider");
            ProgressText = LocalizationService.GetString("ExtractingPdfPages");
            try
            {
                var files = await PdfConversionService.ExtractPdfPagesAsync(target.FilePath, outputFolder);
                ProgressText = LocalizationService.GetString("PdfPagesExtracted", files.Count);
                if (Directory.Exists(outputFolder))
                {
                    Process.Start(new ProcessStartInfo { FileName = outputFolder, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                ProgressText = LocalizationService.GetString("ConversionFailed", ex.Message);
            }
        }
    }

    private void UpdateStatusSummary()
    {
        if (Files.Count == 0)
        {
            StatusSummary = LocalizationService.GetString("NoImagesSelected");
            EstimatedOutputSize = string.Empty;
            return;
        }

        long totalBytes = Files.Sum(f => f.FileSizeBytes);
        string totalSize = ImageFileItem.FormatBytes(totalBytes);
        string plural = Files.Count > 1 ? LocalizationService.GetString("PluralSuffix") : string.Empty;

        StatusSummary = LocalizationService.GetString("ImagesCount", Files.Count, plural, totalSize);

        // Calculate estimated PDF size
        long estimatedBytes;
        if (FilterMode == ImageFilterMode.BlackAndWhite)
        {
            estimatedBytes = Math.Max(25_000, Files.Count * 95_000L);
        }
        else if (QualityMode == QualityMode.Compressed)
        {
            double factor = (JpegQuality / 100.0) * 0.38;
            estimatedBytes = (long)(totalBytes * factor);
        }
        else
        {
            estimatedBytes = (long)(totalBytes * 1.02);
        }

        EstimatedOutputSize = LocalizationService.GetString("EstimatedPdfSize", ImageFileItem.FormatBytes(estimatedBytes));
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedItem != null)
        {
            Files.Remove(SelectedItem);
            SelectedItem = Files.FirstOrDefault();
            HasFiles = Files.Count > 0;
            UpdateStatusSummary();
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
            IsCropModeActive = false;
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
        IsCropModeActive = false;
    }

    [RelayCommand]
    public void ToggleCropMode()
    {
        IsCropModeActive = !IsCropModeActive;
    }

    [RelayCommand]
    public void ResetCrop()
    {
        if (PreviewItem != null)
        {
            PreviewItem.ResetCrop();
            OnPropertyChanged(nameof(PreviewItem));
        }
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
        if (item.IsPdf)
        {
            PreviewImage = null;
            return;
        }

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
    public void ApplyPreset(string presetName)
    {
        switch (presetName?.ToLowerInvariant())
        {
            case "receipts":
                PageSizeMode = PageSizeMode.A4_AutoOrientation;
                MarginMode = PageMarginMode.Compact;
                ImagesPerPage = ImagesPerPage.Two;
                FilterMode = ImageFilterMode.Grayscale;
                QualityMode = QualityMode.Compressed;
                JpegQuality = 80;
                OutputMode = OutputMode.MergedPdf;
                break;

            case "photoalbum":
                PageSizeMode = PageSizeMode.OriginalImageSize;
                MarginMode = PageMarginMode.None;
                ImagesPerPage = ImagesPerPage.One;
                FilterMode = ImageFilterMode.Original;
                QualityMode = QualityMode.Lossless;
                OutputMode = OutputMode.MergedPdf;
                break;

            case "email":
                PageSizeMode = PageSizeMode.A4_AutoOrientation;
                MarginMode = PageMarginMode.Compact;
                ImagesPerPage = ImagesPerPage.One;
                FilterMode = ImageFilterMode.Original;
                QualityMode = QualityMode.Compressed;
                JpegQuality = 75;
                break;

            case "documentscan":
                PageSizeMode = PageSizeMode.A4_AutoOrientation;
                MarginMode = PageMarginMode.Compact;
                ImagesPerPage = ImagesPerPage.One;
                FilterMode = ImageFilterMode.BlackAndWhite;
                QualityMode = QualityMode.Compressed;
                JpegQuality = 85;
                OutputMode = OutputMode.MergedPdf;
                break;

            default: // Standard
                PageSizeMode = PageSizeMode.OriginalImageSize;
                MarginMode = PageMarginMode.Compact;
                ImagesPerPage = ImagesPerPage.One;
                FilterMode = ImageFilterMode.Original;
                QualityMode = QualityMode.Lossless;
                break;
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        Files.Clear();
        SelectedItem = null;
        HasFiles = false;
        ProgressValue = 0;
        ProgressText = LocalizationService.GetString("ReadyToConvert");
        UpdateStatusSummary();
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedItem == null) return;
        int index = Files.IndexOf(SelectedItem);
        if (index > 0)
        {
            Files.Move(index, index - 1);
        }
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedItem == null) return;
        int index = Files.IndexOf(SelectedItem);
        if (index < Files.Count - 1 && index >= 0)
        {
            Files.Move(index, index + 1);
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
            FilterMode = FilterMode,
            MergedFileName = MergedFileName,
            UseCustomOutputFolder = UseCustomOutputFolder,
            CustomOutputFolder = CustomOutputFolder,
            OpenFolderWhenDone = OpenFolderWhenDone,
            OpenPdfWhenDone = OpenPdfWhenDone,
            EnablePasswordProtection = EnablePasswordProtection,
            Password = Password,
            EnablePageNumbers = EnablePageNumbers,
            PageNumberFormat = PageNumberFormat,
            EnableWatermark = EnableWatermark,
            WatermarkText = WatermarkText,
            ShowCaptions = ShowCaptions,
            CaptionMode = CaptionMode,
            EnableOcr = EnableOcr,
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
