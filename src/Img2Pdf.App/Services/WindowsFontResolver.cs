using System.Collections.Concurrent;
using System.IO;
using PdfSharp.Fonts;

namespace Img2Pdf.App.Services;

/// <summary>
/// Thread-safe font resolver for PDFsharp 6 Core build on Windows.
/// Resolves Segoe UI, Arial, Calibri, Tahoma, Times New Roman, and Consolas directly from C:\Windows\Fonts.
/// </summary>
public sealed class WindowsFontResolver : IFontResolver
{
    public static readonly WindowsFontResolver Instance = new();
    private static readonly ConcurrentDictionary<string, byte[]> FontCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object InitLock = new();
    private static bool _isInitialized;

    public static void EnsureRegistered()
    {
        if (_isInitialized) return;
        lock (InitLock)
        {
            if (_isInitialized) return;
            try
            {
                if (GlobalFontSettings.FontResolver == null)
                {
                    GlobalFontSettings.FontResolver = Instance;
                }
            }
            catch
            {
                // Ignore if already assigned or locked by PDFsharp
            }
            _isInitialized = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        string norm = (familyName ?? "Segoe UI").Trim().ToLowerInvariant();

        string prefix = norm switch
        {
            "arial" or "helvetica" or "sans-serif" => "arial",
            "times new roman" or "times" or "serif" => "times",
            "courier new" or "courier" or "consolas" or "monospace" => "consola",
            "calibri" => "calibri",
            "tahoma" => "tahoma",
            _ => "segoeui"
        };

        string style = (isBold, isItalic) switch
        {
            (true, true) => "bi",
            (true, false) => "b",
            (false, true) => "i",
            _ => "r"
        };

        string faceName = $"{prefix}#{style}";

        // Check if exact bold/italic variant exists; if not, fallback to regular and let PDFsharp simulate style
        if (FindFontFilePath(faceName) != null)
        {
            return new FontResolverInfo(faceName);
        }

        return new FontResolverInfo($"{prefix}#r", isBold, isItalic);
    }

    public byte[]? GetFont(string faceName)
    {
        return FontCache.GetOrAdd(faceName, LoadFontBytes);
    }

    private static byte[] LoadFontBytes(string faceName)
    {
        string? path = FindFontFilePath(faceName) ?? FindFontFilePath("segoeui#r");
        if (path != null && File.Exists(path))
        {
            return File.ReadAllBytes(path);
        }

        string fontsDir = GetSystemFontsDirectory();
        if (Directory.Exists(fontsDir))
        {
            var anyTtf = Directory.EnumerateFiles(fontsDir, "*.ttf").FirstOrDefault();
            if (anyTtf != null)
            {
                return File.ReadAllBytes(anyTtf);
            }
        }

        throw new FileNotFoundException($"Unable to find TrueType font '{faceName}' in '{fontsDir}'.");
    }

    private static string? FindFontFilePath(string faceName)
    {
        string[] candidates = faceName.ToLowerInvariant() switch
        {
            "segoeui#b"  => ["segoeuib.ttf", "arialbd.ttf", "calibrib.ttf", "tahomabd.ttf", "segoeui.ttf", "arial.ttf"],
            "segoeui#i"  => ["segoeuii.ttf", "ariali.ttf", "calibrii.ttf", "segoeui.ttf", "arial.ttf"],
            "segoeui#bi" => ["segoeuiz.ttf", "arialbi.ttf", "calibriz.ttf", "segoeuib.ttf", "segoeui.ttf", "arial.ttf"],
            "segoeui#r"  => ["segoeui.ttf", "arial.ttf", "calibri.ttf", "tahoma.ttf"],

            "arial#b"    => ["arialbd.ttf", "segoeuib.ttf", "calibrib.ttf", "arial.ttf", "segoeui.ttf"],
            "arial#i"    => ["ariali.ttf", "segoeuii.ttf", "calibrii.ttf", "arial.ttf", "segoeui.ttf"],
            "arial#bi"   => ["arialbi.ttf", "segoeuiz.ttf", "calibriz.ttf", "arialbd.ttf", "arial.ttf"],
            "arial#r"    => ["arial.ttf", "segoeui.ttf", "calibri.ttf", "tahoma.ttf"],

            "calibri#b"  => ["calibrib.ttf", "segoeuib.ttf", "arialbd.ttf", "calibri.ttf", "segoeui.ttf"],
            "calibri#i"  => ["calibrii.ttf", "segoeuii.ttf", "ariali.ttf", "calibri.ttf", "segoeui.ttf"],
            "calibri#bi" => ["calibriz.ttf", "segoeuiz.ttf", "arialbi.ttf", "calibrib.ttf", "calibri.ttf"],
            "calibri#r"  => ["calibri.ttf", "segoeui.ttf", "arial.ttf", "tahoma.ttf"],

            "tahoma#b"   => ["tahomabd.ttf", "segoeuib.ttf", "arialbd.ttf", "tahoma.ttf", "segoeui.ttf"],
            "tahoma#r"   => ["tahoma.ttf", "segoeui.ttf", "arial.ttf"],

            "times#b"    => ["timesbd.ttf", "segoeuib.ttf", "arialbd.ttf", "times.ttf", "segoeui.ttf"],
            "times#i"    => ["timesi.ttf", "segoeuii.ttf", "ariali.ttf", "times.ttf", "segoeui.ttf"],
            "times#bi"   => ["timesbi.ttf", "segoeuiz.ttf", "arialbi.ttf", "timesbd.ttf", "times.ttf"],
            "times#r"    => ["times.ttf", "segoeui.ttf", "arial.ttf"],

            "consola#b"  => ["consolab.ttf", "courbd.ttf", "segoeuib.ttf", "consola.ttf", "segoeui.ttf"],
            "consola#i"  => ["consolai.ttf", "couri.ttf", "segoeuii.ttf", "consola.ttf", "segoeui.ttf"],
            "consola#bi" => ["consolaz.ttf", "courbi.ttf", "segoeuiz.ttf", "consolab.ttf", "consola.ttf"],
            "consola#r"  => ["consola.ttf", "cour.ttf", "segoeui.ttf", "arial.ttf"],

            _ => ["segoeui.ttf", "arial.ttf", "calibri.ttf", "tahoma.ttf"]
        };

        string sysFonts = GetSystemFontsDirectory();
        foreach (var file in candidates)
        {
            string fullPath = Path.Combine(sysFonts, file);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        string userFonts = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Fonts");
        if (Directory.Exists(userFonts))
        {
            foreach (var file in candidates)
            {
                string fullPath = Path.Combine(userFonts, file);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }

    private static string GetSystemFontsDirectory()
    {
        string dir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
        {
            return dir;
        }

        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(winDir))
        {
            string fallback = Path.Combine(winDir, "Fonts");
            if (Directory.Exists(fallback))
            {
                return fallback;
            }
        }

        return @"C:\Windows\Fonts";
    }
}
