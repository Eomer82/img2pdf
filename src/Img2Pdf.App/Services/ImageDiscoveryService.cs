using System.IO;
using System.Windows.Media.Imaging;
using Img2Pdf.App.Models;

namespace Img2Pdf.App.Services;

public class ImageDiscoveryService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".webp",
        ".tiff",
        ".tif"
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
                        // Ignore directories without access
                    }
                }
            }
        }, cancellationToken);

        var items = new List<ImageFileItem>();

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

                    InspectImageMetadata(item);
                    items.Add(item);
                }
                catch
                {
                    // Ignore files that fail to inspect
                }
            }
        }, cancellationToken);

        return items;
    }

    private static void InspectImageMetadata(ImageFileItem item)
    {
        try
        {
            using var stream = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);

            if (decoder.Frames.Count > 0)
            {
                var frame = decoder.Frames[0];
                int pixelWidth = frame.PixelWidth;
                int pixelHeight = frame.PixelHeight;

                int exifOrientation = 1;
                if (frame.Metadata is BitmapMetadata metadata)
                {
                    if (metadata.ContainsQuery("/app1/ifd/{ushort=274}"))
                    {
                        var val = metadata.GetQuery("/app1/ifd/{ushort=274}");
                        if (val is ushort u) exifOrientation = u;
                        else if (val is int i) exifOrientation = i;
                    }
                    else if (metadata.ContainsQuery("/ifd/{ushort=274}"))
                    {
                        var val = metadata.GetQuery("/ifd/{ushort=274}");
                        if (val is ushort u) exifOrientation = u;
                        else if (val is int i) exifOrientation = i;
                    }
                }

                int rotation = exifOrientation switch
                {
                    3 => 180,
                    6 => 90,
                    8 => 270,
                    _ => 0
                };

                item.ExifRotation = rotation;

                if (rotation == 90 || rotation == 270)
                {
                    item.Dimensions = $"{pixelHeight} × {pixelWidth}";
                }
                else
                {
                    item.Dimensions = $"{pixelWidth} × {pixelHeight}";
                }
            }
        }
        catch
        {
            item.Dimensions = LocalizationService.GetString("Dimensions_Unknown");
        }

        try
        {
            var thumb = new BitmapImage();
            thumb.BeginInit();
            thumb.UriSource = new Uri(item.FilePath);
            thumb.DecodePixelWidth = 80;
            thumb.CacheOption = BitmapCacheOption.OnLoad;
            thumb.CreateOptions = BitmapCreateOptions.DelayCreation;
            thumb.EndInit();
            thumb.Freeze();
            item.Thumbnail = thumb;
        }
        catch
        {
            item.Thumbnail = null;
        }
    }
}
