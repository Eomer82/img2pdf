using System.IO;
using System.Windows.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Img2Pdf.App.Models;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Img2Pdf.App.Services;

public record ConversionProgressReport(int CurrentIndex, int TotalCount, string CurrentFile, double Percentage);

public enum PdfCompressionProfile
{
    Email,      // 150 DPI, 55% JPEG
    Balanced,   // 200 DPI, 75% JPEG
    HighQuality // 300 DPI, 85% JPEG
}

public record PdfCompressionResult(string OutputPath, long OriginalBytes, long CompressedBytes, int PageCount)
{
    public double SavingsPercentage => OriginalBytes > 0 
        ? Math.Max(0.0, (1.0 - (double)CompressedBytes / OriginalBytes) * 100.0) 
        : 0.0;
}

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
                        if (item.IsPdf)
                        {
                            var inputDoc = PdfReader.Open(item.FilePath, PdfDocumentOpenMode.Import);
                            disposables.Add(inputDoc);
                            for (int p = 0; p < inputDoc.Pages.Count; p++)
                            {
                                document.AddPage(inputDoc.Pages[p]);
                            }
                        }
                        else
                        {
                            AddMultiImagePage(document, new[] { item }, settings, disposables);
                        }

                        ApplyDecorations(document, settings);
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

        string fileName = FilenamePatternHelper.Resolve(settings.MergedFileName, items.Count, targetFolder);
        string outputPath = Path.Combine(targetFolder, fileName);

        await Task.Run(() =>
        {
            using var document = new PdfDocument();
            var disposables = new List<IDisposable>();
            var pageMapping = new List<(ImageFileItem Item, int PageNumber)>();

            try
            {
                int perPage = (int)settings.ImagesPerPage;
                var pendingImages = new List<ImageFileItem>();

                void FlushPendingImages()
                {
                    while (pendingImages.Count > 0)
                    {
                        var chunk = pendingImages.Take(perPage).ToList();
                        pendingImages.RemoveRange(0, chunk.Count);
                        try
                        {
                            AddMultiImagePage(document, chunk, settings, disposables);
                            int pageIdx = document.Pages.Count - 1;
                            foreach (var item in chunk)
                            {
                                pageMapping.Add((item, pageIdx + 1));
                                if (settings.GenerateBookmarks && pageIdx >= 0 && pageIdx < document.Pages.Count)
                                {
                                    document.Outlines.Add(item.FileName, document.Pages[pageIdx], true);
                                }
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
                    }
                }

                for (int i = 0; i < items.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var item = items[i];
                    item.Status = ConversionStatus.Behandler;

                    if (item.IsPdf)
                    {
                        FlushPendingImages();
                        try
                        {
                            var inputDoc = PdfReader.Open(item.FilePath, PdfDocumentOpenMode.Import);
                            disposables.Add(inputDoc);
                            int startPage = document.Pages.Count;
                            for (int p = 0; p < inputDoc.Pages.Count; p++)
                            {
                                document.AddPage(inputDoc.Pages[p]);
                            }
                            pageMapping.Add((item, startPage + 1));
                            if (settings.GenerateBookmarks && startPage < document.Pages.Count)
                            {
                                document.Outlines.Add(item.FileName, document.Pages[startPage], true);
                            }
                            item.Status = ConversionStatus.Fullført;
                            item.ErrorMessage = null;
                        }
                        catch (Exception ex)
                        {
                            item.Status = ConversionStatus.Feilet;
                            item.ErrorMessage = ex.Message;
                        }
                    }
                    else
                    {
                        pendingImages.Add(item);
                        if (pendingImages.Count >= perPage)
                        {
                            var chunk = pendingImages.Take(perPage).ToList();
                            pendingImages.RemoveRange(0, perPage);
                            try
                            {
                                AddMultiImagePage(document, chunk, settings, disposables);
                                int pageIdx = document.Pages.Count - 1;
                                foreach (var ci in chunk)
                                {
                                    pageMapping.Add((ci, pageIdx + 1));
                                    if (settings.GenerateBookmarks && pageIdx >= 0 && pageIdx < document.Pages.Count)
                                    {
                                        document.Outlines.Add(ci.FileName, document.Pages[pageIdx], true);
                                    }
                                    ci.Status = ConversionStatus.Fullført;
                                    ci.ErrorMessage = null;
                                }
                            }
                            catch (Exception ex)
                            {
                                foreach (var ci in chunk)
                                {
                                    ci.Status = ConversionStatus.Feilet;
                                    ci.ErrorMessage = ex.Message;
                                }
                            }
                        }
                    }

                    progress?.Report(new ConversionProgressReport(
                        i + 1,
                        total,
                        item.FileName,
                        (double)(i + 1) / total * 100.0
                    ));
                }

                // Flush any remaining images
                FlushPendingImages();

                if (settings.GenerateTableOfContents && pageMapping.Count > 0)
                {
                    GenerateTocPage(document, items, pageMapping);
                }

                ApplyDecorations(document, settings);
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
            var state = gfx.Save();
            if (rot != 0)
            {
                gfx.RotateAtTransform(rot, new XPoint(page.Width.Point / 2.0, page.Height.Point / 2.0));
            }

            double drawX = (page.Width.Point - xImage.PointWidth) / 2.0;
            double drawY = (page.Height.Point - xImage.PointHeight) / 2.0;
            gfx.DrawImage(xImage, drawX, drawY);

            // OCR in 1:1 mode
            if (settings.EnableOcr)
            {
                var words = OcrService.RecognizeWordsAsync(item.FilePath).GetAwaiter().GetResult();
                DrawOcrTextLayer(gfx, words, xImage.PixelWidth, xImage.PixelHeight, drawX, drawY, xImage.PointWidth, xImage.PointHeight);
            }

            // Stempel / Signatur in 1:1 mode
            if (item.HasStamp)
            {
                DrawStampBadge(gfx, item.StampText!, item.StampColor, drawX + xImage.PointWidth - 96, drawY + 12);
            }

            gfx.Restore(state);
            return;
        }

        // Standard Page setup (A4 or US Letter)
        var multiPage = document.AddPage();
        multiPage.Size = settings.PageSizeMode switch
        {
            PageSizeMode.Letter_AutoOrientation => PageSize.Letter,
            _ => PageSize.A4
        };

        // For single image, adjust page orientation to match image aspect ratio
        if (settings.ImagesPerPage == ImagesPerPage.One)
        {
            var singleItem = items[0];
            var testImage = LoadImage(singleItem, settings, disposables);
            int rot = singleItem.TotalRotation;
            double effW = (rot == 90 || rot == 270) ? testImage.PixelHeight : testImage.PixelWidth;
            double effH = (rot == 90 || rot == 270) ? testImage.PixelWidth : testImage.PixelHeight;
            multiPage.Orientation = effW > effH ? PageOrientation.Landscape : PageOrientation.Portrait;
        }
        else
        {
            multiPage.Orientation = PageOrientation.Portrait;
        }

        double margin = settings.MarginMode switch
        {
            PageMarginMode.None => 0.0,
            PageMarginMode.Normal => 56.69, // 20 mm
            _ => 28.35                     // 10 mm
        };

        double pageWidth = multiPage.Width.Point;
        double pageHeight = multiPage.Height.Point;

        var slots = CalculateSlots(items.Count, pageWidth, pageHeight, margin);

        using (var gfx = XGraphics.FromPdfPage(multiPage))
        {
            for (int i = 0; i < items.Count && i < slots.Count; i++)
            {
                var item = items[i];
                var xImage = LoadImage(item, settings, disposables);
                var slot = slots[i];

                DrawImageInSlot(gfx, xImage, item, settings, slot.X, slot.Y, slot.Width, slot.Height);
            }
        }
    }

    private static List<(double X, double Y, double Width, double Height)> CalculateSlots(
        int count,
        double pageWidth,
        double pageHeight,
        double margin)
    {
        var slots = new List<(double, double, double, double)>();
        double availW = Math.Max(10, pageWidth - (margin * 2));
        double availH = Math.Max(10, pageHeight - (margin * 2));
        double gap = 14.0; // gap between images

        if (count <= 1)
        {
            slots.Add((margin, margin, availW, availH));
        }
        else if (count == 2)
        {
            if (pageHeight >= pageWidth)
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
        ImageFileItem item,
        ConversionSettings settings,
        double slotX,
        double slotY,
        double slotW,
        double slotH)
    {
        int rot = item.TotalRotation;
        double effW = (rot == 90 || rot == 270) ? xImage.PixelHeight : xImage.PixelWidth;
        double effH = (rot == 90 || rot == 270) ? xImage.PixelWidth : xImage.PixelHeight;

        double reservedBottom = settings.ShowCaptions ? 18.0 : 0.0;
        double usableSlotH = Math.Max(10.0, slotH - reservedBottom);

        double ratio = effW / effH;
        double targetW = slotW;
        double targetH = targetW / ratio;

        if (targetH > usableSlotH)
        {
            targetH = usableSlotH;
            targetW = targetH * ratio;
        }

        double cx = slotX + (slotW / 2.0);
        double cy = slotY + (usableSlotH / 2.0);

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

        // OCR searchable text layer
        if (settings.EnableOcr)
        {
            var words = OcrService.RecognizeWordsAsync(item.FilePath).GetAwaiter().GetResult();
            DrawOcrTextLayer(gfx, words, xImage.PixelWidth, xImage.PixelHeight, drawX, drawY, drawW, drawH);
        }

        gfx.Restore(state);

        // Captions
        if (settings.ShowCaptions)
        {
            string caption = settings.CaptionMode switch
            {
                CaptionMode.DateTaken => item.FormattedDate,
                CaptionMode.FileNameAndDate => $"{item.FileName} • {item.FormattedDate}",
                _ => item.FileName
            };

            var captionFont = new XFont("Segoe UI", 8.5, XFontStyleEx.Regular);
            var captionBrush = new XSolidBrush(XColor.FromArgb(160, 70, 70, 70));
            var size = gfx.MeasureString(caption, captionFont);
            double capX = cx - (size.Width / 2.0);
            double capY = slotY + slotH - 4.0;
            gfx.DrawString(caption, captionFont, captionBrush, capX, capY);
        }

        // Stempel / Signatur
        if (item.HasStamp)
        {
            DrawStampBadge(gfx, item.StampText!, item.StampColor, slotX + slotW - 96, slotY + 12);
        }
    }

    private static void DrawStampBadge(XGraphics gfx, string stampText, string colorName, double x, double y)
    {
        var stampState = gfx.Save();
        gfx.RotateAtTransform(-12, new XPoint(x + 44, y + 17));

        XColor mainColor = colorName.ToLowerInvariant() switch
        {
            "red" => XColor.FromArgb(215, 204, 28, 28),
            "blue" => XColor.FromArgb(215, 20, 115, 210),
            "orange" => XColor.FromArgb(215, 220, 110, 0),
            _ => XColor.FromArgb(215, 20, 140, 48) // Green
        };

        var pen = new XPen(mainColor, 2.0);
        var fontTitle = new XFont("Segoe UI", 9.5, XFontStyleEx.Bold);
        var fontDate = new XFont("Segoe UI", 7.0, XFontStyleEx.Regular);

        var sizeTitle = gfx.MeasureString(stampText, fontTitle);
        double w = Math.Max(88.0, sizeTitle.Width + 18.0);
        double h = 34.0;

        // Double border for authentic rubber stamp feel
        gfx.DrawRoundedRectangle(pen, x, y, w, h, 4, 4);
        var innerPen = new XPen(mainColor, 0.75);
        gfx.DrawRoundedRectangle(innerPen, x + 2.5, y + 2.5, w - 5.0, h - 5.0, 3, 3);

        var brush = new XSolidBrush(mainColor);
        gfx.DrawString(stampText, fontTitle, brush, x + (w / 2.0), y + 15, XStringFormats.Center);

        string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
        gfx.DrawString(dateStr, fontDate, brush, x + (w / 2.0), y + 26, XStringFormats.Center);

        gfx.Restore(stampState);
    }

    private static void DrawOcrTextLayer(
        XGraphics gfx,
        IReadOnlyList<OcrWordBox> words,
        int pixelWidth,
        int pixelHeight,
        double drawX,
        double drawY,
        double drawW,
        double drawH)
    {
        if (words == null || words.Count == 0 || pixelWidth <= 0 || pixelHeight <= 0) return;

        double scaleX = drawW / (double)pixelWidth;
        double scaleY = drawH / (double)pixelHeight;
        var transparentBrush = new XSolidBrush(XColor.FromArgb(0, 0, 0, 0));

        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word.Text)) continue;

            double wx = drawX + (word.X * scaleX);
            double wy = drawY + (word.Y * scaleY);
            double ww = word.Width * scaleX;
            double wh = word.Height * scaleY;

            double fontSize = Math.Max(4.0, wh * 0.85);
            var font = new XFont("Segoe UI", fontSize, XFontStyleEx.Regular);

            gfx.DrawString(word.Text, font, transparentBrush, wx, wy + (wh * 0.85));
        }
    }

    private static XImage LoadImage(ImageFileItem item, ConversionSettings settings, List<IDisposable> disposables)
    {
        string ext = Path.GetExtension(item.FilePath).ToLowerInvariant();
        bool isStandardFormat = ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp";
        bool hasTransforms = settings.FilterMode != ImageFilterMode.Original || item.HasCrop;

        // Fast & 100% Lossless path for standard formats without crop/filters:
        if (isStandardFormat && settings.QualityMode == QualityMode.Lossless && !hasTransforms)
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

        // 1. Apply Crop if specified
        if (item.HasCrop)
        {
            int origW = source.PixelWidth;
            int origH = source.PixelHeight;

            int x = (int)Math.Round(origW * (item.CropLeft / 100.0));
            int y = (int)Math.Round(origH * (item.CropTop / 100.0));
            int w = origW - x - (int)Math.Round(origW * (item.CropRight / 100.0));
            int h = origH - y - (int)Math.Round(origH * (item.CropBottom / 100.0));

            w = Math.Clamp(w, 10, origW);
            h = Math.Clamp(h, 10, origH);
            x = Math.Clamp(x, 0, origW - w);
            y = Math.Clamp(y, 0, origH - h);

            source = new CroppedBitmap(source, new System.Windows.Int32Rect(x, y, w, h));
        }

        // 2. Apply Filters (Grayscale or Document B&W scan)
        if (settings.FilterMode == ImageFilterMode.Grayscale)
        {
            source = new FormatConvertedBitmap(source, System.Windows.Media.PixelFormats.Gray8, null, 0);
        }
        else if (settings.FilterMode == ImageFilterMode.BlackAndWhite)
        {
            var gray = new FormatConvertedBitmap(source, System.Windows.Media.PixelFormats.Gray8, null, 0);
            int w = gray.PixelWidth;
            int h = gray.PixelHeight;
            byte[] pixels = new byte[w * h];
            gray.CopyPixels(pixels, w, 0);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = pixels[i] > 135 ? (byte)255 : (byte)0;
            }
            source = BitmapSource.Create(w, h, gray.DpiX, gray.DpiY, System.Windows.Media.PixelFormats.Gray8, null, pixels, w);
        }

        var ms = new MemoryStream();
        disposables.Add(ms);

        if (settings.FilterMode == ImageFilterMode.BlackAndWhite)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(ms);
        }
        else if (settings.QualityMode == QualityMode.Compressed || ext == ".jpg" || ext == ".jpeg" || settings.FilterMode == ImageFilterMode.Grayscale)
        {
            int quality = settings.QualityMode == QualityMode.Compressed ? settings.JpegQuality : 95;
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

    private static void ApplyDecorations(PdfDocument document, ConversionSettings settings)
    {
        if (!settings.EnablePageNumbers && (!settings.EnableWatermark || string.IsNullOrWhiteSpace(settings.WatermarkText)))
        {
            return;
        }

        int totalPages = document.Pages.Count;
        for (int p = 0; p < totalPages; p++)
        {
            var page = document.Pages[p];
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

            // Watermark
            if (settings.EnableWatermark && !string.IsNullOrWhiteSpace(settings.WatermarkText))
            {
                var state = gfx.Save();
                double cx = page.Width.Point / 2.0;
                double cy = page.Height.Point / 2.0;
                gfx.TranslateTransform(cx, cy);
                gfx.RotateTransform(-40);

                var wmFont = new XFont("Segoe UI", 42, XFontStyleEx.Bold);
                var wmBrush = new XSolidBrush(XColor.FromArgb(40, 128, 128, 128));
                var size = gfx.MeasureString(settings.WatermarkText.Trim(), wmFont);
                gfx.DrawString(settings.WatermarkText.Trim(), wmFont, wmBrush, -size.Width / 2.0, size.Height / 3.0);
                gfx.Restore(state);
            }

            // Page numbers
            if (settings.EnablePageNumbers)
            {
                string fmt = string.IsNullOrWhiteSpace(settings.PageNumberFormat) ? "Side {0} av {1}" : settings.PageNumberFormat;
                string text = string.Format(fmt, p + 1, totalPages);
                var font = new XFont("Segoe UI", 9.5, XFontStyleEx.Regular);
                var brush = new XSolidBrush(XColor.FromArgb(170, 70, 70, 70));

                var size = gfx.MeasureString(text, font);
                double y = page.Height.Point - 18.0;
                double x = settings.PageNumberPosition switch
                {
                    PageNumberPosition.BottomLeft => 30.0,
                    PageNumberPosition.BottomRight => page.Width.Point - size.Width - 30.0,
                    _ => (page.Width.Point - size.Width) / 2.0
                };

                gfx.DrawString(text, font, brush, x, y);
            }
        }
    }

    public static async Task<List<string>> ExtractPdfPagesAsync(
        string pdfPath, 
        string outputFolder, 
        string? pageRange = null, 
        CancellationToken cancellationToken = default)
    {
        var extractedFiles = new List<string>();
        Directory.CreateDirectory(outputFolder);

        await Task.Run(() =>
        {
            using var inputDoc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
            string baseName = Path.GetFileNameWithoutExtension(pdfPath);
            var pages = PageRangeHelper.Parse(pageRange, inputDoc.Pages.Count);

            foreach (int pageIndex in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var singlePageDoc = new PdfDocument();
                singlePageDoc.AddPage(inputDoc.Pages[pageIndex]);
                string singlePagePath = Path.Combine(outputFolder, $"{baseName}_side_{pageIndex + 1}.pdf");
                singlePageDoc.Save(singlePagePath);
                extractedFiles.Add(singlePagePath);
            }
        }, cancellationToken);

        return extractedFiles;
    }

    public static async Task<List<string>> ExtractPdfAsImagesAsync(
        string pdfPath,
        string outputFolder,
        string? pageRange = null,
        CancellationToken cancellationToken = default)
    {
        var extractedFiles = new List<string>();
        Directory.CreateDirectory(outputFolder);

        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(pdfPath));
        var pdfDoc = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);

        string baseName = Path.GetFileNameWithoutExtension(pdfPath);
        var pages = PageRangeHelper.Parse(pageRange, (int)pdfDoc.PageCount);

        foreach (int pageIndex in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var page = pdfDoc.GetPage((uint)pageIndex);

            // Render at 300 DPI for high fidelity output
            var renderOptions = new Windows.Data.Pdf.PdfPageRenderOptions
            {
                DestinationWidth = (uint)Math.Max(100, Math.Round(page.Size.Width * (300.0 / 72.0))),
                DestinationHeight = (uint)Math.Max(100, Math.Round(page.Size.Height * (300.0 / 72.0)))
            };

            string outPath = Path.Combine(outputFolder, $"{baseName}_side_{pageIndex + 1}.png");
            using var memStream = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(memStream, renderOptions);
            memStream.Seek(0);

            using (var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
            {
                using var netStream = memStream.AsStreamForRead();
                await netStream.CopyToAsync(outFs, cancellationToken);
            }

            extractedFiles.Add(outPath);
        }

        return extractedFiles;
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
            security.OwnerPassword = settings.Password;
        }
    }

    private static void GenerateTocPage(PdfDocument document, IReadOnlyList<ImageFileItem> items, List<(ImageFileItem Item, int PageNumber)> pageMapping)
    {
        var tocPage = document.InsertPage(0);
        tocPage.Size = PageSize.A4;
        using var gfx = XGraphics.FromPdfPage(tocPage);

        var titleFont = new XFont("Segoe UI", 18, XFontStyleEx.Bold);
        var subFont = new XFont("Segoe UI", 9.5, XFontStyleEx.Regular);
        var itemFont = new XFont("Segoe UI", 9.5, XFontStyleEx.Regular);
        var pageNumFont = new XFont("Segoe UI", 9.5, XFontStyleEx.Bold);
        var brush = new XSolidBrush(XColor.FromArgb(40, 40, 40));
        var grayBrush = new XSolidBrush(XColor.FromArgb(120, 120, 120));
        var dotPen = new XPen(XColor.FromArgb(180, 180, 180), 0.6) { DashStyle = XDashStyle.Dot };

        string tocTitle = LocalizationService.GetString("TableOfContents");
        gfx.DrawString(tocTitle, titleFont, brush, 40, 50);

        string subtitle = $"{DateTime.Now:yyyy-MM-dd HH:mm} • {items.Count} {LocalizationService.GetString("Col_Status")}";
        gfx.DrawString(subtitle, subFont, grayBrush, 40, 68);
        gfx.DrawLine(new XPen(XColor.FromArgb(200, 200, 200), 1), 40, 80, tocPage.Width.Point - 40, 80);

        double y = 105;
        foreach (var entry in pageMapping)
        {
            if (y > tocPage.Height.Point - 50) break;

            string name = entry.Item.FileName;
            var sizeName = gfx.MeasureString(name, itemFont);
            gfx.DrawString(name, itemFont, brush, 40, y);

            string pStr = $"{entry.PageNumber + 1}";
            var sizeP = gfx.MeasureString(pStr, pageNumFont);
            double rightX = tocPage.Width.Point - 40;
            gfx.DrawString(pStr, pageNumFont, brush, rightX - sizeP.Width, y);

            double dotStart = 40 + sizeName.Width + 8;
            double dotEnd = rightX - sizeP.Width - 8;
            if (dotEnd > dotStart)
            {
                gfx.DrawLine(dotPen, dotStart, y - 3, dotEnd, y - 3);
            }

            y += 20;
        }
    }

    public static async Task<PdfCompressionResult> CompressPdfAsync(
        string pdfPath,
        string outputPath,
        PdfCompressionProfile profile = PdfCompressionProfile.Balanced,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var (targetDpi, jpegQuality) = profile switch
        {
            PdfCompressionProfile.Email => (150, 55),
            PdfCompressionProfile.HighQuality => (300, 85),
            _ => (200, 75) // Balanced
        };

        long originalBytes = new FileInfo(pdfPath).Length;
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(pdfPath));
        var pdfDoc = await Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file);
        int totalPages = (int)pdfDoc.PageCount;

        string outDir = Path.GetDirectoryName(outputPath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(outDir)) Directory.CreateDirectory(outDir);

        using var outDoc = new PdfDocument();

        for (int p = 0; p < totalPages; p++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var page = pdfDoc.GetPage((uint)p);

            double scale = targetDpi / 72.0;
            uint renderW = (uint)Math.Max(100, Math.Round(page.Size.Width * scale));
            uint renderH = (uint)Math.Max(100, Math.Round(page.Size.Height * scale));

            var renderOptions = new Windows.Data.Pdf.PdfPageRenderOptions
            {
                DestinationWidth = renderW,
                DestinationHeight = renderH
            };

            using var memStream = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(memStream, renderOptions);
            memStream.Seek(0);

            byte[] jpegBytes;
            using (var netStream = memStream.AsStreamForRead())
            {
                var decoder = BitmapDecoder.Create(netStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];

                using var jpegStream = new MemoryStream();
                var encoder = new JpegBitmapEncoder { QualityLevel = jpegQuality };
                encoder.Frames.Add(frame);
                encoder.Save(jpegStream);
                jpegBytes = jpegStream.ToArray();
            }

            var outPage = outDoc.AddPage();
            outPage.Width = XUnit.FromPoint(page.Size.Width);
            outPage.Height = XUnit.FromPoint(page.Size.Height);

            using var gfx = XGraphics.FromPdfPage(outPage);
            using var ms = new MemoryStream(jpegBytes);
            using var xImg = XImage.FromStream(ms);
            gfx.DrawImage(xImg, 0, 0, outPage.Width.Point, outPage.Height.Point);

            progress?.Report((double)(p + 1) / totalPages * 100.0);
        }

        outDoc.Save(outputPath);
        long compressedBytes = new FileInfo(outputPath).Length;

        return new PdfCompressionResult(outputPath, originalBytes, compressedBytes, totalPages);
    }
}
