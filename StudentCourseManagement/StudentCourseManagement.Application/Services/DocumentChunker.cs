namespace StudentCourseManagement.Application.Services;

/// <summary>
/// Splits document text into overlapping chunks and plain-text documents into pages.
/// Chunking logic lives here (not in the controller) so it is unit-testable and reusable.
/// </summary>
public class DocumentChunker
{
    public const int DefaultChunkSize = 2000;
    public const int DefaultOverlap = 200;

    /// <summary>
    /// Splits text into chunks of at most <paramref name="chunkSize"/> characters with roughly
    /// <paramref name="overlap"/> characters of overlap between consecutive chunks. Chunk ends are
    /// snapped to the last word boundary inside the overlap zone so sentences are not cut mid-word
    /// more than necessary.
    /// </summary>
    public List<string> ChunkText(string text, int chunkSize = DefaultChunkSize, int overlap = DefaultOverlap)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();

        var chunks = new List<string>();
        int currentIndex = 0;

        while (currentIndex < text.Length)
        {
            int length = Math.Min(chunkSize, text.Length - currentIndex);
            string chunk = text.Substring(currentIndex, length);

            if (currentIndex + length < text.Length)
            {
                int lastSpace = chunk.LastIndexOf(' ');
                if (lastSpace > chunkSize - overlap)
                {
                    length = lastSpace;
                    chunk = text.Substring(currentIndex, length);
                }
            }

            chunks.Add(chunk.Trim());

            if (currentIndex + length >= text.Length)
                break;

            currentIndex += Math.Max(length - overlap, 1);
        }
        return chunks;
    }

    /// <summary>
    /// Splits a plain-text document into pages using the form-feed character (\f) as the page
    /// separator, so citations can reference real page numbers. Text without form feeds is
    /// returned as a single page (page 1), matching the historical TXT behaviour.
    /// </summary>
    public List<(int PageNumber, string Text)> SplitTxtIntoPages(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<(int, string)>();

        var pages = text.Split('\f', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0)
            .ToList();

        var result = new List<(int PageNumber, string Text)>(pages.Count);
        for (int i = 0; i < pages.Count; i++)
            result.Add((i + 1, pages[i]));

        return result;
    }
}
