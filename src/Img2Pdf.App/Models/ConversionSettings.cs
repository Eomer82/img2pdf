namespace Img2Pdf.App.Models;

public enum OutputMode
{
    IndividualPdfs, // One PDF per image
    MergedPdf       // Merge all images into one multi-page PDF
}

public enum PageSizeMode
{
    OriginalImageSize,   // 1:1 original size (Lossless / preserve dimensions)
    A4_AutoOrientation,  // A4 standard (auto portrait or landscape)
    Letter_AutoOrientation // US Letter standard
}

public enum QualityMode
{
    Lossless,   // 100% original quality
    Compressed  // Re-compressed JPEG (smaller file size for email/web)
}

public enum PageMarginMode
{
    None,      // 0 mm (Borderless / edge-to-edge)
    Compact,   // 10 mm
    Normal     // 20 mm
}

public class ConversionSettings
{
    public OutputMode OutputMode { get; set; } = OutputMode.IndividualPdfs;
    public PageSizeMode PageSizeMode { get; set; } = PageSizeMode.OriginalImageSize;
    public QualityMode QualityMode { get; set; } = QualityMode.Lossless;
    public int JpegQuality { get; set; } = 80;
    public PageMarginMode MarginMode { get; set; } = PageMarginMode.Compact;
    public string MergedFileName { get; set; } = "Images_Merged.pdf";
    public bool UseCustomOutputFolder { get; set; } = false;
    public string? CustomOutputFolder { get; set; }
    public bool OpenFolderWhenDone { get; set; } = true;
    public bool EnablePasswordProtection { get; set; } = false;
    public string? Password { get; set; }
}
