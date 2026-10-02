using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Interfaces;
using StudentCourseManagement.Infrastructure.Data;
using StudentCourseManagement.Infrastructure.Entities;

namespace StudentCourseManagement.Infrastructure.Repositories;

public class VectorStore : IVectorStore
{
    private const int RrfK = 60;

    // Second-pass rerank / quality filter tuning (see docs/rag-design-notes.md for rationale).
    private const int CandidatePoolSize = 15;
    private const double RerankVectorWeight = 0.6;
    private const double RerankCoverageWeight = 0.4;
    private const double MinKeywordCoverage = 1.0 / 3.0;
    private const double HighVectorScoreThreshold = 0.50;
    private const int MaxChunksPerPage = 2;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "the", "and", "for", "are", "can", "how", "what", "when", "where", "which", "who", "why",
        "does", "did", "you", "your", "with", "this", "that", "from", "have", "has", "had", "was",
        "were", "will", "would", "could", "should", "not", "but", "any", "all", "may", "many",
        "much", "about", "into", "than", "then", "them", "they", "there", "their", "its", "our",
        "out", "per",
        "after", "before", "because", "been", "being", "between", "both", "during", "each", "few",
        "more", "most", "other", "some", "such", "only", "own", "same", "too", "very", "once",
        "here", "while", "until", "against", "above", "below", "further", "again", "under", "over",
        "also", "shall", "might", "must", "upon", "onto", "among", "across", "along", "within",
        "without", "these", "those", "whose", "whom", "himself", "herself", "itself",
        "themselves", "ourselves", "yourself",
        "don", "doesn", "didn", "isn", "aren", "wasn", "weren", "won", "wouldn", "couldn",
        "shouldn", "hasn", "haven", "hadn"
    };

    private readonly ApplicationDbContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly Microsoft.Extensions.Logging.ILogger<VectorStore>? _logger;

    public VectorStore(
        ApplicationDbContext db, 
        IEmbeddingService embeddingService, 
        Microsoft.Extensions.Logging.ILogger<VectorStore>? logger = null)
    {
        _db = db;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task ProcessAndStoreDocumentAsync(string documentName, List<(int PageNumber, int ChunkIndex, string Text)> chunks)
    {
        if (string.IsNullOrWhiteSpace(documentName))
            throw new ArgumentException("Document name is required.", nameof(documentName));

        if (chunks == null)
            throw new ArgumentNullException(nameof(chunks));

        var validChunks = chunks.Where(c => !string.IsNullOrWhiteSpace(c.Text)).ToList();

        if (validChunks.Count == 0)
            throw new InvalidOperationException("The document contains no readable text to store.");

        // Embed first, so a failed AI call never deletes the existing version of the document.
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(
            validChunks.Select(c => c.Text).ToList());

        var entities = new List<DocumentChunk>(validChunks.Count);
        for (int i = 0; i < validChunks.Count; i++)
        {
            entities.Add(new DocumentChunk
            {
                DocumentName = documentName,
                PageNumber = validChunks[i].PageNumber,
                ChunkIndex = validChunks[i].ChunkIndex,
                TextContent = validChunks[i].Text,
                EmbeddingJson = JsonSerializer.Serialize(embeddings[i])
            });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();

        // Load-then-remove (instead of ExecuteDelete) so the same code path works on SQL Server
        // and on provider-agnostic test doubles; the transaction keeps delete+insert atomic.
        var existingChunks = await _db.DocumentChunks
            .Where(c => c.DocumentName == documentName)
            .ToListAsync();

        _db.DocumentChunks.RemoveRange(existingChunks);
        await _db.SaveChangesAsync();

        await _db.DocumentChunks.AddRangeAsync(entities);
        await _db.SaveChangesAsync();

        await transaction.CommitAsync();
    }

    public async Task<List<VectorSearchResultDto>> SearchAsync(
        string query, int topK = 5, double minScore = 0.5, VectorSearchMode mode = VectorSearchMode.Hybrid)
    {
        var empty = new List<VectorSearchResultDto>();

        if (string.IsNullOrWhiteSpace(query) || topK <= 0)
            return empty;

        _logger?.LogInformation(
            "RAG vector search initiated. QueryLength: {QueryLength}, TopK: {TopK}, MinScore: {MinScore}, Mode: {SearchMode}", 
            query.Length, topK, minScore, mode);

        var queryVector = await _embeddingService.GenerateEmbeddingAsync(query);
        var keywords = ExtractKeywords(query);
        var allChunks = await _db.DocumentChunks.AsNoTracking().ToListAsync();

        var candidates = new List<ScoredChunk>();

        foreach (var chunk in allChunks)
        {
            float[]? vector = null;
            try
            {
                vector = JsonSerializer.Deserialize<float[]>(chunk.EmbeddingJson);
            }
            catch (JsonException)
            {
                continue;
            }

            if (vector == null || vector.Length != queryVector.Length)
                continue;

            double score = CosineSimilarity(queryVector, vector);
            if (score < minScore)
                continue;

            candidates.Add(new ScoredChunk(chunk, score, CountKeywordHits(chunk.TextContent, keywords)));
        }

        _logger?.LogInformation(
            "RAG candidate evaluation completed. Total chunks evaluated: {TotalChunks}, Candidates meeting minScore ({MinScore}): {CandidateCount}",
            allChunks.Count, minScore, candidates.Count);

        if (candidates.Count == 0)
        {
            _logger?.LogWarning(
                "RAG retrieval returned 0 matching candidates above minScore {MinScore}.",
                minScore);
            return empty;
        }

        if (mode == VectorSearchMode.VectorOnly)
        {
            var vectorOnlyResults = candidates
                .OrderByDescending(c => c.VectorScore)
                .Take(topK)
                .ToList();

            var vectorOnlySummaries = vectorOnlyResults.Select(r => new
            {
                r.Chunk.DocumentName,
                r.Chunk.PageNumber,
                r.Chunk.ChunkIndex,
                VectorScore = Math.Round(r.VectorScore, 4)
            }).ToList();

            _logger?.LogInformation(
                "RAG retrieval (VectorOnly) returning {ResultCount} chunks. Chunks: {@RetrievedChunks}",
                vectorOnlyResults.Count, vectorOnlySummaries);

            return vectorOnlyResults
                .Select(ToDto)
                .ToList();
        }

        var byVector = candidates
            .OrderByDescending(c => c.VectorScore)
            .ToList();

        var byKeyword = candidates
            .Where(c => c.KeywordHits > 0)
            .OrderByDescending(c => c.KeywordHits)
            .ThenByDescending(c => c.VectorScore)
            .ToList();

        for (int i = 0; i < byVector.Count; i++)
            byVector[i].RrfScore += 1.0 / (RrfK + i + 1);

        for (int i = 0; i < byKeyword.Count; i++)
            byKeyword[i].RrfScore += 1.0 / (RrfK + i + 1);

        // Second pass before the prompt is built: take the strongest fused candidates, apply a
        // quality filter, rerank with a blended score and cap how many chunks may come from the
        // same document page. This is a precision gate - chunks that neither match the question's
        // keywords nor score highly on vector similarity are dropped so weak retrieval degrades
        // into an explicit "not found in the documents" refusal instead of a guessed answer.
        var pool = candidates
            .OrderByDescending(c => c.RrfScore)
            .ThenByDescending(c => c.VectorScore)
            .Take(CandidatePoolSize)
            .ToList();

        bool hasKeywords = keywords.Count > 0;
        var survivors = new List<ScoredChunk>(pool.Count);

        foreach (var candidate in pool)
        {
            candidate.KeywordCoverage = hasKeywords
                ? (double)CountDistinctKeywordsPresent(candidate.Chunk.TextContent, keywords) / keywords.Count
                : 0.0;

            bool passesQualityFilter = !hasKeywords
                || candidate.KeywordCoverage >= MinKeywordCoverage
                || candidate.VectorScore >= HighVectorScoreThreshold;

            if (passesQualityFilter)
                survivors.Add(candidate);
        }

        _logger?.LogInformation(
            "RAG quality filter processed {PoolCount} candidates. Survivors after keyword coverage & threshold check: {SurvivorCount}",
            pool.Count, survivors.Count);

        var reranked = survivors
            .OrderByDescending(c => RerankVectorWeight * c.VectorScore + RerankCoverageWeight * c.KeywordCoverage)
            .ThenByDescending(c => c.VectorScore);

        var chunksPerPage = new Dictionary<(string DocumentName, int PageNumber), int>();
        var results = new List<ScoredChunk>(topK);

        foreach (var candidate in reranked)
        {
            var pageKey = (candidate.Chunk.DocumentName, candidate.Chunk.PageNumber);
            chunksPerPage.TryGetValue(pageKey, out int count);

            if (count >= MaxChunksPerPage)
                continue;

            chunksPerPage[pageKey] = count + 1;
            results.Add(candidate);

            if (results.Count == topK)
                break;
        }

        var retrievedChunksSummaries = results.Select(r => new
        {
            r.Chunk.DocumentName,
            r.Chunk.PageNumber,
            r.Chunk.ChunkIndex,
            VectorScore = Math.Round(r.VectorScore, 4),
            KeywordCoverage = Math.Round(r.KeywordCoverage, 4),
            KeywordHits = r.KeywordHits,
            RrfScore = Math.Round(r.RrfScore, 4),
            BlendedScore = Math.Round(RerankVectorWeight * r.VectorScore + RerankCoverageWeight * r.KeywordCoverage, 4)
        }).ToList();

        _logger?.LogInformation(
            "RAG retrieval completed. Returning {ResultCount} chunks. Retrieved Chunks: {@RetrievedChunks}",
            results.Count, retrievedChunksSummaries);

        return results
            .Select(ToDto)
            .ToList();
    }

    public async Task DeleteDocumentAsync(string documentName)
    {
        if (string.IsNullOrWhiteSpace(documentName))
            return;

        var chunks = await _db.DocumentChunks
            .Where(c => c.DocumentName == documentName)
            .ToListAsync();

        _db.DocumentChunks.RemoveRange(chunks);
        await _db.SaveChangesAsync();
    }

    public async Task<List<DocumentInfoDto>> GetIndexedDocumentsAsync()
    {
        var chunks = await _db.DocumentChunks
            .AsNoTracking()
            .Select(c => new { c.DocumentName, c.PageNumber })
            .ToListAsync();

        return chunks
            .GroupBy(c => c.DocumentName)
            .Select(g => new DocumentInfoDto
            {
                DocumentName = g.Key,
                ChunkCount = g.Count(),
                PageCount = g.Select(x => x.PageNumber).Distinct().Count()
            })
            .OrderBy(d => d.DocumentName)
            .ToList();
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
            return 0;

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private static List<string> ExtractKeywords(string query)
    {
        return Regex.Split(query.ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(t => t.Length >= 3 && !StopWords.Contains(t))
            .Select(t => t.Length > 3 && t.EndsWith('s') ? t.Substring(0, t.Length - 1) : t)
            .Distinct()
            .ToList();
    }

    private static int CountKeywordHits(string text, List<string> keywords)
    {
        if (keywords.Count == 0 || string.IsNullOrEmpty(text))
            return 0;

        string lower = text.ToLowerInvariant();
        int hits = 0;

        foreach (var keyword in keywords)
        {
            int position = 0;
            while ((position = lower.IndexOf(keyword, position, StringComparison.Ordinal)) >= 0)
            {
                hits++;
                position += keyword.Length;
            }
        }

        return hits;
    }

    private static int CountDistinctKeywordsPresent(string text, List<string> keywords)
    {
        if (keywords.Count == 0 || string.IsNullOrEmpty(text))
            return 0;

        string lower = text.ToLowerInvariant();
        return keywords.Count(k => lower.Contains(k, StringComparison.Ordinal));
    }

    private static VectorSearchResultDto ToDto(ScoredChunk chunk) => new()
    {
        DocumentName = chunk.Chunk.DocumentName,
        PageNumber = chunk.Chunk.PageNumber,
        ChunkIndex = chunk.Chunk.ChunkIndex,
        TextContent = chunk.Chunk.TextContent,
        Score = chunk.VectorScore
    };

    private sealed class ScoredChunk
    {
        public ScoredChunk(DocumentChunk chunk, double vectorScore, int keywordHits)
        {
            Chunk = chunk;
            VectorScore = vectorScore;
            KeywordHits = keywordHits;
        }

        public DocumentChunk Chunk { get; }
        public double VectorScore { get; }
        public int KeywordHits { get; }
        public double RrfScore { get; set; }
        public double KeywordCoverage { get; set; }
    }
}