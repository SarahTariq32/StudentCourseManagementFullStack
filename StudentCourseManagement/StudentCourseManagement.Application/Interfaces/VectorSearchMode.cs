namespace StudentCourseManagement.Application.Interfaces;

/// <summary>
/// Retrieval strategy for <see cref="IVectorStore.SearchAsync"/>.
/// </summary>
public enum VectorSearchMode
{
    /// <summary>Plain vector search: cosine similarity ranking only. Used as the evaluation baseline.</summary>
    VectorOnly,

    /// <summary>
    /// Hybrid search: keyword and vector rankings merged with reciprocal rank fusion, followed by a
    /// second-pass quality filter and blended rerank before the results are returned.
    /// </summary>
    Hybrid
}
