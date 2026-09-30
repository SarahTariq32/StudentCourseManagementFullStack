using System.Text.RegularExpressions;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.Tests.TestDoubles;

/// <summary>
/// Deterministic offline embedding service for tests. Produces a bag-of-words vector:
/// each token is FNV-1a hashed into one of <see cref="Dimensions"/> buckets and counted,
/// so cosine similarity between two texts tracks their shared-token overlap. No network.
/// Tokenization mirrors the production keyword extractor (lowercase, non-alphanumeric
/// split, length >= 3, English stop-words removed, trailing 's' stemmed) so the fake
/// behaves like a lexical-aware embedding model.
/// </summary>
public class FakeEmbeddingService : IEmbeddingService
{
    public const int Dimensions = 512;

    /// <summary>Standard English stop-words (kept in sync with the production list in VectorStore).</summary>
    public static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
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

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(Embed(text));

    public Task<List<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        => Task.FromResult(texts.Select(Embed).ToList());

    public static float[] Embed(string text)
    {
        var vector = new float[Dimensions];

        if (string.IsNullOrEmpty(text)) return vector;

        foreach (var token in Tokenize(text))
            vector[Fnv1a(token) % Dimensions] += 1f;

        return vector;
    }

    public static List<string> Tokenize(string text) =>
        Regex.Split(text.ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(t => t.Length >= 3 && !StopWords.Contains(t))
            .Select(t => t.Length > 3 && t.EndsWith('s') ? t.Substring(0, t.Length - 1) : t)
            .ToList();

    private static uint Fnv1a(string token)
    {
        uint hash = 2166136261;
        foreach (char c in token)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash;
    }
}
