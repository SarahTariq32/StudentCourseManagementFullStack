using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Exceptions;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.Application.Services;

/// <summary>
/// Parses uploaded documents (PDF via PdfPig, plain text) into page-aware chunks and stores
/// them in the vector store. Untrusted document text only ever travels into the vector store;
/// it is never executed or interpreted as instructions here.
/// </summary>
public class DocumentIngestionService : IDocumentIngestionService
{
    private readonly DocumentChunker _chunker;
    private readonly IVectorStore _vectorStore;

    public DocumentIngestionService(DocumentChunker chunker, IVectorStore vectorStore)
    {
        _chunker = chunker;
        _vectorStore = vectorStore;
    }

    public async Task<DocumentIngestionResultDto> IngestDocumentAsync(
        string documentName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentName))
            throw new ArgumentException("Document name is required.", nameof(documentName));
        if (content == null || !content.CanRead)
            throw new ArgumentException("A readable document stream is required.", nameof(content));

        bool isPdf = contentType == "application/pdf"
                     || documentName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        bool isTxt = contentType == "text/plain"
                     || documentName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

        if (!isPdf && !isTxt)
            throw new NotSupportedException("Only PDF and TXT files are supported.");

        var chunks = isPdf
            ? await Task.Run(() => ExtractPdfChunks(content), cancellationToken)
            : await ExtractTxtChunksAsync(content, cancellationToken);

        if (chunks.Count == 0)
            throw new InvalidOperationException("The document contains no readable text to index.");

        try
        {
            await _vectorStore.ProcessAndStoreDocumentAsync(documentName, chunks);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new EmbeddingProviderUnavailableException(
                "The embedding provider is unavailable, so the document could not be indexed. " +
                "Please verify the embedding service is running and try again.", ex);
        }

        return new DocumentIngestionResultDto
        {
            ChunkCount = chunks.Count,
            PageCount = chunks.Select(c => c.PageNumber).Distinct().Count()
        };
    }

    private List<(int PageNumber, int ChunkIndex, string Text)> ExtractPdfChunks(Stream content)
    {
        var chunks = new List<(int PageNumber, int ChunkIndex, string Text)>();

        using var document = UglyToad.PdfPig.PdfDocument.Open(content);

        foreach (var page in document.GetPages())
        {
            if (string.IsNullOrWhiteSpace(page.Text)) continue;

            var pageChunks = _chunker.ChunkText(page.Text);
            for (int i = 0; i < pageChunks.Count; i++)
                chunks.Add((page.Number, i, pageChunks[i]));
        }

        return chunks;
    }

    private async Task<List<(int PageNumber, int ChunkIndex, string Text)>> ExtractTxtChunksAsync(
        Stream content, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content);
        var text = await reader.ReadToEndAsync();

        var chunks = new List<(int PageNumber, int ChunkIndex, string Text)>();

        foreach (var (pageNumber, pageText) in _chunker.SplitTxtIntoPages(text))
        {
            var pageChunks = _chunker.ChunkText(pageText);
            for (int i = 0; i < pageChunks.Count; i++)
                chunks.Add((pageNumber, i, pageChunks[i]));
        }

        return chunks;
    }
}
