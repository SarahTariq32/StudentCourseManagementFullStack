using System.Text;
using System.Text.Json;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.Infrastructure.Repositories;

public class OllamaEmbeddingService : IEmbeddingService
{
    private const string EmbeddingModel = "nomic-embed-text";
    private readonly HttpClient _httpClient;

    public OllamaEmbeddingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var results = await GenerateEmbeddingsAsync(new[] { text }, cancellationToken);
        return results[0];
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new { model = EmbeddingModel, input = texts });

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/embed")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Ollama embedding request failed ({(int)response.StatusCode}): {body}. " +
                "Is Ollama running? Try 'ollama serve' and 'ollama pull nomic-embed-text'.",
                null,
                response.StatusCode);
        }

        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("embeddings", out var embeddingsArray) || embeddingsArray.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Ollama response did not contain 'embeddings': {body}");
        }

        var result = new List<float[]>();
        foreach (var vectorElement in embeddingsArray.EnumerateArray())
        {
            var vector = new List<float>();
            foreach (var value in vectorElement.EnumerateArray())
                vector.Add(value.GetSingle());
            result.Add(vector.ToArray());
        }

        if (result.Count != texts.Count)
        {
            throw new InvalidOperationException(
                $"Expected {texts.Count} embeddings but received {result.Count}.");
        }

        return result;
    }
}