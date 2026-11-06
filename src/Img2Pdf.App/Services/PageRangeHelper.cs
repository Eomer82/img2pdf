namespace Img2Pdf.App.Services;

public static class PageRangeHelper
{
    /// <summary>
    /// Parses a page range string (e.g., "1, 3-5, 8") into 0-based page indices.
    /// If rangeText is empty, whitespace, or "all", all pages (0 .. totalPages - 1) are returned.
    /// </summary>
    public static List<int> Parse(string? rangeText, int totalPages)
    {
        var allPages = Enumerable.Range(0, totalPages).ToList();
        if (string.IsNullOrWhiteSpace(rangeText) || rangeText.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return allPages;
        }

        var result = new SortedSet<int>();
        var parts = rangeText.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Contains('-'))
            {
                var sub = trimmed.Split('-');
                if (sub.Length == 2 && int.TryParse(sub[0], out int start) && int.TryParse(sub[1], out int end))
                {
                    int from = Math.Max(1, Math.Min(start, end));
                    int to = Math.Min(totalPages, Math.Max(start, end));
                    for (int p = from; p <= to; p++)
                    {
                        result.Add(p - 1);
                    }
                }
            }
            else if (int.TryParse(trimmed, out int pageNum))
            {
                if (pageNum >= 1 && pageNum <= totalPages)
                {
                    result.Add(pageNum - 1);
                }
            }
        }

        return result.Count > 0 ? result.ToList() : allPages;
    }
}
