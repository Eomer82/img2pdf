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
        new("nb", "Norsk", "Norwegian", "🇳🇴")
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
            savedCode = (sysLang == "nb" || sysLang == "no" || sysLang == "nn") ? "nb" : "en";
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
            ["AddFiles"] = "Add images",
            ["AddFolder"] = "Add folder",
            ["Rotate"] = "Rotate 90°",
            ["MoveUp"] = "Move up",
            ["MoveDown"] = "Move down",
            ["Remove"] = "Remove",
            ["ClearList"] = "Clear list",

            // Empty drop area
            ["DropAreaTitle"] = "Drag and drop images or folders here",
            ["DropAreaSubtitle"] = "Supports JPG, JPEG, PNG, BMP, WebP, and TIFF • 100% lossless original quality",
            ["ChooseFiles"] = "Select files...",
            ["ChooseFolder"] = "Select folder...",

            // Table headers
            ["Col_FileName"] = "File Name",
            ["Col_Dimensions"] = "Resolution",
            ["Col_Size"] = "Size",
            ["Col_Status"] = "Status",
            ["Col_Folder"] = "Folder",

            // Status badges
            ["Status_Waiting"] = "Waiting",
            ["Status_Processing"] = "Converting",
            ["Status_Completed"] = "Completed",
            ["Status_Failed"] = "Failed",
            ["Dimensions_Unknown"] = "Unknown",

            // Conversion mode
            ["Card_ModeTitle"] = "Conversion Mode",
            ["Mode_Individual"] = "One PDF per image (Separate)",
            ["Mode_Merged"] = "Merge all into a single PDF",
            ["MergedFileName"] = "PDF File Name:",
            ["DefaultMergedFileName"] = "Images_Merged.pdf",

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

            // Output folder
            ["Card_OutputFolder"] = "Output Destination",
            ["Output_Source"] = "Same folder as source image",
            ["Output_Custom"] = "Specific folder",
            ["Browse"] = "Browse...",
            ["OpenWhenDone"] = "Open folder when conversion completes",

            // Security
            ["Card_Security"] = "Security & Encryption",
            ["Security_Enable"] = "Password protect PDF",
            ["Security_PasswordLabel"] = "Password to open PDF:",
            ["Security_Desc"] = "Encrypts the PDF so a password is required to view.",

            // Action buttons
            ["StartConversion"] = "Start conversion",
            ["CancelConversion"] = "Cancel conversion",

            // Progress and summaries
            ["NoImagesSelected"] = "No images selected",
            ["ImagesCount"] = "{0} image{1} ({2})",
            ["PluralSuffix"] = "s",
            ["ReadyToConvert"] = "Ready to convert",
            ["ImagesReady"] = "{0} images ready",
            ["SearchingImages"] = "Scanning for image files...",
            ["ErrorReadingFiles"] = "Error reading files: {0}",
            ["ConvertingProgress"] = "Converting {0} of {1}: {2}",
            ["CompletedInSeconds"] = "Completed in {0:F1} seconds!",
            ["ConversionCancelled"] = "Conversion cancelled by user.",
            ["ConversionFailed"] = "Conversion error: {0}",

            // Dialogs
            ["Dialog_SelectImagesTitle"] = "Select images to convert",
            ["Dialog_ImageFilter"] = "Image files (*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tiff;*.tif|All files (*.*)|*.*",
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
            ["AddFiles"] = "Legg til bilder",
            ["AddFolder"] = "Legg til mappe",
            ["Rotate"] = "Roter 90°",
            ["MoveUp"] = "Flytt opp",
            ["MoveDown"] = "Flytt ned",
            ["Remove"] = "Fjern",
            ["ClearList"] = "Tøm liste",

            // Empty drop area
            ["DropAreaTitle"] = "Dra og slipp bilder eller mapper her",
            ["DropAreaSubtitle"] = "Støtter JPG, JPEG, PNG, BMP, WebP og TIFF • Bevarer 100 % original kvalitet",
            ["ChooseFiles"] = "Velg filer...",
            ["ChooseFolder"] = "Velg mappe...",

            // Table headers
            ["Col_FileName"] = "Filnavn",
            ["Col_Dimensions"] = "Oppløsning",
            ["Col_Size"] = "Størrelse",
            ["Col_Status"] = "Status",
            ["Col_Folder"] = "Mappe",

            // Status badges
            ["Status_Waiting"] = "Venter",
            ["Status_Processing"] = "Behandler",
            ["Status_Completed"] = "Fullført",
            ["Status_Failed"] = "Feilet",
            ["Dimensions_Unknown"] = "Ukjent",

            // Conversion mode
            ["Card_ModeTitle"] = "Konverteringsmodus",
            ["Mode_Individual"] = "Én PDF per bilde (Separat)",
            ["Mode_Merged"] = "Slå sammen alle til én PDF",
            ["MergedFileName"] = "PDF-filnavn:",
            ["DefaultMergedFileName"] = "Bilder_Samlet.pdf",

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

            // Output folder
            ["Card_OutputFolder"] = "Mappe for ferdige filer",
            ["Output_Source"] = "Samme mappe som kildefil",
            ["Output_Custom"] = "Spesifikk mappe",
            ["Browse"] = "Bla...",
            ["OpenWhenDone"] = "Åpne mappen når konvertering er ferdig",

            // Security
            ["Card_Security"] = "Sikkerhet & Passord",
            ["Security_Enable"] = "Passordbeskytt PDF",
            ["Security_PasswordLabel"] = "Passord for å åpne PDF:",
            ["Security_Desc"] = "Krypterer filen(e) slik at passord kreves for visning.",

            // Action buttons
            ["StartConversion"] = "Start konvertering",
            ["CancelConversion"] = "Avbryt konvertering",

            // Progress and summaries
            ["NoImagesSelected"] = "Ingen bilder valgt",
            ["ImagesCount"] = "{0} bilde{1} ({2})",
            ["PluralSuffix"] = "r",
            ["ReadyToConvert"] = "Klar til å konvertere",
            ["ImagesReady"] = "{0} bilder klare",
            ["SearchingImages"] = "Søker etter bildefiler...",
            ["ErrorReadingFiles"] = "Feil ved lesing av filer: {0}",
            ["ConvertingProgress"] = "Konverterer {0} av {1}: {2}",
            ["CompletedInSeconds"] = "Fullført på {0:F1} sekunder!",
            ["ConversionCancelled"] = "Konvertering avbrutt av bruker.",
            ["ConversionFailed"] = "Feil under konvertering: {0}",

            // Dialogs
            ["Dialog_SelectImagesTitle"] = "Velg bilder som skal konverteres",
            ["Dialog_ImageFilter"] = "Bildefiler (*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tiff;*.tif|Alle filer (*.*)|*.*",
            ["Dialog_SelectFolderTitle"] = "Velg mappe med bilder",
            ["Dialog_SelectOutputTitle"] = "Velg mappe for lagring av PDF-filer"
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
