using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.Application.Plugins;

public class HandbookPlugin
{
    private readonly IVectorStore _vectorStore;
    private readonly ILogger? _logger;

    public HandbookPlugin(IVectorStore vectorStore, ILogger? logger)
    {
        _vectorStore = vectorStore;
        _logger = logger;
    }

    private string ComputeSha256Hash(string rawData)
    {
        using (var sha256Hash = System.Security.Cryptography.SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawData ?? ""));
            var builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }

    [KernelFunction, Description("Searches the student handbook for policies, rules, and procedures. Call this ONLY when the student asks a question that requires knowledge from the handbook.")]
    public async Task<string> SearchHandbookAsync(
        [Description("The search query to look up in the handbook.")] string query)
    {
        var queryHash = ComputeSha256Hash(query);
        try
        {
            var allResults = await _vectorStore.SearchAsync(query, topK: 5, minScore: 0.0);
            var results = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(allResults, r => r.Score >= 0.25));
            var topScore = allResults.Count > 0 ? System.Linq.Enumerable.Max(allResults, r => r.Score) : 0.0;
            if (topScore == 0.0)
            {
                var rawResults = await _vectorStore.SearchAsync(query, topK: 1, minScore: 0.0, mode: VectorSearchMode.VectorOnly);
                topScore = rawResults.Count > 0 ? System.Linq.Enumerable.Max(rawResults, r => r.Score) : 0.0;
            }

            if (results == null || results.Count == 0)
            {
                _logger?.LogInformation(
                    "HandbookPlugin: No matching handbook passages found. " +
                    "QueryLength: {QueryLength}, QueryHash: {QueryHash}, Threshold: {Threshold}, RefusalReason: {RefusalReason}, TopScore: {TopScore}", 
                    query?.Length ?? 0, queryHash, 0.25, "No results above threshold", topScore);

                return "<documents>\n(No relevant handbook passages were found for this question. " +
                       "If the student's question is about university policy, rules, or procedures, " +
                       "reply exactly: 'not found in the documents'.)\n</documents>";
            }

            var chunkIds = string.Join(",", System.Linq.Enumerable.Select(results, r => $"{r.DocumentName}_{r.ChunkIndex}"));
            var scores = string.Join(",", System.Linq.Enumerable.Select(results, r => r.Score.ToString("F3")));

            _logger?.LogInformation(
                "HandbookPlugin: Successfully retrieved chunks. " +
                "QueryLength: {QueryLength}, QueryHash: {QueryHash}, ChunkCount: {ChunkCount}, " +
                "ChunkIds: {ChunkIds}, RelevanceScores: {RelevanceScores}, Threshold: {Threshold}", 
                query?.Length ?? 0, queryHash, results.Count, chunkIds, scores, 0.25);

            var sb = new StringBuilder("<documents>\n");
            foreach (var res in results)
                sb.AppendLine($"[Source: {res.DocumentName}, Page: {res.PageNumber}] {res.TextContent}");
            sb.AppendLine("</documents>");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "HandbookPlugin: Error searching vector store. QueryLength: {QueryLength}, QueryHash: {QueryHash}", query?.Length ?? 0, queryHash);
            return "<documents>\n(Handbook search is temporarily unavailable.)\n</documents>";
        }
    }
}
