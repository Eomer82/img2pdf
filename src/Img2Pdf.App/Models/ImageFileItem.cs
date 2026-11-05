using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Img2Pdf.App.Services;

namespace Img2Pdf.App.Models;

public enum ConversionStatus
{
    Venter,
    Behandler,
    Fullført,
    Feilet
}

public partial class ImageFileItem : ObservableObject
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string Directory { get; init; }
    public long FileSizeBytes { get; init; }
    public string FormattedSize => FormatBytes(FileSizeBytes);

    [ObservableProperty]
    private string _dimensions = LocalizationService.GetString("Dimensions_Unknown");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    private ConversionStatus _status = ConversionStatus.Venter;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalRotation))]
    private int _manualRotation = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalRotation))]
    private int _exifRotation = 0;

    public int TotalRotation => (ExifRotation + ManualRotation) % 360;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    private ImageSource? _thumbnail;

    public bool HasThumbnail => Thumbnail != null;

    public string StatusDisplay => Status switch
    {
        ConversionStatus.Venter => LocalizationService.GetString("Status_Waiting"),
        ConversionStatus.Behandler => LocalizationService.GetString("Status_Processing"),
        ConversionStatus.Fullført => LocalizationService.GetString("Status_Completed"),
        ConversionStatus.Feilet => LocalizationService.GetString("Status_Failed"),
        _ => Status.ToString()
    };

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(StatusDisplay));
        if (Dimensions == "Ukjent" || Dimensions == "Unknown")
        {
            Dimensions = LocalizationService.GetString("Dimensions_Unknown");
        }
    }

    public static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
