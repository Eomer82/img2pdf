using System.IO;
using Img2Pdf.App.Models;
using PdfSharp.Drawing;

namespace Img2Pdf.App.Services;

public class ImageDiscoveryService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp"
    };

    public static bool IsSupportedImage(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(ext) && SupportedExtensions.Contains(ext);
    }

    public static async Task<List<ImageFileItem>> DiscoverImagesAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        var discoveredFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await Task.Run(() =>
        {
            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(path))
                {
                    if (IsSupportedImage(path))
                    {
                        discoveredFiles.Add(Path.GetFullPath(path));
                    }
                }
                else if (Directory.Exists(path))
                {
                    try
                    {
                        var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                            .Where(IsSupportedImage);

                        foreach (var file in files)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            discoveredFiles.Add(Path.GetFullPath(file));
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Hopper over mapper uten tilgang
                    }
                }
            }
        }, cancellationToken);

        var items = new List<ImageFileItem>();

        // Les filinformasjon og dimensjoner asynkront
        await Task.Run(() =>
        {
            foreach (var filePath in discoveredFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var item = new ImageFileItem
                    {
                        FilePath = filePath,
                        FileName = fileInfo.Name,
                        Directory = fileInfo.DirectoryName ?? string.Empty,
                        FileSizeBytes = fileInfo.Length
                    };

                    item.Dimensions = TryGetDimensions(filePath);
                    items.Add(item);
                }
                catch
                {
                    // Ignorer ugyldige filer
                }
            }
        }, cancellationToken);

        return items;
    }

    private static string TryGetDimensions(string filePath)
    {
        try
        {
            using var img = XImage.FromFile(filePath);
            return $"{img.PixelWidth} × {img.PixelHeight}";
        }
        catch
        {
            return "Ukjent";
        }
    }
}
