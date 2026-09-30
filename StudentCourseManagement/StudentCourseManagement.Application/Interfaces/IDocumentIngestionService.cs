using StudentCourseManagement.Application.DTOs;

namespace StudentCourseManagement.Application.Interfaces;

public interface IDocumentIngestionService
{
    /// <summary>
    /// Parses a PDF or plain-text document, splits it into page-aware overlapping chunks and
    /// stores them (with embeddings) through the vector store. Re-uploading an existing
    /// document name atomically replaces its chunks.
    /// </summary>
    /// <exception cref="NotSupportedException">The file type is not a supported PDF or TXT.</exception>
    /// <exception cref="InvalidOperationException">The document contains no readable text.</exception>
    /// <exception cref="EmbeddingProviderUnavailableException">The embedding provider could not be reached.</exception>
    Task<DocumentIngestionResultDto> IngestDocumentAsync(
        string documentName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);
}
