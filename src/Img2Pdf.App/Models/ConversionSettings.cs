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

public enum ImagesPerPage
{
    One = 1,   // 1 image per page (Standard)
    Two = 2,   // 2 images per page (Top / Bottom or Side-by-side)
    Four = 4   // 4 images per page (2x2 grid)
}

public enum ImageFilterMode
{
    Original,      // Standard color
    Grayscale,     // Grayscale
    BlackAndWhite  // High-contrast B&W document scan
}

public enum PageNumberPosition
{
    BottomCenter,
    BottomRight,
    BottomLeft
}

public enum CaptionMode
{
    FileName,
    DateTaken,
    FileNameAndDate
}

public class ConversionSettings
{
    public OutputMode OutputMode { get; set; } = OutputMode.IndividualPdfs;
    public PageSizeMode PageSizeMode { get; set; } = PageSizeMode.OriginalImageSize;
    public QualityMode QualityMode { get; set; } = QualityMode.Lossless;
    public int JpegQuality { get; set; } = 80;
    public PageMarginMode MarginMode { get; set; } = PageMarginMode.Compact;
    public ImagesPerPage ImagesPerPage { get; set; } = ImagesPerPage.One;
    public ImageFilterMode FilterMode { get; set; } = ImageFilterMode.Original;
    public string MergedFileName { get; set; } = "Images_Merged.pdf";
    public bool UseCustomOutputFolder { get; set; } = false;
    public string? CustomOutputFolder { get; set; }
    public bool OpenFolderWhenDone { get; set; } = true;
    public bool OpenPdfWhenDone { get; set; } = false;
    public bool EnablePasswordProtection { get; set; } = false;
    public string? Password { get; set; }

    // Page Numbers & Watermark
    public bool EnablePageNumbers { get; set; } = false;
    public string PageNumberFormat { get; set; } = "Side {0} av {1}";
    public PageNumberPosition PageNumberPosition { get; set; } = PageNumberPosition.BottomCenter;
    public bool EnableWatermark { get; set; } = false;
    public string WatermarkText { get; set; } = "DRAFT";

    // Captions (Filnavn & Dato)
    public bool ShowCaptions { get; set; } = false;
    public CaptionMode CaptionMode { get; set; } = CaptionMode.FileName;

    // Searchable PDF (Windows OCR)
    public bool EnableOcr { get; set; } = false;

    // PDF Metadata
    public string? DocumentTitle { get; set; }
    public string? DocumentAuthor { get; set; }
    public string? DocumentSubject { get; set; }
}
