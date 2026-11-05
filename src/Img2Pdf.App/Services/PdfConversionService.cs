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
                        AddMultiImagePage(document, new[] { item }, settings, disposables);
                        ApplyMetadata(document, settings);
                        ApplySecurity(document, settings);
                        document.Save(outputPath);
                    }
                    finally
                    {
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
                int perPage = (int)settings.ImagesPerPage;
                int processed = 0;

                for (int i = 0; i < items.Count; i += perPage)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var chunk = items.Skip(i).Take(perPage).ToList();
                    foreach (var item in chunk)
                    {
                        item.Status = ConversionStatus.Behandler;
                    }

                    try
                    {
                        AddMultiImagePage(document, chunk, settings, disposables);
                        foreach (var item in chunk)
                        {
                            item.Status = ConversionStatus.Fullført;
                            item.ErrorMessage = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        foreach (var item in chunk)
                        {
                            item.Status = ConversionStatus.Feilet;
                            item.ErrorMessage = ex.Message;
                        }
                    }

                    processed += chunk.Count;
                    progress?.Report(new ConversionProgressReport(
                        Math.Min(processed, total),
                        total,
                        chunk[^1].FileName,
                        (double)processed / total * 100.0
                    ));
                }

                ApplyMetadata(document, settings);
                ApplySecurity(document, settings);
                document.Save(outputPath);
            }
            finally
            {
                foreach (var d in disposables)
                {
                    try { d.Dispose(); } catch { }
                }
            }
        }, cancellationToken);
    }

    private static void AddMultiImagePage(
        PdfDocument document,
        IReadOnlyList<ImageFileItem> items,
        ConversionSettings settings,
        List<IDisposable> disposables)
    {
        if (items.Count == 0) return;

        // If 1 image per page and 1:1 original size mode, use exact dimensions
        if (settings.ImagesPerPage == ImagesPerPage.One && settings.PageSizeMode == PageSizeMode.OriginalImageSize)
        {
            var item = items[0];
            var page = document.AddPage();
            var xImage = LoadImage(item, settings, disposables);
            int rot = item.TotalRotation;

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
            DrawImageInSlot(gfx, xImage, rot, 0, 0, page.Width.Point, page.Height.Point);
            return;
        }

        // Standard page size (A4 / Letter) or Multi-Image grid
        var multiPage = document.AddPage();
        multiPage.Size = settings.PageSizeMode == PageSizeMode.Letter_AutoOrientation ? PageSize.Letter : PageSize.A4;

        // Determine orientation
        if (settings.ImagesPerPage == ImagesPerPage.Two)
        {
            // For 2 images, Portrait stacks vertically, Landscape side-by-side
            multiPage.Orientation = PageOrientation.Portrait;
        }
        else if (settings.ImagesPerPage == ImagesPerPage.Four)
        {
            multiPage.Orientation = PageOrientation.Portrait;
        }
        else
        {
            // 1 image per page auto-orientation
            var firstItem = items[0];
            var firstImg = LoadImage(firstItem, settings, disposables);
            int r = firstItem.TotalRotation;
            double effW = (r == 90 || r == 270) ? firstImg.PixelHeight : firstImg.PixelWidth;
            double effH = (r == 90 || r == 270) ? firstImg.PixelWidth : firstImg.PixelHeight;
            multiPage.Orientation = effW > effH ? PageOrientation.Landscape : PageOrientation.Portrait;
        }

        double margin = settings.MarginMode switch
        {
            PageMarginMode.None => 0.0,
            PageMarginMode.Normal => 56.69, // 20 mm
            _ => 28.35                     // 10 mm
        };

        double availW = Math.Max(1.0, multiPage.Width.Point - (2 * margin));
        double availH = Math.Max(1.0, multiPage.Height.Point - (2 * margin));

        using var pageGfx = XGraphics.FromPdfPage(multiPage);

        var slots = CalculateSlots(items.Count, margin, availW, availH, multiPage.Orientation);

        for (int i = 0; i < items.Count && i < slots.Count; i++)
        {
            var item = items[i];
            var xImage = LoadImage(item, settings, disposables);
            var (slotX, slotY, slotW, slotH) = slots[i];
            DrawImageInSlot(pageGfx, xImage, item.TotalRotation, slotX, slotY, slotW, slotH);
        }
    }

    private static List<(double X, double Y, double Width, double Height)> CalculateSlots(
        int count,
        double margin,
        double availW,
        double availH,
        PageOrientation orientation)
    {
        var slots = new List<(double X, double Y, double Width, double Height)>();
        const double gap = 8.0;

        if (count == 1)
        {
            slots.Add((margin, margin, availW, availH));
        }
        else if (count == 2)
        {
            if (orientation == PageOrientation.Portrait)
            {
                // Top and bottom
                double slotH = (availH - gap) / 2.0;
                slots.Add((margin, margin, availW, slotH));
                slots.Add((margin, margin + slotH + gap, availW, slotH));
            }
            else
            {
                // Left and right
                double slotW = (availW - gap) / 2.0;
                slots.Add((margin, margin, slotW, availH));
                slots.Add((margin + slotW + gap, margin, slotW, availH));
            }
        }
        else
        {
            // 4 images (2x2 grid)
            double slotW = (availW - gap) / 2.0;
            double slotH = (availH - gap) / 2.0;

            slots.Add((margin, margin, slotW, slotH)); // Top-Left
            slots.Add((margin + slotW + gap, margin, slotW, slotH)); // Top-Right
            slots.Add((margin, margin + slotH + gap, slotW, slotH)); // Bottom-Left
            slots.Add((margin + slotW + gap, margin + slotH + gap, slotW, slotH)); // Bottom-Right
        }

        return slots;
    }

    private static void DrawImageInSlot(
        XGraphics gfx,
        XImage xImage,
        int rot,
        double slotX,
        double slotY,
        double slotW,
        double slotH)
    {
        double effW = (rot == 90 || rot == 270) ? xImage.PixelHeight : xImage.PixelWidth;
        double effH = (rot == 90 || rot == 270) ? xImage.PixelWidth : xImage.PixelHeight;

        double ratio = effW / effH;
        double targetW = slotW;
        double targetH = targetW / ratio;

        if (targetH > slotH)
        {
            targetH = slotH;
            targetW = targetH * ratio;
        }

        double cx = slotX + (slotW / 2.0);
        double cy = slotY + (slotH / 2.0);

        double drawW = (rot == 90 || rot == 270) ? targetH : targetW;
        double drawH = (rot == 90 || rot == 270) ? targetW : targetH;

        double drawX = cx - (drawW / 2.0);
        double drawY = cy - (drawH / 2.0);

        var state = gfx.Save();
        if (rot != 0)
        {
            gfx.RotateAtTransform(rot, new XPoint(cx, cy));
        }
        gfx.DrawImage(xImage, drawX, drawY, drawW, drawH);
        gfx.Restore(state);
    }

    private static XImage LoadImage(ImageFileItem item, ConversionSettings settings, List<IDisposable> disposables)
    {
        string ext = Path.GetExtension(item.FilePath).ToLowerInvariant();
        bool isStandardFormat = ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp";

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
                // Fallback to WIC below if PDFsharp fails to parse
            }
        }

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

    private static void ApplyMetadata(PdfDocument document, ConversionSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.DocumentTitle))
            document.Info.Title = settings.DocumentTitle.Trim();
        if (!string.IsNullOrWhiteSpace(settings.DocumentAuthor))
            document.Info.Author = settings.DocumentAuthor.Trim();
        if (!string.IsNullOrWhiteSpace(settings.DocumentSubject))
            document.Info.Subject = settings.DocumentSubject.Trim();
        document.Info.Creator = "Img2Pdf";
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
