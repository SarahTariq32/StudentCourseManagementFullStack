using System.Text;
using StudentCourseManagement.Application.Exceptions;
using StudentCourseManagement.Application.Interfaces;
using StudentCourseManagement.Application.Services;
using StudentCourseManagement.Application.DTOs;
using Xunit;

namespace StudentCourseManagement.Tests.UnitTests;

public class DocumentIngestionServiceTests
{
    private readonly CapturingVectorStore _vectorStore = new();
    private readonly DocumentIngestionService _service;

    public DocumentIngestionServiceTests()
    {
        _service = new DocumentIngestionService(new DocumentChunker(), _vectorStore);
    }

    private static MemoryStream TextStream(string text) =>
        new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task IngestAsync_TxtWithFormFeeds_ChunksPagesSeparately()
    {
        var text = "Students may drop two courses after week two.\fTuition refunds are seventy percent before week six.";

        var result = await _service.IngestDocumentAsync("policies.txt", "text/plain", TextStream(text));

        Assert.Equal(2, result.ChunkCount);
        Assert.Equal(2, result.PageCount);

        var call = Assert.Single(_vectorStore.Calls);
        Assert.Equal("policies.txt", call.DocumentName);
        Assert.Equal(1, call.Chunks[0].PageNumber);
        Assert.Contains("drop two courses", call.Chunks[0].Text);
        Assert.Equal(2, call.Chunks[1].PageNumber);
        Assert.Contains("seventy percent", call.Chunks[1].Text);
    }

    [Fact]
    public async Task IngestAsync_TxtWithoutFormFeeds_PutsAllChunksOnPageOne()
    {
        var longText = string.Join(" ", Enumerable.Range(0, 400).Select(i => $"policy word {i:d4} for the handbook"));

        var result = await _service.IngestDocumentAsync("handbook.txt", "text/plain", TextStream(longText));

        Assert.True(result.ChunkCount > 1);
        Assert.Equal(1, result.PageCount);
        Assert.All(_vectorStore.Calls[0].Chunks, c => Assert.Equal(1, c.PageNumber));
        Assert.Equal(0, _vectorStore.Calls[0].Chunks[0].ChunkIndex);
        Assert.Equal(1, _vectorStore.Calls[0].Chunks[1].ChunkIndex);
    }

    [Fact]
    public async Task IngestAsync_UnsupportedFileType_ThrowsNotSupportedException()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _service.IngestDocumentAsync("rules.json", "application/json", TextStream("{ }")));
    }

    [Fact]
    public async Task IngestAsync_TxtWithOnlyWhitespace_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.IngestDocumentAsync("empty.txt", "text/plain", TextStream("   \f \r\n  ")));
    }

    [Fact]
    public async Task IngestAsync_PdfDocument_ExtractsTextPerPage()
    {
        var pdf = MinimalPdfBuilder.SinglePagePdf("Drop policy: two courses after week two.", "Refund policy: seventy percent.");

        var result = await _service.IngestDocumentAsync("guide.pdf", "application/pdf", new MemoryStream(pdf));

        Assert.Equal(1, result.PageCount);
        var call = Assert.Single(_vectorStore.Calls);
        Assert.Equal("guide.pdf", call.DocumentName);
        Assert.Equal(1, call.Chunks[0].PageNumber);
        Assert.Contains("Drop policy", call.Chunks[0].Text);
        Assert.Contains("seventy percent", call.Chunks[0].Text);
    }

    [Fact]
    public async Task IngestAsync_EmbeddingConnectionFailure_ThrowsEmbeddingProviderUnavailableException()
    {
        _vectorStore.OnProcess = (_, _) => throw new HttpRequestException("No connection could be made to Ollama");

        var ex = await Assert.ThrowsAsync<EmbeddingProviderUnavailableException>(() =>
            _service.IngestDocumentAsync("policies.txt", "text/plain", TextStream("Some real text to index.")));

        Assert.Contains("embedding provider", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IngestAsync_EmbeddingTimeout_ThrowsEmbeddingProviderUnavailableException()
    {
        _vectorStore.OnProcess = (_, _) => throw new TaskCanceledException("The embedding request timed out.");

        await Assert.ThrowsAsync<EmbeddingProviderUnavailableException>(() =>
            _service.IngestDocumentAsync("policies.txt", "text/plain", TextStream("Some real text to index.")));
    }

    /// <summary>Builds a minimal but valid single-page PDF with the given text lines.</summary>
    private static class MinimalPdfBuilder
    {
        public static byte[] SinglePagePdf(params string[] lines)
        {
            var content = new StringBuilder();
            content.AppendLine("BT");
            content.AppendLine("/F1 12 Tf");
            content.AppendLine("72 720 Td");
            foreach (var line in lines)
            {
                content.AppendLine($"({line}) Tj");
                content.AppendLine("0 -20 Td");
            }
            content.AppendLine("ET");

            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
                $"<< /Length {content.Length} >>\nstream\n{content}endstream",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
            };

            var pdf = new StringBuilder();
            pdf.AppendLine("%PDF-1.4");
            var offsets = new List<int>();
            for (int i = 0; i < objects.Count; i++)
            {
                offsets.Add(pdf.Length);
                pdf.AppendLine($"{i + 1} 0 obj");
                pdf.AppendLine(objects[i]);
                pdf.AppendLine("endobj");
            }

            int xrefStart = pdf.Length;
            pdf.AppendLine($"xref");
            pdf.AppendLine($"0 {objects.Count + 1}");
            pdf.AppendLine("0000000000 65535 f ");
            foreach (var offset in offsets)
                pdf.AppendLine($"{offset:d10} 00000 n ");

            pdf.AppendLine($"trailer");
            pdf.AppendLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
            pdf.AppendLine("startxref");
            pdf.AppendLine(xrefStart.ToString());
            pdf.AppendLine("%%EOF");

            return Encoding.ASCII.GetBytes(pdf.ToString());
        }
    }
}

/// <summary>Test double that records what was sent to the vector store.</summary>
public class CapturingVectorStore : IVectorStore
{
    public List<(string DocumentName, List<(int PageNumber, int ChunkIndex, string Text)> Chunks)> Calls { get; } = new();

    public Func<string, List<(int PageNumber, int ChunkIndex, string Text)>, Task>? OnProcess { get; set; }

    public Task ProcessAndStoreDocumentAsync(string documentName, List<(int PageNumber, int ChunkIndex, string Text)> chunks)
    {
        Calls.Add((documentName, chunks));
        return OnProcess?.Invoke(documentName, chunks) ?? Task.CompletedTask;
    }

    public Task<List<VectorSearchResultDto>> SearchAsync(string query, int topK = 5, double minScore = 0.5, VectorSearchMode mode = VectorSearchMode.Hybrid)
        => Task.FromResult(new List<VectorSearchResultDto>());

    public Task DeleteDocumentAsync(string documentName) => Task.CompletedTask;

    public Task<List<DocumentInfoDto>> GetIndexedDocumentsAsync()
        => Task.FromResult(new List<DocumentInfoDto>());
}
