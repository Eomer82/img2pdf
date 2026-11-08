using System.IO;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace Img2Pdf.App.Services;

public record OcrWordBox(string Text, double X, double Y, double Width, double Height);

public static class OcrService
{
    private static bool? _isSupported;

    public static bool IsSupported
    {
        get
        {
            if (_isSupported.HasValue) return _isSupported.Value;
            try
            {
                _isSupported = OcrEngine.AvailableRecognizerLanguages.Count > 0;
            }
            catch
            {
                _isSupported = false;
            }
            return _isSupported.Value;
        }
    }

    public static async Task<List<OcrWordBox>> RecognizeWordsAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var words = new List<OcrWordBox>();
        if (!IsSupported || !File.Exists(imagePath)) return words;

        try
        {
            var engine = OcrEngine.TryCreateFromUserProfileLanguages()
                      ?? (OcrEngine.AvailableRecognizerLanguages.Count > 0
                            ? OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0])
                            : null);

            if (engine == null) return words;

            var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(imagePath));
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);

            uint origW = decoder.OrientedPixelWidth;
            uint origH = decoder.OrientedPixelHeight;
            uint maxDim = OcrEngine.MaxImageDimension;

            var transform = new BitmapTransform();
            double scaleBack = 1.0;
            if (origW > maxDim || origH > maxDim)
            {
                double ratio = (double)maxDim / Math.Max(origW, origH);
                transform.ScaledWidth = (uint)Math.Max(1, Math.Round(origW * ratio));
                transform.ScaledHeight = (uint)Math.Max(1, Math.Round(origH * ratio));
                scaleBack = 1.0 / ratio;
            }

            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.DoNotColorManage);

            var result = await engine.RecognizeAsync(softwareBitmap);
            if (result?.Lines != null)
            {
                foreach (var line in result.Lines)
                {
                    foreach (var word in line.Words)
                    {
                        words.Add(new OcrWordBox(
                            word.Text,
                            word.BoundingRect.X * scaleBack,
                            word.BoundingRect.Y * scaleBack,
                            word.BoundingRect.Width * scaleBack,
                            word.BoundingRect.Height * scaleBack
                        ));
                    }
                }
            }
        }
        catch
        {
            // Gracefully ignore OCR errors
        }

        return words;
    }
}
