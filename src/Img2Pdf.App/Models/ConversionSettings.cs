namespace Img2Pdf.App.Models;

public enum OutputMode
{
    IndividualPdfs, // Én PDF per bilde
    MergedPdf       // Slå sammen alle til én flersidig PDF
}

public enum PageSizeMode
{
    OriginalImageSize,   // 1:1 original bildestørrelse (Maksimal kvalitet / tapsfritt)
    A4_AutoOrientation,  // A4 tilpasset (automatisk stående eller liggende)
    Letter_AutoOrientation // US Letter tilpasset
}

public class ConversionSettings
{
    public OutputMode OutputMode { get; set; } = OutputMode.IndividualPdfs;
    public PageSizeMode PageSizeMode { get; set; } = PageSizeMode.OriginalImageSize;
    public string MergedFileName { get; set; } = "Bilder_Samlet.pdf";
    public bool UseCustomOutputFolder { get; set; } = false;
    public string? CustomOutputFolder { get; set; }
    public bool OpenFolderWhenDone { get; set; } = true;
    public bool EnablePasswordProtection { get; set; } = false;
    public string? Password { get; set; }
}
