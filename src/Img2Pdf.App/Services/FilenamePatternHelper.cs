using System.IO;

namespace Img2Pdf.App.Services;

public static class FilenamePatternHelper
{
    public static string Resolve(string pattern, int fileCount, string? defaultFolder = null)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            pattern = "Images_Merged";
        }

        var now = DateTime.Now;
        string folderName = !string.IsNullOrWhiteSpace(defaultFolder) ? Path.GetFileName(defaultFolder) : "Bilder";
        if (string.IsNullOrWhiteSpace(folderName)) folderName = "Bilder";

        string result = pattern
            .Replace("{Date}", now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Dato}", now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Time}", now.ToString("HHmm"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Tid}", now.ToString("HHmm"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Year}", now.ToString("yyyy"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Aar}", now.ToString("yyyy"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Month}", now.ToString("MM"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Maaned}", now.ToString("MM"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Day}", now.ToString("dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Dag}", now.ToString("dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{Count}", fileCount.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{Antall}", fileCount.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{Folder}", folderName, StringComparison.OrdinalIgnoreCase)
            .Replace("{Mappe}", folderName, StringComparison.OrdinalIgnoreCase);

        // Remove any illegal filename characters
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            if (c != '/' && c != '\\') // preserve extension separator if any
            {
                result = result.Replace(c, '_');
            }
        }

        if (!result.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            result += ".pdf";
        }

        return result;
    }
}
