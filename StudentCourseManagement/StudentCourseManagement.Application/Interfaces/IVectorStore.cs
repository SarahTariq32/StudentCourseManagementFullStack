using System.Collections.Generic;
using System.Threading.Tasks;
using StudentCourseManagement.Application.DTOs;

namespace StudentCourseManagement.Application.Interfaces;

public interface IVectorStore
{
    Task ProcessAndStoreDocumentAsync(string documentName, List<(int PageNumber, int ChunkIndex, string Text)> chunks);
    Task<List<VectorSearchResultDto>> SearchAsync(string query, int topK = 5, double minScore = 0.5, VectorSearchMode mode = VectorSearchMode.Hybrid);
    Task DeleteDocumentAsync(string documentName);
    Task<List<DocumentInfoDto>> GetIndexedDocumentsAsync();
}