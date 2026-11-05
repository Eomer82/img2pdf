using System.IO;
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

        // Bruker Parallel.ForEachAsync for maksimal utnyttelse av prosessorkjerner
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
                    AddImagePage(document, item.FilePath, settings.PageSizeMode);
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
                    AddImagePage(document, item.FilePath, settings.PageSizeMode);
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

    private static void AddImagePage(PdfDocument document, string imagePath, PageSizeMode pageSizeMode)
    {
        var page = document.AddPage();

        // XImage fra PDFsharp laster inn bildet og beholder tapsfri kvalitet
        using var xImage = XImage.FromFile(imagePath);

        if (pageSizeMode == PageSizeMode.OriginalImageSize)
        {
            // 1:1 original bildestørrelse. Sidestørrelsen tilpasses nøyaktig til bildet i punkter.
            page.Width = XUnit.FromPoint(xImage.PointWidth);
            page.Height = XUnit.FromPoint(xImage.PointHeight);

            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawImage(xImage, 0, 0, page.Width.Point, page.Height.Point);
        }
        else
        {
            // Tilpass til standard sidestørrelse (f.eks. A4 eller Letter)
            page.Size = pageSizeMode == PageSizeMode.Letter_AutoOrientation ? PageSize.Letter : PageSize.A4;

            // Automatisk orientering basert på bildets proporsjoner
            page.Orientation = xImage.PixelWidth > xImage.PixelHeight
                ? PageOrientation.Landscape
                : PageOrientation.Portrait;

            const double margin = 20.0; // 20 punkters marg
            double availableWidth = page.Width.Point - (2 * margin);
            double availableHeight = page.Height.Point - (2 * margin);

            double imageRatio = (double)xImage.PixelWidth / xImage.PixelHeight;

            double targetWidth = availableWidth;
            double targetHeight = targetWidth / imageRatio;

            if (targetHeight > availableHeight)
            {
                targetHeight = availableHeight;
                targetWidth = targetHeight * imageRatio;
            }

            // Sentrer bildet på siden
            double x = margin + (availableWidth - targetWidth) / 2.0;
            double y = margin + (availableHeight - targetHeight) / 2.0;

            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawImage(xImage, x, y, targetWidth, targetHeight);
        }
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
