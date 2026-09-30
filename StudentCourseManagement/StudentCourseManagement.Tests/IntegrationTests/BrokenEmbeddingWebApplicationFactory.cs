using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.Tests.IntegrationTests;

/// <summary>
/// Factory variant where the embedding provider is down (every call throws), used to verify the
/// upload endpoint returns 502 Bad Gateway instead of a generic 500.
/// </summary>
public class BrokenEmbeddingWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmbeddingService>();
            services.AddSingleton<IEmbeddingService>(new ThrowingEmbeddingService());
        });
    }

    private sealed class ThrowingEmbeddingService : IEmbeddingService
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("Simulated: no connection could be made to Ollama");

        public Task<List<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("Simulated: no connection could be made to Ollama");
    }
}
