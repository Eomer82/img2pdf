using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Img2Pdf.App.Models;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Img2Pdf.App.Services;

public record ConversionProgressReport(int CurrentIndex, int TotalCount, string CurrentFile, double Percentage);

public class PdfConversionService
{
    public static async Task ConvertAsync(
        IReadOnlyList<ImageFileItem> items,
        ConversionSettings settings,
        IProgress<ConversionProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0) return;

        if (settings.OutputMode == OutputMode.MergedPdf)
        {
            await ConvertToSingleMergedPdfAsync(items, settings, progress, cancellationToken);
        }
        else
        {
            await ConvertToIndividualPdfsAsync(items, settings, progress, cancellationToken);
        }
    }

    private static async Task ConvertToIndividualPdfsAsync(
        IReadOnlyList<ImageFileItem> items,
        ConversionSettings settings,
        IProgress<ConversionProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        int total = items.Count;
        int completed = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1),
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(items, parallelOptions, async (item, token) =>
        {
            token.ThrowIfCancellationRequested();

            try
            {
                item.Status = ConversionStatus.Behandler;

                string targetFolder = settings.UseCustomOutputFolder && !string.IsNullOrWhiteSpace(settings.CustomOutputFolder)
                    ? settings.CustomOutputFolder
                    : item.Directory;

                Directory.CreateDirectory(targetFolder);

                string baseName = Path.GetFileNameWithoutExtension(item.FilePath);
                string outputPath = Path.Combine(targetFolder, $"{baseName}.pdf");

                await Task.Run(() =>
                {
                    using var document = new PdfDocument();
                    AddImagePage(document, item, settings);
                    ApplySecurity(document, settings);
                    document.Save(outputPath);
                }, token);

                item.Status = ConversionStatus.Fullført;
                item.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                item.Status = ConversionStatus.Feilet;
                item.ErrorMessage = ex.Message;
            }
            finally
            {
                int current = Interlocked.Increment(ref completed);
                progress?.Report(new ConversionProgressReport(
                    current,
                    total,
                    item.FileName,
                    (double)current / total * 100.0
                ));
            }
        });
    }

    private static async Task ConvertToSingleMergedPdfAsync(
        IReadOnlyList<ImageFileItem> items,
        ConversionSettings settings,
        IProgress<ConversionProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        int total = items.Count;

        string targetFolder = settings.UseCustomOutputFolder && !string.IsNullOrWhiteSpace(settings.CustomOutputFolder)
            ? settings.CustomOutputFolder
            : items[0].Directory;

        Directory.CreateDirectory(targetFolder);

        string fileName = settings.MergedFileName.Trim();
        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".pdf";
        }
        string outputPath = Path.Combine(targetFolder, fileName);

        await Task.Run(() =>
        {
            using var document = new PdfDocument();

            for (int i = 0; i < items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var item = items[i];
                item.Status = ConversionStatus.Behandler;

                try
                {
                    AddImagePage(document, item, settings);
                    item.Status = ConversionStatus.Fullført;
                    item.ErrorMessage = null;
                }
                catch (Exception ex)
                {
                    item.Status = ConversionStatus.Feilet;
                    item.ErrorMessage = ex.Message;
                }

                progress?.Report(new ConversionProgressReport(
                    i + 1,
                    total,
                    item.FileName,
                    (double)(i + 1) / total * 100.0
                ));
            }

            ApplySecurity(document, settings);
            document.Save(outputPath);
        }, cancellationToken);
    }

    private static void AddImagePage(PdfDocument document, ImageFileItem item, ConversionSettings settings)
    {
        var page = document.AddPage();
        IDisposable? cleanup = null;
        XImage? xImage = null;

        try
        {
            xImage = LoadPreparedImage(item, settings, out cleanup);

            if (settings.PageSizeMode == PageSizeMode.OriginalImageSize)
            {
                page.Width = XUnit.FromPoint(xImage.PointWidth);
                page.Height = XUnit.FromPoint(xImage.PointHeight);

                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawImage(xImage, 0, 0, page.Width.Point, page.Height.Point);
            }
            else
            {
                page.Size = settings.PageSizeMode == PageSizeMode.Letter_AutoOrientation ? PageSize.Letter : PageSize.A4;

                page.Orientation = xImage.PixelWidth > xImage.PixelHeight
                    ? PageOrientation.Landscape
                    : PageOrientation.Portrait;

                double margin = settings.MarginMode switch
                {
                    PageMarginMode.None => 0.0,
                    PageMarginMode.Normal => 56.69, // 20 mm
                    _ => 28.35                     // 10 mm
                };

                double availableWidth = Math.Max(1.0, page.Width.Point - (2 * margin));
                double availableHeight = Math.Max(1.0, page.Height.Point - (2 * margin));

                double imageRatio = (double)xImage.PixelWidth / xImage.PixelHeight;

                double targetWidth = availableWidth;
                double targetHeight = targetWidth / imageRatio;

                if (targetHeight > availableHeight)
                {
                    targetHeight = availableHeight;
                    targetWidth = targetHeight * imageRatio;
                }

                double x = margin + (availableWidth - targetWidth) / 2.0;
                double y = margin + (availableHeight - targetHeight) / 2.0;

                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawImage(xImage, x, y, targetWidth, targetHeight);
            }
        }
        finally
        {
            xImage?.Dispose();
            cleanup?.Dispose();
        }
    }

    private static XImage LoadPreparedImage(ImageFileItem item, ConversionSettings settings, out IDisposable? cleanup)
    {
        int totalRotation = item.TotalRotation;
        string ext = Path.GetExtension(item.FilePath).ToLowerInvariant();
        bool isStandardJpegOrPng = ext == ".jpg" || ext == ".jpeg" || ext == ".png";

        // Direct lossless embedding if standard format with zero rotation and lossless quality mode
        if (isStandardJpegOrPng && totalRotation == 0 && settings.QualityMode == QualityMode.Lossless)
        {
            cleanup = null;
            return XImage.FromFile(item.FilePath);
        }

        // Load via WPF/WIC decoder to support rotation, WebP, TIFF, BMP, and compression
        using var stream = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource source = decoder.Frames[0];

        if (totalRotation != 0)
        {
            source = new TransformedBitmap(source, new RotateTransform(totalRotation));
        }

        var ms = new MemoryStream();

        if (settings.QualityMode == QualityMode.Compressed || ext == ".jpg" || ext == ".jpeg")
        {
            int quality = settings.QualityMode == QualityMode.Compressed ? settings.JpegQuality : 100;
            var encoder = new JpegBitmapEncoder { QualityLevel = quality };
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(ms);
        }
        else
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(ms);
        }

        ms.Position = 0;
        cleanup = ms;
        return XImage.FromStream(ms);
    }

    private static void ApplySecurity(PdfDocument document, ConversionSettings settings)
    {
        if (settings.EnablePasswordProtection && !string.IsNullOrWhiteSpace(settings.Password))
        {
            var security = document.SecuritySettings;
            security.UserPassword = settings.Password;
            security.OwnerPassword = Guid.NewGuid().ToString("N");
            security.PermitPrint = true;
            security.PermitExtractContent = true;
        }
    }
}
