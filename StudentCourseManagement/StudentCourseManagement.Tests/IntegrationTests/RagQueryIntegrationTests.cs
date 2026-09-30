using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using StudentCourseManagement.Application.DTOs;
using Xunit;

namespace StudentCourseManagement.Tests.IntegrationTests;

public class RagQueryIntegrationTests : RagIntegrationTestBase, IClassFixture<CustomWebApplicationFactory>
{
    public RagQueryIntegrationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // Each test uploads a handbook with topic vocabulary unique to that test, so retrieval
    // can never cross-match a chunk from another test's document.

    private const string TuitionHandbook =
        "Gym membership policy. Students may use the campus gym with a valid student card. " +
        "Guest passes cost five dollars per visit.\f" +
        "Tuition refund policy. Tuition refunds are seventy percent before week six and forty percent before week nine. " +
        "No refunds are given after week nine.\f" +
        "Exam seating rules. Students must sit in assigned seats during final exams. " +
        "Leaving early requires instructor permission.";

    private const string LibraryHandbook =
        "Campus cafeteria menu. The cafeteria serves breakfast from seven until ten in the morning.\f" +
        "Sports center booking rules. Courts can be reserved online up to three days in advance.\f" +
        "Library opening hours. The university library opens at eight in the morning and closes at ten in the evening " +
        "on weekdays. On weekends the library closes at six in the evening.\f" +
        "Printing quota policy. Each student receives two hundred printed pages per semester.";

    private const string UnrelatedHandbook =
        "Campus shuttle schedule. The shuttle runs every twenty minutes between the north and south campuses on weekdays.\f" +
        "Lost and found policy. Items found on campus are stored at the security desk for thirty days.";

    [Fact]
    public async Task SearchStrict_HandbookQuestion_ReturnsAnswerCitingDocumentAndPage()
    {
        var adminToken = await LoginAsync("testadmin", "AdminPass123!");
        var upload = await UploadHandbookAsync(adminToken, "rag-tuition-handbook.txt", TuitionHandbook);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var studentToken = await LoginAsync("teststudent", "StudentPass123!");

        var result = await SearchAsync(studentToken, "strict", "how much tuition refund do i get before week six");

        Assert.Contains("[Source: rag-tuition-handbook.txt, Page: 2]", result.AdvisorNote);

        var prompt = Factory.FakeChat.ReceivedPrompts.Last();
        Assert.Contains("<documents>", prompt);
        Assert.Contains("[Source: rag-tuition-handbook.txt, Page: 2]", prompt);
        Assert.Contains("Tuition refund policy", prompt);
        // Untrusted document content must be explicitly marked as data, not instructions.
        Assert.Contains("not instructions", prompt);
    }

    [Fact]
    public async Task SearchFree_HandbookQuestion_ReturnsAnswerCitingDocumentAndPage()
    {
        var adminToken = await LoginAsync("testadmin", "AdminPass123!");
        var upload = await UploadHandbookAsync(adminToken, "rag-library-handbook.txt", LibraryHandbook);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var studentToken = await LoginAsync("teststudent", "StudentPass123!");

        var result = await SearchAsync(studentToken, "free", "what are the library opening hours on weekends");

        Assert.Contains("[Source: rag-library-handbook.txt, Page: 3]", result.AdvisorNote);
    }

    [Fact]
    public async Task SearchStrict_QuestionNoDocumentAnswers_ReturnsNotFoundRefusal()
    {
        var adminToken = await LoginAsync("testadmin", "AdminPass123!");
        var upload = await UploadHandbookAsync(adminToken, "rag-refusal-handbook.txt", UnrelatedHandbook);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var studentToken = await LoginAsync("teststudent", "StudentPass123!");

        var result = await SearchAsync(studentToken, "strict", "what is the parking fee for zebras on mars");

        Assert.Equal("not found in the documents", result.AdvisorNote);

        var prompt = Factory.FakeChat.ReceivedPrompts.Last();
        Assert.Contains("No relevant handbook passages were found", prompt);
    }

    [Fact]
    public async Task DeleteDocument_AsAdmin_RemovesAllItsChunks()
    {
        var adminToken = await LoginAsync("testadmin", "AdminPass123!");
        var upload = await UploadHandbookAsync(adminToken, "rag-delete-handbook.txt", UnrelatedHandbook);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.NotEmpty(GetChunksFromDb("rag-delete-handbook.txt"));

        var response = await DeleteDocumentAsync(adminToken, "rag-delete-handbook.txt");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(GetChunksFromDb("rag-delete-handbook.txt"));
    }

    [Fact]
    public async Task DeleteDocument_AsStudent_Returns403Forbidden()
    {
        var studentToken = await LoginAsync("teststudent", "StudentPass123!");

        var response = await DeleteDocumentAsync(studentToken, "rag-delete-handbook.txt");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

public class RagRateLimitIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RagRateLimitIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SearchStrict_ExceedingAiSearchLimit_Returns429TooManyRequests()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Username = "teststudent", Password = "StudentPass123!" });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(auth);

        var requests = Enumerable.Range(0, 80).Select(_ =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                "/api/AiCourse/search/strict?query=library%20hours");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
            return _client.SendAsync(request);
        });

        var responses = await Task.WhenAll(requests);

        Assert.Contains(responses, r => r.StatusCode == (HttpStatusCode)429);
    }
}
