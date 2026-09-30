namespace StudentCourseManagement.Application.Exceptions;

/// <summary>
/// Thrown when the embedding provider (e.g. Ollama) cannot be reached or times out while
/// indexing a document. The API maps this to HTTP 502 Bad Gateway.
/// </summary>
public class EmbeddingProviderUnavailableException : Exception
{
    public EmbeddingProviderUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
