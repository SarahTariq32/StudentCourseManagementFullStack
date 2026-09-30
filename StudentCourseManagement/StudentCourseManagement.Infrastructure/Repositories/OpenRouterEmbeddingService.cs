using System.Text;
using System.Text.Json;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.Infrastructure.Repositories;

public class OpenRouterEmbeddingService : IEmbeddingService
{
    private const string EmbeddingModel = "openai/text-embedding-3-small";
    private const int MaxBatchSize = 64;

    private readonly HttpClient _httpClient;

    public OpenRouterEmbeddingService(HttpClient httpClient)
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
        var all = new List<float[]>(texts.Count);

        for (int i = 0; i < texts.Count; i += MaxBatchSize)
        {
            var batch = texts.Skip(i).Take(MaxBatchSize).ToList();
            var batchResult = await RequestBatchAsync(batch, cancellationToken);
            all.AddRange(batchResult);
        }

        return all;
    }

    private async Task<List<float[]>> RequestBatchAsync(List<string> batch, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { model = EmbeddingModel, input = batch });

        using var request = new HttpRequestMessage(HttpMethod.Post, "embeddings")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Embedding request failed ({(int)response.StatusCode}): {body}",
                null,
                response.StatusCode);
        }

        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Embedding response did not contain a 'data' list: {body}");
        }

        var ordered = new SortedDictionary<int, float[]>();
        int position = 0;

        foreach (var item in data.EnumerateArray())
        {
            int index = item.TryGetProperty("index", out var idx) && idx.ValueKind == JsonValueKind.Number
                ? idx.GetInt32()
                : position;

            var vector = new List<float>();
            foreach (var value in item.GetProperty("embedding").EnumerateArray())
            {
                vector.Add(value.GetSingle());
            }

            ordered[index] = vector.ToArray();
            position++;
        }

        if (ordered.Count != batch.Count)
        {
            throw new InvalidOperationException(
                $"Expected {batch.Count} embeddings but received {ordered.Count}.");
        }

        return ordered.Values.ToList();
    }
}