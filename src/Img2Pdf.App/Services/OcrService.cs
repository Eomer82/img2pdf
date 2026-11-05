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
            var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            var result = await engine.RecognizeAsync(softwareBitmap);
            if (result?.Lines != null)
            {
                foreach (var line in result.Lines)
                {
                    foreach (var word in line.Words)
                    {
                        words.Add(new OcrWordBox(
                            word.Text,
                            word.BoundingRect.X,
                            word.BoundingRect.Y,
                            word.BoundingRect.Width,
                            word.BoundingRect.Height
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
