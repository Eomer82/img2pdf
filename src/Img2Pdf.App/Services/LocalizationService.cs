using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Markup;

namespace Img2Pdf.App.Services;

public record LanguageInfo(string Code, string NativeName, string EnglishName, string Flag)
{
    public string DisplayName => $"{Flag} {NativeName}";
}

public class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public IReadOnlyList<LanguageInfo> SupportedLanguages { get; } = new List<LanguageInfo>
    {
        new("en", "English", "English", "🇬🇧"),
        new("nb", "Norsk", "Norwegian", "🇳🇴"),
        new("de", "Deutsch", "German", "🇩🇪"),
        new("es", "Español", "Spanish", "🇪🇸")
    };

    private LanguageInfo _currentLanguage;

    public LanguageInfo CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                SaveLanguagePreference(value.Code);
                OnPropertyChanged(string.Empty);
                OnPropertyChanged("Item[]");
                OnPropertyChanged(nameof(CurrentLanguage));
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string this[string key] => GetString(key);

    private readonly Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);

    private LocalizationService()
    {
        InitializeTranslations();

        string savedCode = LoadLanguagePreference();
        if (string.IsNullOrWhiteSpace(savedCode))
        {
            string sysLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            savedCode = (sysLang == "nb" || sysLang == "no" || sysLang == "nn") ? "nb" :
                        (sysLang == "de") ? "de" :
                        (sysLang == "es") ? "es" : "en";
        }

        _currentLanguage = SupportedLanguages.FirstOrDefault(l => l.Code.Equals(savedCode, StringComparison.OrdinalIgnoreCase)) 
                           ?? SupportedLanguages[0];
    }

    public void SetLanguage(string code)
    {
        var found = SupportedLanguages.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (found != null)
        {
            CurrentLanguage = found;
        }
    }

    public static string GetString(string key, params object[] args)
    {
        var inst = Instance;
        string langCode = inst.CurrentLanguage.Code;

        if (inst._translations.TryGetValue(langCode, out var dict) && dict.TryGetValue(key, out var text))
        {
            return args.Length > 0 ? string.Format(text, args) : text;
        }

        if (inst._translations.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enText))
        {
            return args.Length > 0 ? string.Format(enText, args) : enText;
        }

        return args.Length > 0 ? string.Format(key, args) : key;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string GetConfigFilePath()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Img2Pdf");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "language.json");
    }

    private static void SaveLanguagePreference(string code)
    {
        try
        {
            string path = GetConfigFilePath();
            File.WriteAllText(path, JsonSerializer.Serialize(new { Language = code }));
        }
        catch { }
    }

    private static string LoadLanguagePreference()
    {
        try
        {
            string path = GetConfigFilePath();
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("Language", out var prop))
                {
                    return prop.GetString() ?? string.Empty;
                }
            }
        }
        catch { }
        return string.Empty;
    }

    private void InitializeTranslations()
    {
        // -------------------------------------------------------------
        // ENGLISH (Default international)
        // -------------------------------------------------------------
        _translations["en"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AppTitle"] = "Img2Pdf - Image to PDF Converter",
            ["AppHeaderTitle"] = "Img2Pdf • High-Quality Image Converter",
            ["ThemeToggle"] = "Theme",
            ["Language"] = "Language",
            
            // Actions
            ["AddFiles"] = "Add files",
            ["AddFolder"] = "Add folder",
            ["PasteImage"] = "Paste image (Ctrl+V)",
            ["ExtractPdf"] = "Extract PDF pages",
            ["ExtractPdfAsImages"] = "Extract as images (PNG)",
            ["Rotate"] = "Rotate 90°",
            ["MoveUp"] = "Move up",
            ["MoveDown"] = "Move down",
            ["Remove"] = "Remove",
            ["ClearList"] = "Clear list",
            ["QuickLook"] = "Preview",
            ["Close"] = "Close",
            ["Crop"] = "Crop",
            ["CropMode"] = "Crop & Trim",
            ["ResetCrop"] = "Reset Crop",
            ["QuickLookTip"] = "Tip: Space or Esc to close • Left/Right arrows to flip • R to rotate • C to crop",
            ["ToolTip_Previous"] = "Previous (Left Arrow)",
            ["ToolTip_Next"] = "Next (Right Arrow)",
            ["CropTrimLeft"] = "Left %:",
            ["CropTrimRight"] = "Right %:",
            ["CropTrimTop"] = "Top %:",
            ["CropTrimBottom"] = "Bottom %:",

            // Presets
            ["Card_Presets"] = "Quick Presets",
            ["Preset_Standard"] = "Standard",
            ["Preset_Receipts"] = "Receipts",
            ["Preset_PhotoAlbum"] = "Photo Album",
            ["Preset_Email"] = "Email",
            ["Preset_DocScan"] = "Scan",

            // Empty drop area
            ["DropAreaTitle"] = "Drag and drop images, PDFs, or press Ctrl+V to paste",
            ["DropAreaSubtitle"] = "Supports JPG, PNG, BMP, WebP, TIFF and PDF • 100% lossless original quality",
            ["ChooseFiles"] = "Select files...",
            ["ChooseFolder"] = "Select folder...",

            // Table headers
            ["Col_FileName"] = "File Name",
            ["Col_Dimensions"] = "Resolution",
            ["Col_Size"] = "Size",
            ["Col_Status"] = "Status",
            ["Col_Folder"] = "Folder",

            // Status badges & types
            ["Status_Waiting"] = "Waiting",
            ["Status_Processing"] = "Converting",
            ["Status_Completed"] = "Completed",
            ["Status_Failed"] = "Failed",
            ["Dimensions_Unknown"] = "Unknown",
            ["PdfSinglePage"] = "1 page",
            ["PdfMultiPages"] = "{0} pages",
            ["PdfDocument"] = "PDF Document",
            ["EstimatedPdfSize"] = "Est. PDF size: ~{0}",
            ["ExtractingPdfPages"] = "Extracting PDF pages...",
            ["ExtractingPdfImages"] = "Extracting images from PDF...",
            ["PdfPagesExtracted"] = "Extracted {0} pages to folder!",
            ["PdfImagesExtracted"] = "Extracted {0} images to folder!",
            ["Card_PdfExtraction"] = "PDF Extraction Tools",
            ["PageRangeLabel"] = "Page range (e.g. 1-3, 5):",
            ["PageRangePlaceholder"] = "All pages (leave empty for all)",

            // Conversion mode
            ["Card_ModeTitle"] = "Conversion Mode",
            ["Mode_Individual"] = "One PDF per image (Separate)",
            ["Mode_Merged"] = "Merge all into a single PDF",
            ["MergedFileName"] = "PDF File Name:",
            ["DefaultMergedFileName"] = "Images_Merged.pdf",

            // Document Filters
            ["Card_Filter"] = "Document Filters",
            ["Filter_Original"] = "Color (Original)",
            ["Filter_Grayscale"] = "Grayscale",
            ["Filter_BlackAndWhite"] = "B&W Document Scan (High contrast)",

            // Quality & Compression
            ["Card_Quality"] = "Quality & Compression",
            ["Quality_Lossless"] = "100% Lossless (Original quality)",
            ["Quality_LosslessDesc"] = "Preserves bit-exact original image streams with zero quality loss.",
            ["Quality_Compressed"] = "Optimized for email / web (JPEG)",
            ["Quality_CompressedDesc"] = "Compresses images for significantly smaller file size.",
            ["CompressionLevel"] = "JPEG Quality:",

            // Page setup & Margins
            ["Card_PageSetup"] = "Page Layout & Margins",
            ["Size_Original"] = "Original image dimensions (1:1)",
            ["Size_OriginalDesc"] = "Each page matches exact image dimensions without borders.",
            ["Size_A4"] = "Fit to A4 (Auto portrait/landscape)",
            ["Size_A4Desc"] = "Scales image proportionally to standard A4 sheet.",
            ["Size_Letter"] = "Fit to US Letter",
            ["Section_Margins"] = "Page Margins:",
            ["Margin_None"] = "0 mm (Borderless / Edge-to-edge)",
            ["Margin_Compact"] = "10 mm (Compact margin)",
            ["Margin_Normal"] = "20 mm (Standard document margin)",

            // Multi-image per page
            ["ImagesPerPage"] = "Images per page:",
            ["OnePerPage"] = "1 image per page (Standard)",
            ["TwoPerPage"] = "2 images per page (Side by side)",
            ["FourPerPage"] = "4 images per page (2x2 grid)",

            // Page Numbers & Watermark
            ["Card_Decorations"] = "Page Numbers & Watermark",
            ["EnablePageNumbers"] = "Add page numbers",
            ["PageNumberFormatLabel"] = "Format:",
            ["EnableWatermark"] = "Add diagonal watermark",
            ["WatermarkTextLabel"] = "Watermark text:",

            // Captions
            ["Card_Captions"] = "Image Captions",
            ["ShowCaptions"] = "Print filename / date below each image",

            // Advanced & System
            ["Card_AdvancedFeatures"] = "System & Searchable PDF",
            ["EnableOcr"] = "Searchable PDF (Windows OCR)",
            ["OcrDescription"] = "Embeds invisible text layer for selecting and searching text with Ctrl+F.",
            ["EnableContextMenu"] = "Add to Windows Explorer right-click menu",
            ["ContextMenuDescription"] = "Allows converting images and folders directly from Windows Explorer.",
            ["ContextMenuTitle"] = "Convert to PDF with Img2Pdf",

            // Metadata
            ["Card_Metadata"] = "PDF Document Metadata",
            ["DocTitle"] = "Document Title:",
            ["DocAuthor"] = "Author:",
            ["DocSubject"] = "Subject / Topic:",

            // Output folder
            ["Card_OutputFolder"] = "Output Destination",
            ["Output_Source"] = "Same folder as source image",
            ["Output_Custom"] = "Specific folder",
            ["Browse"] = "Browse...",
            ["OpenWhenDone"] = "Open folder when conversion completes",
            ["OpenPdfWhenDone"] = "Open PDF directly when conversion completes",

            // Security
            ["Card_Security"] = "Security & Encryption",
            ["Security_Enable"] = "Password protect PDF",
            ["Security_PasswordLabel"] = "Password to open PDF:",
            ["Security_Desc"] = "Encrypts the PDF so a password is required to view.",

            // Action buttons
            ["StartConversion"] = "Start conversion",
            ["CancelConversion"] = "Cancel conversion",

            // Progress and summaries
            ["NoImagesSelected"] = "No files selected",
            ["ImagesCount"] = "{0} file{1} ({2})",
            ["PluralSuffix"] = "s",
            ["ReadyToConvert"] = "Ready to convert",
            ["ImagesReady"] = "{0} files ready",
            ["SearchingImages"] = "Scanning for files...",
            ["ErrorReadingFiles"] = "Error reading files: {0}",
            ["ConvertingProgress"] = "Converting {0} of {1}: {2}",
            ["CompletedInSeconds"] = "Completed in {0:F1} seconds!",
            ["ConversionCancelled"] = "Conversion cancelled by user.",
            ["ConversionFailed"] = "Conversion error: {0}",

            // Dialogs
            ["Dialog_SelectImagesTitle"] = "Select images or PDFs to convert",
            ["Dialog_ImageFilter"] = "Supported files (*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tiff;*.pdf)|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tiff;*.tif;*.pdf|All files (*.*)|*.*",
            ["Dialog_SelectFolderTitle"] = "Select folder containing images",
            ["Dialog_SelectOutputTitle"] = "Select destination folder for PDF files"
        };

        // -------------------------------------------------------------
        // NORWEGIAN (Bokmål)
        // -------------------------------------------------------------
        _translations["nb"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AppTitle"] = "Img2Pdf - Bilde til PDF-konverterer",
            ["AppHeaderTitle"] = "Img2Pdf • Høykvalitets bildekonverterer",
            ["ThemeToggle"] = "Bytt tema",
            ["Language"] = "Språk",

            // Actions
            ["AddFiles"] = "Legg til filer",
            ["AddFolder"] = "Legg til mappe",
            ["PasteImage"] = "Lim inn bilde (Ctrl+V)",
            ["ExtractPdf"] = "Pakk ut PDF-sider",
            ["ExtractPdfAsImages"] = "Pakk ut som bilder (PNG)",
            ["Rotate"] = "Roter 90°",
            ["MoveUp"] = "Flytt opp",
            ["MoveDown"] = "Flytt ned",
            ["Remove"] = "Fjern",
            ["ClearList"] = "Tøm liste",
            ["QuickLook"] = "Forhåndsvisning",
            ["Close"] = "Lukk",
            ["Crop"] = "Beskjær",
            ["CropMode"] = "Beskjæring",
            ["ResetCrop"] = "Nullstill beskjæring",
            ["QuickLookTip"] = "Tips: Mellomrom eller Esc for å lukke • Venstre/Høyre piltast for å bla • R for å rotere • C for å beskjære",
            ["ToolTip_Previous"] = "Forrige (Venstre pil)",
            ["ToolTip_Next"] = "Neste (Høyre pil)",
            ["CropTrimLeft"] = "Venstre %:",
            ["CropTrimRight"] = "Høyre %:",
            ["CropTrimTop"] = "Topp %:",
            ["CropTrimBottom"] = "Bunn %:",

            // Presets
            ["Card_Presets"] = "Hurtigprofiler",
            ["Preset_Standard"] = "Standard",
            ["Preset_Receipts"] = "Kvitteringer",
            ["Preset_PhotoAlbum"] = "Fotoalbum",
            ["Preset_Email"] = "E-post",
            ["Preset_DocScan"] = "Skanning",

            // Empty drop area
            ["DropAreaTitle"] = "Dra og slipp bilder, PDF-er eller trykk Ctrl+V",
            ["DropAreaSubtitle"] = "Støtter JPG, PNG, BMP, WebP, TIFF og PDF • 100 % tapsfri original kvalitet",
            ["ChooseFiles"] = "Velg filer...",
            ["ChooseFolder"] = "Velg mappe...",

            // Table headers
            ["Col_FileName"] = "Filnavn",
            ["Col_Dimensions"] = "Oppløsning",
            ["Col_Size"] = "Størrelse",
            ["Col_Status"] = "Status",
            ["Col_Folder"] = "Mappe",

            // Status badges & types
            ["Status_Waiting"] = "Venter",
            ["Status_Processing"] = "Behandler",
            ["Status_Completed"] = "Fullført",
            ["Status_Failed"] = "Feilet",
            ["Dimensions_Unknown"] = "Ukjent",
            ["PdfSinglePage"] = "1 side",
            ["PdfMultiPages"] = "{0} sider",
            ["PdfDocument"] = "PDF-dokument",
            ["EstimatedPdfSize"] = "Estimert PDF-størrelse: ~{0}",
            ["ExtractingPdfPages"] = "Pakker ut PDF-sider...",
            ["ExtractingPdfImages"] = "Pakker ut bilder fra PDF...",
            ["PdfPagesExtracted"] = "Pakket ut {0} sider til mappe!",
            ["PdfImagesExtracted"] = "Pakket ut {0} bilder til mappe!",
            ["Card_PdfExtraction"] = "PDF-utpakking og verktøy",
            ["PageRangeLabel"] = "Sideintervall (f.eks. 1-3, 5):",
            ["PageRangePlaceholder"] = "Alle sider (la stå tom for alle)",

            // Conversion mode
            ["Card_ModeTitle"] = "Konverteringsmodus",
            ["Mode_Individual"] = "Én PDF per bilde (Separat)",
            ["Mode_Merged"] = "Slå sammen alle til én PDF",
            ["MergedFileName"] = "PDF-filnavn:",
            ["DefaultMergedFileName"] = "Bilder_Samlet.pdf",

            // Document Filters
            ["Card_Filter"] = "Bilde- & Dokumentfilter",
            ["Filter_Original"] = "Farger (Original)",
            ["Filter_Grayscale"] = "Gråtoner",
            ["Filter_BlackAndWhite"] = "Svart-hvitt dokumentskann (Høy kontrast)",

            // Quality & Compression
            ["Card_Quality"] = "Bildekvalitet & Komprimering",
            ["Quality_Lossless"] = "100 % Tapsfri (Original kvalitet)",
            ["Quality_LosslessDesc"] = "Bevarer originalfiler og piksler uten rekomprimering.",
            ["Quality_Compressed"] = "Optimalisert for e-post / web (JPEG)",
            ["Quality_CompressedDesc"] = "Komprimerer bildene kraftig for mye mindre filstørrelse.",
            ["CompressionLevel"] = "JPEG-kvalitet:",

            // Page setup & Margins
            ["Card_PageSetup"] = "Sideoppsett & Marger",
            ["Size_Original"] = "Original bildestørrelse (1:1)",
            ["Size_OriginalDesc"] = "Hver PDF-side får nøyaktig samme mål som bildet uten kanter.",
            ["Size_A4"] = "Tilpass til A4 (Auto stående/ligg.)",
            ["Size_A4Desc"] = "Skalerer bildet proporsjonalt til et standard A4-ark.",
            ["Size_Letter"] = "Tilpass til US Letter",
            ["Section_Margins"] = "Sidemarger:",
            ["Margin_None"] = "0 mm (Kant-til-kant / Uten marg)",
            ["Margin_Compact"] = "10 mm (Kompakt marg)",
            ["Margin_Normal"] = "20 mm (Normal dokumentmarg)",

            // Multi-image per page
            ["ImagesPerPage"] = "Bilder per side:",
            ["OnePerPage"] = "1 bilde per side (Standard)",
            ["TwoPerPage"] = "2 bilder per side (To på arket)",
            ["FourPerPage"] = "4 bilder per side (2x2 rutenett)",

            // Page Numbers & Watermark
            ["Card_Decorations"] = "Sidetall & Vannmerke",
            ["EnablePageNumbers"] = "Legg til sidetall",
            ["PageNumberFormatLabel"] = "Format:",
            ["EnableWatermark"] = "Legg til diagonalt vannmerke",
            ["WatermarkTextLabel"] = "Vannmerketekst:",

            // Captions
            ["Card_Captions"] = "Bildetekst",
            ["ShowCaptions"] = "Vis filnavn / dato under hvert bilde",

            // Advanced & System
            ["Card_AdvancedFeatures"] = "System & Søkbar PDF",
            ["EnableOcr"] = "Gjør PDF søkbar (Windows OCR)",
            ["OcrDescription"] = "Legger inn usynlig tekstlag så du kan markere og søke i teksten med Ctrl+F.",
            ["EnableContextMenu"] = "Integrer i Windows Utforsker høyreklikkmeny",
            ["ContextMenuDescription"] = "Lar deg konvertere bilder og mapper direkte fra Windows Utforsker.",
            ["ContextMenuTitle"] = "Konverter til PDF med Img2Pdf",

            // Metadata
            ["Card_Metadata"] = "PDF-dokumentmetadata",
            ["DocTitle"] = "Dokumenttittel:",
            ["DocAuthor"] = "Forfatter:",
            ["DocSubject"] = "Emne / Tema:",

            // Output folder
            ["Card_OutputFolder"] = "Mappe for ferdige filer",
            ["Output_Source"] = "Samme mappe som kildefil",
            ["Output_Custom"] = "Spesifikk mappe",
            ["Browse"] = "Bla...",
            ["OpenWhenDone"] = "Åpne mappen når konvertering er ferdig",
            ["OpenPdfWhenDone"] = "Åpne PDF direkte når konvertering er ferdig",

            // Security
            ["Card_Security"] = "Sikkerhet & Passord",
            ["Security_Enable"] = "Passordbeskytt PDF",
            ["Security_PasswordLabel"] = "Passord for å åpne PDF:",
            ["Security_Desc"] = "Krypterer filen(e) slik at passord kreves for visning.",

            // Action buttons
            ["StartConversion"] = "Start konvertering",
            ["CancelConversion"] = "Avbryt konvertering",

            // Progress and summaries
            ["NoImagesSelected"] = "Ingen filer valgt",
            ["ImagesCount"] = "{0} fil{1} ({2})",
            ["PluralSuffix"] = "er",
            ["ReadyToConvert"] = "Klar til å konvertere",
            ["ImagesReady"] = "{0} filer klare",
            ["SearchingImages"] = "Søker etter filer...",
            ["ErrorReadingFiles"] = "Feil ved lesing av filer: {0}",
            ["ConvertingProgress"] = "Konverterer {0} av {1}: {2}",
            ["CompletedInSeconds"] = "Fullført på {0:F1} sekunder!",
            ["ConversionCancelled"] = "Konvertering avbrutt av bruker.",
            ["ConversionFailed"] = "Feil under konvertering: {0}",

            // Dialogs
            ["Dialog_SelectImagesTitle"] = "Velg bilder eller PDF-filer som skal konverteres",
            ["Dialog_ImageFilter"] = "Støttede filer (*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tiff;*.pdf)|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tiff;*.tif;*.pdf|Alle filer (*.*)|*.*",
            ["Dialog_SelectFolderTitle"] = "Velg mappe med bilder",
            ["Dialog_SelectOutputTitle"] = "Velg mappe for lagring av PDF-filer"
        };

        // -------------------------------------------------------------
        // GERMAN (Deutsch 🇩🇪)
        // -------------------------------------------------------------
        _translations["de"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AppTitle"] = "Img2Pdf - Bild zu PDF Konverter",
            ["AppHeaderTitle"] = "Img2Pdf • Hochwertiger Bildkonverter",
            ["ThemeToggle"] = "Design",
            ["Language"] = "Sprache",

            // Actions
            ["AddFiles"] = "Dateien hinzufügen",
            ["AddFolder"] = "Ordner hinzufügen",
            ["PasteImage"] = "Bild einfügen (Strg+V)",
            ["ExtractPdf"] = "PDF-Seiten extrahieren",
            ["ExtractPdfAsImages"] = "Als Bilder extrahieren (PNG)",
            ["Rotate"] = "90° drehen",
            ["MoveUp"] = "Nach oben",
            ["MoveDown"] = "Nach unten",
            ["Remove"] = "Entfernen",
            ["ClearList"] = "Liste leeren",
            ["QuickLook"] = "Vorschau",
            ["Close"] = "Schließen",
            ["Crop"] = "Zuschneiden",
            ["CropMode"] = "Zuschnitt",
            ["ResetCrop"] = "Zuschnitt zurücksetzen",
            ["QuickLookTip"] = "Tipp: Leertaste oder Esc zum Schließen • Pfeiltasten zum Blättern • R zum Drehen • C zum Zuschneiden",
            ["ToolTip_Previous"] = "Vorheriges (Pfeil nach links)",
            ["ToolTip_Next"] = "Nächstes (Pfeil nach rechts)",
            ["CropTrimLeft"] = "Links %:",
            ["CropTrimRight"] = "Rechts %:",
            ["CropTrimTop"] = "Oben %:",
            ["CropTrimBottom"] = "Unten %:",

            // Presets
            ["Card_Presets"] = "Schnellprofile",
            ["Preset_Standard"] = "Standard",
            ["Preset_Receipts"] = "Belege",
            ["Preset_PhotoAlbum"] = "Fotoalbum",
            ["Preset_Email"] = "E-Mail",
            ["Preset_DocScan"] = "Scan",

            // Empty drop area
            ["DropAreaTitle"] = "Bilder, PDFs hierher ziehen oder Strg+V drücken",
            ["DropAreaSubtitle"] = "Unterstützt JPG, PNG, BMP, WebP, TIFF und PDF • 100% verlustfreie Originalqualität",
            ["ChooseFiles"] = "Dateien auswählen...",
            ["ChooseFolder"] = "Ordner auswählen...",

            // Table headers
            ["Col_FileName"] = "Dateiname",
            ["Col_Dimensions"] = "Auflösung",
            ["Col_Size"] = "Größe",
            ["Col_Status"] = "Status",
            ["Col_Folder"] = "Ordner",

            // Status badges & types
            ["Status_Waiting"] = "Wartet",
            ["Status_Processing"] = "Wird verarbeitet",
            ["Status_Completed"] = "Abgeschlossen",
            ["Status_Failed"] = "Fehlgeschlagen",
            ["Dimensions_Unknown"] = "Unbekannt",
            ["PdfSinglePage"] = "1 Seite",
            ["PdfMultiPages"] = "{0} Seiten",
            ["PdfDocument"] = "PDF-Dokument",
            ["EstimatedPdfSize"] = "Geschätzte PDF-Größe: ~{0}",
            ["ExtractingPdfPages"] = "PDF-Seiten werden extrahiert...",
            ["ExtractingPdfImages"] = "Bilder aus PDF werden extrahiert...",
            ["PdfPagesExtracted"] = "{0} Seiten in Ordner extrahiert!",
            ["PdfImagesExtracted"] = "{0} Bilder in Ordner extrahiert!",
            ["Card_PdfExtraction"] = "PDF-Extraktionswerkzeuge",
            ["PageRangeLabel"] = "Seitenbereich (z. B. 1-3, 5):",
            ["PageRangePlaceholder"] = "Alle Seiten (leer lassen für alle)",

            // Conversion mode
            ["Card_ModeTitle"] = "Konvertierungsmodus",
            ["Mode_Individual"] = "Eine PDF pro Bild (Separat)",
            ["Mode_Merged"] = "Alle in eine PDF zusammenführen",
            ["MergedFileName"] = "PDF-Dateiname:",
            ["DefaultMergedFileName"] = "Bilder_Zusammengefuehrt.pdf",

            // Document Filters
            ["Card_Filter"] = "Bild- & Dokumentenfilter",
            ["Filter_Original"] = "Farbe (Original)",
            ["Filter_Grayscale"] = "Graustufen",
            ["Filter_BlackAndWhite"] = "Schwarz-Weiß Dokumentenscan",

            // Quality & Compression
            ["Card_Quality"] = "Qualität & Komprimierung",
            ["Quality_Lossless"] = "100% Verlustfrei (Originalqualität)",
            ["Quality_LosslessDesc"] = "Erhält die exakten Originalbilddaten ohne Qualitätsverlust.",
            ["Quality_Compressed"] = "Für E-Mail / Web optimiert (JPEG)",
            ["Quality_CompressedDesc"] = "Komprimiert Bilder für deutlich geringere Dateigröße.",
            ["CompressionLevel"] = "JPEG-Qualität:",

            // Page setup & Margins
            ["Card_PageSetup"] = "Seitenlayout & Ränder",
            ["Size_Original"] = "Original-Bildgröße (1:1)",
            ["Size_OriginalDesc"] = "Jede Seite entspricht exakt den Abmessungen des Bildes ohne Ränder.",
            ["Size_A4"] = "An A4 anpassen (Auto Hoch-/Querformat)",
            ["Size_A4Desc"] = "Skaliert das Bild proportional auf eine Standard-A4-Seite.",
            ["Size_Letter"] = "An US Letter anpassen",
            ["Section_Margins"] = "Seitenränder:",
            ["Margin_None"] = "0 mm (Randlos)",
            ["Margin_Compact"] = "10 mm (Kompakter Rand)",
            ["Margin_Normal"] = "20 mm (Standard-Dokumentenrand)",

            // Multi-image per page
            ["ImagesPerPage"] = "Bilder pro Seite:",
            ["OnePerPage"] = "1 Bild pro Seite (Standard)",
            ["TwoPerPage"] = "2 Bilder pro Seite (Nebeneinander)",
            ["FourPerPage"] = "4 Bilder pro Seite (2x2 Raster)",

            // Page Numbers & Watermark
            ["Card_Decorations"] = "Seitenzahlen & Wasserzeichen",
            ["EnablePageNumbers"] = "Seitenzahlen hinzufügen",
            ["PageNumberFormatLabel"] = "Format:",
            ["EnableWatermark"] = "Diagonales Wasserzeichen",
            ["WatermarkTextLabel"] = "Wasserzeichen-Text:",

            // Captions
            ["Card_Captions"] = "Bildbeschriftung",
            ["ShowCaptions"] = "Dateiname / Datum unter jedem Bild drucken",

            // Advanced & System
            ["Card_AdvancedFeatures"] = "System & Durchsuchbare PDF",
            ["EnableOcr"] = "Durchsuchbare PDF (Windows OCR)",
            ["OcrDescription"] = "Bettet unsichtbare Textebene ein, um Text mit Strg+F zu durchsuchen.",
            ["EnableContextMenu"] = "Zum Windows Explorer Kontextmenü hinzufügen",
            ["ContextMenuDescription"] = "Ermöglicht das Konvertieren direkt aus dem Windows Explorer.",
            ["ContextMenuTitle"] = "Mit Img2Pdf in PDF konvertieren",

            // Metadata
            ["Card_Metadata"] = "PDF-Dokument-Metadaten",
            ["DocTitle"] = "Dokumenttitel:",
            ["DocAuthor"] = "Autor:",
            ["DocSubject"] = "Thema / Betreff:",

            // Output folder
            ["Card_OutputFolder"] = "Ausgabeordner",
            ["Output_Source"] = "Gleicher Ordner wie Quelldatei",
            ["Output_Custom"] = "Benutzerdefinierter Ordner",
            ["Browse"] = "Durchsuchen...",
            ["OpenWhenDone"] = "Ordner nach Fertigstellung öffnen",
            ["OpenPdfWhenDone"] = "PDF direkt nach Fertigstellung öffnen",

            // Security
            ["Card_Security"] = "Sicherheit & Passwort",
            ["Security_Enable"] = "PDF mit Passwort schützen",
            ["Security_PasswordLabel"] = "Passwort zum Öffnen der PDF:",
            ["Security_Desc"] = "Verschlüsselt die Datei(en), sodass ein Passwort erforderlich ist.",

            // Action buttons
            ["StartConversion"] = "Konvertierung starten",
            ["CancelConversion"] = "Konvertierung abbrechen",

            // Progress and summaries
            ["NoImagesSelected"] = "Keine Dateien ausgewählt",
            ["ImagesCount"] = "{0} Datei{1} ({2})",
            ["PluralSuffix"] = "en",
            ["ReadyToConvert"] = "Bereit zur Konvertierung",
            ["ImagesReady"] = "{0} Dateien bereit",
            ["SearchingImages"] = "Suche nach Dateien...",
            ["ErrorReadingFiles"] = "Fehler beim Lesen der Dateien: {0}",
            ["ConvertingProgress"] = "Konvertiere {0} von {1}: {2}",
            ["CompletedInSeconds"] = "Abgeschlossen in {0:F1} Sekunden!",
            ["ConversionCancelled"] = "Konvertierung vom Benutzer abgebrochen.",
            ["ConversionFailed"] = "Konvertierungsfehler: {0}",

            // Dialogs
            ["Dialog_SelectImagesTitle"] = "Bilder oder PDF-Dateien zum Konvertieren auswählen",
            ["Dialog_ImageFilter"] = "Unterstützte Dateien (*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tiff;*.pdf)|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tiff;*.tif;*.pdf|Alle Dateien (*.*)|*.*",
            ["Dialog_SelectFolderTitle"] = "Ordner mit Bildern auswählen",
            ["Dialog_SelectOutputTitle"] = "Zielordner für PDF-Dateien auswählen"
        };

        // -------------------------------------------------------------
        // SPANISH (Español 🇪🇸)
        // -------------------------------------------------------------
        _translations["es"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AppTitle"] = "Img2Pdf - Conversor de Imagen a PDF",
            ["AppHeaderTitle"] = "Img2Pdf • Conversor de imágenes de alta calidad",
            ["ThemeToggle"] = "Tema",
            ["Language"] = "Idioma",

            // Actions
            ["AddFiles"] = "Añadir archivos",
            ["AddFolder"] = "Añadir carpeta",
            ["PasteImage"] = "Pegar imagen (Ctrl+V)",
            ["ExtractPdf"] = "Extraer páginas PDF",
            ["ExtractPdfAsImages"] = "Extraer como imágenes (PNG)",
            ["Rotate"] = "Girar 90°",
            ["MoveUp"] = "Subir",
            ["MoveDown"] = "Bajar",
            ["Remove"] = "Eliminar",
            ["ClearList"] = "Limpiar lista",
            ["QuickLook"] = "Vista previa",
            ["Close"] = "Cerrar",
            ["Crop"] = "Recortar",
            ["CropMode"] = "Recorte",
            ["ResetCrop"] = "Restablecer recorte",
            ["QuickLookTip"] = "Consejo: Espacio o Esc para cerrar • Flechas para pasar página • R para rotar • C para recortar",
            ["ToolTip_Previous"] = "Anterior (Flecha izquierda)",
            ["ToolTip_Next"] = "Siguiente (Flecha derecha)",
            ["CropTrimLeft"] = "Izquierda %:",
            ["CropTrimRight"] = "Derecha %:",
            ["CropTrimTop"] = "Arriba %:",
            ["CropTrimBottom"] = "Abajo %:",

            // Presets
            ["Card_Presets"] = "Perfiles rápidos",
            ["Preset_Standard"] = "Estándar",
            ["Preset_Receipts"] = "Recibos",
            ["Preset_PhotoAlbum"] = "Álbum de fotos",
            ["Preset_Email"] = "Correo",
            ["Preset_DocScan"] = "Escaneo",

            // Empty drop area
            ["DropAreaTitle"] = "Arrastra imágenes, PDFs o pulsa Ctrl+V",
            ["DropAreaSubtitle"] = "Compatible con JPG, PNG, BMP, WebP, TIFF y PDF • Calidad original 100% sin pérdidas",
            ["ChooseFiles"] = "Seleccionar archivos...",
            ["ChooseFolder"] = "Seleccionar carpeta...",

            // Table headers
            ["Col_FileName"] = "Nombre de archivo",
            ["Col_Dimensions"] = "Resolución",
            ["Col_Size"] = "Tamaño",
            ["Col_Status"] = "Estado",
            ["Col_Folder"] = "Carpeta",

            // Status badges & types
            ["Status_Waiting"] = "En espera",
            ["Status_Processing"] = "Procesando",
            ["Status_Completed"] = "Completado",
            ["Status_Failed"] = "Error",
            ["Dimensions_Unknown"] = "Desconocido",
            ["PdfSinglePage"] = "1 página",
            ["PdfMultiPages"] = "{0} páginas",
            ["PdfDocument"] = "Documento PDF",
            ["EstimatedPdfSize"] = "Tamaño est. PDF: ~{0}",
            ["ExtractingPdfPages"] = "Extrayendo páginas PDF...",
            ["ExtractingPdfImages"] = "Extrayendo imágenes del PDF...",
            ["PdfPagesExtracted"] = "¡{0} páginas extraídas a la carpeta!",
            ["PdfImagesExtracted"] = "¡{0} imágenes extraídas a la carpeta!",
            ["Card_PdfExtraction"] = "Herramientas de extracción de PDF",
            ["PageRangeLabel"] = "Rango de páginas (ej. 1-3, 5):",
            ["PageRangePlaceholder"] = "Todas las páginas (dejar vacío)",

            // Conversion mode
            ["Card_ModeTitle"] = "Modo de conversión",
            ["Mode_Individual"] = "Un PDF por imagen (Separados)",
            ["Mode_Merged"] = "Combinar todas en un solo PDF",
            ["MergedFileName"] = "Nombre del archivo PDF:",
            ["DefaultMergedFileName"] = "Imagenes_Combinadas.pdf",

            // Document Filters
            ["Card_Filter"] = "Filtros de documento",
            ["Filter_Original"] = "Color (Original)",
            ["Filter_Grayscale"] = "Escala de grises",
            ["Filter_BlackAndWhite"] = "B&N Escaneo de documento",

            // Quality & Compression
            ["Card_Quality"] = "Calidad y compresión",
            ["Quality_Lossless"] = "100% Sin pérdidas (Calidad original)",
            ["Quality_LosslessDesc"] = "Conserva los datos exactos de la imagen original sin pérdida de calidad.",
            ["Quality_Compressed"] = "Optimizado para correo/web (JPEG)",
            ["Quality_CompressedDesc"] = "Comprime las imágenes para un tamaño de archivo mucho menor.",
            ["CompressionLevel"] = "Calidad JPEG:",

            // Page setup & Margins
            ["Card_PageSetup"] = "Diseño de página y márgenes",
            ["Size_Original"] = "Tamaño original de imagen (1:1)",
            ["Size_OriginalDesc"] = "Cada página coincide con las dimensiones exactas de la imagen sin bordes.",
            ["Size_A4"] = "Ajustar a A4 (Auto vertical/horizontal)",
            ["Size_A4Desc"] = "Escala la imagen proporcionalmente a una hoja A4 estándar.",
            ["Size_Letter"] = "Ajustar a US Letter",
            ["Section_Margins"] = "Márgenes de página:",
            ["Margin_None"] = "0 mm (Sin bordes)",
            ["Margin_Compact"] = "10 mm (Margen compacto)",
            ["Margin_Normal"] = "20 mm (Margen estándar)",

            // Multi-image per page
            ["ImagesPerPage"] = "Imágenes por página:",
            ["OnePerPage"] = "1 imagen por página (Estándar)",
            ["TwoPerPage"] = "2 imágenes por página (2 en 1)",
            ["FourPerPage"] = "4 imágenes por página (Cuadrícula 2x2)",

            // Page Numbers & Watermark
            ["Card_Decorations"] = "Números de página y marca de agua",
            ["EnablePageNumbers"] = "Añadir números de página",
            ["PageNumberFormatLabel"] = "Formato:",
            ["EnableWatermark"] = "Marca de agua diagonal",
            ["WatermarkTextLabel"] = "Texto de marca de agua:",

            // Captions
            ["Card_Captions"] = "Subtítulo de imagen",
            ["ShowCaptions"] = "Imprimir nombre de archivo / fecha debajo de cada imagen",

            // Advanced & System
            ["Card_AdvancedFeatures"] = "Sistema y PDF con búsqueda",
            ["EnableOcr"] = "PDF con búsqueda (Windows OCR)",
            ["OcrDescription"] = "Incrusta capa de texto invisible para seleccionar y buscar con Ctrl+F.",
            ["EnableContextMenu"] = "Añadir al menú contextual de Windows",
            ["ContextMenuDescription"] = "Permite convertir imágenes y carpetas directamente desde Windows.",
            ["ContextMenuTitle"] = "Convertir a PDF con Img2Pdf",

            // Metadata
            ["Card_Metadata"] = "Metadatos del PDF",
            ["DocTitle"] = "Título del documento:",
            ["DocAuthor"] = "Autor:",
            ["DocSubject"] = "Asunto / Tema:",

            // Output folder
            ["Card_OutputFolder"] = "Destino de salida",
            ["Output_Source"] = "Misma carpeta que la imagen de origen",
            ["Output_Custom"] = "Carpeta específica",
            ["Browse"] = "Examinar...",
            ["OpenWhenDone"] = "Abrir carpeta al completar",
            ["OpenPdfWhenDone"] = "Abrir el PDF directamente al finalizar",

            // Security
            ["Card_Security"] = "Seguridad y contraseña",
            ["Security_Enable"] = "Proteger PDF con contraseña",
            ["Security_PasswordLabel"] = "Contraseña para abrir el PDF:",
            ["Security_Desc"] = "Cifra el archivo para que se requiera contraseña para abrirlo.",

            // Action buttons
            ["StartConversion"] = "Iniciar conversión",
            ["CancelConversion"] = "Cancelar conversión",

            // Progress and summaries
            ["NoImagesSelected"] = "No hay archivos seleccionados",
            ["ImagesCount"] = "{0} archivo{1} ({2})",
            ["PluralSuffix"] = "s",
            ["ReadyToConvert"] = "Listo para convertir",
            ["ImagesReady"] = "{0} archivos listos",
            ["SearchingImages"] = "Buscando archivos...",
            ["ErrorReadingFiles"] = "Error al leer archivos: {0}",
            ["ConvertingProgress"] = "Convirtiendo {0} de {1}: {2}",
            ["CompletedInSeconds"] = "¡Completado en {0:F1} segundos!",
            ["ConversionCancelled"] = "Conversión cancelada por el usuario.",
            ["ConversionFailed"] = "Error de conversión: {0}",

            // Dialogs
            ["Dialog_SelectImagesTitle"] = "Seleccionar imágenes o archivos PDF para convertir",
            ["Dialog_ImageFilter"] = "Archivos compatibles (*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tiff;*.pdf)|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tiff;*.tif;*.pdf|Todos los archivos (*.*)|*.*",
            ["Dialog_SelectFolderTitle"] = "Seleccionar carpeta con imágenes",
            ["Dialog_SelectOutputTitle"] = "Seleccionar carpeta de destino para archivos PDF"
        };
    }
}

[MarkupExtensionReturnType(typeof(string))]
public class TranslateExtension : MarkupExtension
{
    public string Key { get; set; }

    public TranslateExtension()
    {
        Key = string.Empty;
    }

    public TranslateExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
            return string.Empty;

        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationService.Instance,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
