using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Infrastructure.Data;
using StudentCourseManagement.Infrastructure.Entities;
using StudentCourseManagement.Tests.TestDoubles;

namespace StudentCourseManagement.Tests.IntegrationTests;

/// <summary>
/// Shared plumbing for the offline RAG integration tests: login, upload, search and direct
/// database verification helpers, plus the synthetic five-page handbook used as test corpus.
/// </summary>
public abstract class RagIntegrationTestBase
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected RagIntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>Synthetic handbook: five form-feed separated pages, each with a distinct topic.</summary>
    public const string HandbookContent =
        "Course drop policy. Students may drop at most two courses after week two with written advisor approval. " +
        "Drops after week ten require the dean signature.\f" +
        "Tuition refund policy. Tuition refunds are seventy percent before week six and forty percent before week nine. " +
        "No refunds are given after week nine.\f" +
        "Library opening hours. The university library opens at eight in the morning and closes at ten in the evening on weekdays. " +
        "On weekends the library closes at six in the evening.\f" +
        "Exam retake rules. A student may retake an exam at most once. Retakes are scheduled during the final week of the " +
        "semester with the instructor present.\f" +
        "Dormitory guest policy. Overnight guests are allowed only on weekends. All guests must register at the front desk " +
        "before eleven in the evening.";

    protected async Task<string> LoginAsync(string username, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Username = username, Password = password });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));

        return result.Token;
    }

    protected async Task<HttpResponseMessage> UploadHandbookAsync(
        string token, string fileName, string content, string contentType = "text/plain")
    {
        using var form = new MultipartFormDataContent();
        using var file = new StringContent(content, Encoding.UTF8, contentType);
        form.Add(file, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/AiCourse/upload-handbook")
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await Client.SendAsync(request);
    }

    protected async Task<CourseRecommendationResponseDto> SearchAsync(string token, string endpoint, string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/AiCourse/search/{endpoint}?query={Uri.EscapeDataString(query)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<CourseRecommendationResponseDto>();
        Assert.NotNull(dto);
        return dto;
    }

    protected async Task<HttpResponseMessage> DeleteDocumentAsync(string token, string documentName)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/api/AiCourse/documents/{Uri.EscapeDataString(documentName)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await Client.SendAsync(request);
    }

    protected List<DocumentChunk> GetChunksFromDb(string documentName)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return db.DocumentChunks.AsNoTracking()
            .Where(c => c.DocumentName == documentName)
            .ToList();
    }
}
