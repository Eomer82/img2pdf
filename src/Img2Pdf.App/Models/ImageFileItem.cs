using CommunityToolkit.Mvvm.ComponentModel;

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
    private string _dimensions = "Ukjent";

    [ObservableProperty]
    private ConversionStatus _status = ConversionStatus.Venter;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isSelected;

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
