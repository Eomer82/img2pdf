using System.IO;
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
                    var disposables = new List<IDisposable>();

                    try
                    {
                        AddImagePage(document, item, settings, disposables);
                        ApplySecurity(document, settings);
                        document.Save(outputPath);
                    }
                    finally
                    {
                        // Clean up all streams and images only AFTER document.Save has finished
                        foreach (var d in disposables)
                        {
                            try { d.Dispose(); } catch { }
                        }
                    }
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
            var disposables = new List<IDisposable>();

            try
            {
                for (int i = 0; i < items.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var item = items[i];
                    item.Status = ConversionStatus.Behandler;

                    try
                    {
                        AddImagePage(document, item, settings, disposables);
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
            }
            finally
            {
                // Clean up all streams and images only AFTER document.Save has finished
                foreach (var d in disposables)
                {
                    try { d.Dispose(); } catch { }
                }
            }
        }, cancellationToken);
    }

    private static void AddImagePage(PdfDocument document, ImageFileItem item, ConversionSettings settings, List<IDisposable> disposables)
    {
        var page = document.AddPage();
        var xImage = LoadImage(item, settings, disposables);

        int rot = item.TotalRotation;

        if (settings.PageSizeMode == PageSizeMode.OriginalImageSize)
        {
            // If rotated 90 or 270 degrees, swap page width and height
            if (rot == 90 || rot == 270)
            {
                page.Width = XUnit.FromPoint(xImage.PointHeight);
                page.Height = XUnit.FromPoint(xImage.PointWidth);
            }
            else
            {
                page.Width = XUnit.FromPoint(xImage.PointWidth);
                page.Height = XUnit.FromPoint(xImage.PointHeight);
            }

            using var gfx = XGraphics.FromPdfPage(page);
            double cx = page.Width.Point / 2.0;
            double cy = page.Height.Point / 2.0;

            var state = gfx.Save();
            if (rot != 0)
            {
                gfx.RotateAtTransform(rot, new XPoint(cx, cy));
            }

            double drawX = cx - (xImage.PointWidth / 2.0);
            double drawY = cy - (xImage.PointHeight / 2.0);
            gfx.DrawImage(xImage, drawX, drawY, xImage.PointWidth, xImage.PointHeight);
            gfx.Restore(state);
        }
        else
        {
            page.Size = settings.PageSizeMode == PageSizeMode.Letter_AutoOrientation ? PageSize.Letter : PageSize.A4;

            // Determine effective orientation after rotation
            double effPixelW = (rot == 90 || rot == 270) ? xImage.PixelHeight : xImage.PixelWidth;
            double effPixelH = (rot == 90 || rot == 270) ? xImage.PixelWidth : xImage.PixelHeight;

            page.Orientation = effPixelW > effPixelH
                ? PageOrientation.Landscape
                : PageOrientation.Portrait;

            double margin = settings.MarginMode switch
            {
                PageMarginMode.None => 0.0,
                PageMarginMode.Normal => 56.69, // 20 mm in points
                _ => 28.35                     // 10 mm in points
            };

            double availW = Math.Max(1.0, page.Width.Point - (2 * margin));
            double availH = Math.Max(1.0, page.Height.Point - (2 * margin));

            double ratio = effPixelW / effPixelH;

            double targetW = availW;
            double targetH = targetW / ratio;

            if (targetH > availH)
            {
                targetH = availH;
                targetW = targetH * ratio;
            }

            double cx = margin + (availW / 2.0);
            double cy = margin + (availH / 2.0);

            double drawW = (rot == 90 || rot == 270) ? targetH : targetW;
            double drawH = (rot == 90 || rot == 270) ? targetW : targetH;

            double drawX = cx - (drawW / 2.0);
            double drawY = cy - (drawH / 2.0);

            using var gfx = XGraphics.FromPdfPage(page);
            var state = gfx.Save();
            if (rot != 0)
            {
                gfx.RotateAtTransform(rot, new XPoint(cx, cy));
            }
            gfx.DrawImage(xImage, drawX, drawY, drawW, drawH);
            gfx.Restore(state);
        }
    }

    private static XImage LoadImage(ImageFileItem item, ConversionSettings settings, List<IDisposable> disposables)
    {
        string ext = Path.GetExtension(item.FilePath).ToLowerInvariant();
        bool isStandardFormat = ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp";

        // Fast & 100% Lossless path for standard formats:
        // Embed directly from file via PDFsharp without decompressing huge bitmaps into RAM!
        // Rotation is handled losslessly by the PDF graphics matrix.
        if (isStandardFormat && settings.QualityMode == QualityMode.Lossless)
        {
            try
            {
                var img = XImage.FromFile(item.FilePath);
                disposables.Add(img);
                return img;
            }
            catch
            {
                // Fallback to WIC below if PDFsharp fails to parse this specific file directly
            }
        }

        // WIC decoding path for WebP, TIFF, or Compressed quality mode
        using var stream = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource source = decoder.Frames[0];

        var ms = new MemoryStream();
        disposables.Add(ms);

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
        var xImage = XImage.FromStream(ms);
        disposables.Add(xImage);
        return xImage;
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
