using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using StudentCourseManagement.Tests.TestDoubles;
using Xunit;

namespace StudentCourseManagement.Tests.IntegrationTests;

public class RagUploadIntegrationTests : RagIntegrationTestBase, IClassFixture<CustomWebApplicationFactory>
{
    public RagUploadIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task UploadHandbook_AsAdminWithFormFeedTxt_StoresPageNumberedChunksWithEmbeddings()
    {
        var adminToken = await LoginAsync("testadmin", "AdminPass123!");

        var response = await UploadHandbookAsync(adminToken, "rag-test-handbook.txt", HandbookContent);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chunks = GetChunksFromDb("rag-test-handbook.txt");

        Assert.Equal(5, chunks.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, chunks.Select(c => c.PageNumber).OrderBy(p => p).ToArray());
        Assert.All(chunks, c => Assert.Equal(0, c.ChunkIndex));

        // Embeddings must be stored and match the fake embedding dimensionality.
        Assert.All(chunks, c =>
        {
            var vector = JsonSerializer.Deserialize<float[]>(c.EmbeddingJson);
            Assert.NotNull(vector);
            Assert.Equal(FakeEmbeddingService.Dimensions, vector.Length);
        });

        Assert.Contains(chunks, c => c.TextContent.Contains("Course drop policy"));
        Assert.Contains(chunks, c => c.TextContent.Contains("Tuition refund policy"));
        Assert.Contains(chunks, c => c.TextContent.Contains("Library opening hours"));
    }

    [Fact]
    public async Task UploadHandbook_AsStudent_Returns403Forbidden()
    {
        var studentToken = await LoginAsync("teststudent", "StudentPass123!");

        var response = await UploadHandbookAsync(studentToken, "rag-test-handbook.txt", HandbookContent);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(GetChunksFromDb("rag-test-handbook.txt"));
    }

    [Fact]
    public async Task UploadHandbook_UnsupportedFileType_Returns400BadRequest()
    {
        var adminToken = await LoginAsync("testadmin", "AdminPass123!");

        var response = await UploadHandbookAsync(adminToken, "rules.json", "{ \"not\": \"supported\" }", "application/json");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadHandbook_TextFileWithoutReadableText_Returns400BadRequest()
    {
        var adminToken = await LoginAsync("testadmin", "AdminPass123!");

        var response = await UploadHandbookAsync(adminToken, "blank.txt", "   \f \r\n  ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadHandbook_ReuploadSameDocumentName_ReplacesOldChunksAtomically()
    {
        var adminToken = await LoginAsync("testadmin", "AdminPass123!");

        var first = await UploadHandbookAsync(adminToken, "rag-replace-handbook.txt", HandbookContent);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(5, GetChunksFromDb("rag-replace-handbook.txt").Count);

        var second = await UploadHandbookAsync(adminToken, "rag-replace-handbook.txt",
            "Fresh replacement policy text about gym memberships.");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var chunks = GetChunksFromDb("rag-replace-handbook.txt");
        var chunk = Assert.Single(chunks);
        Assert.StartsWith("Fresh replacement", chunk.TextContent);
        Assert.Equal(1, chunk.PageNumber);
    }
}

public class RagEmbeddingFailureIntegrationTests : IClassFixture<BrokenEmbeddingWebApplicationFactory>
{
    private readonly BrokenEmbeddingWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RagEmbeddingFailureIntegrationTests(BrokenEmbeddingWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UploadHandbook_EmbeddingProviderDown_Returns502BadGateway()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new StudentCourseManagement.Application.DTOs.LoginDto
            {
                Username = "testadmin",
                Password = "AdminPass123!"
            });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<StudentCourseManagement.Application.DTOs.AuthResponseDto>();
        Assert.NotNull(auth);

        using var form = new MultipartFormDataContent();
        using var file = new StringContent("Real policy text that would normally be indexed.", System.Text.Encoding.UTF8, "text/plain");
        form.Add(file, "file", "unreachable-embeddings.txt");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/AiCourse/upload-handbook") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("embedding", body, StringComparison.OrdinalIgnoreCase);
    }
}
