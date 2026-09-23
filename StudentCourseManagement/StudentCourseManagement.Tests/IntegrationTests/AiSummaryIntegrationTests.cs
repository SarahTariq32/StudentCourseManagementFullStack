using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using StudentCourseManagement.Application.DTOs;
using Xunit;

namespace StudentCourseManagement.Tests.IntegrationTests;

public class AiSummaryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    // Matches [Route("api/[controller]")] + [HttpGet("enrollmentrequests/ai-summary")]
    private const string SummaryEndpoint = "/api/AiCourse/enrollmentrequests/ai-summary";

    public AiSummaryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Helper method to log in via POST /api/auth/login and retrieve a real JWT token.
    /// </summary>
    private async Task<string> GetJwtTokenAsync(string username, string password)
    {
        var loginDto = new LoginDto
        {
            Username = username,
            Password = password
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginDto);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));

        return result.Token;
    }

    // --- TEST 1: ADMIN AUTHORIZATION & SUMMARY GENERATION SUCCESS ---
    [Fact]
    public async Task GetSummary_AsAdmin_Returns200OkWithValidSummaryDto()
    {
        // 1. Authenticate as Admin via real auth endpoint
        string adminToken = await GetJwtTokenAsync("testadmin", "AdminPass123!");

        // 2. Call pending requests summary endpoint
        var request = new HttpRequestMessage(HttpMethod.Get, SummaryEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // 3. Act
        var response = await _client.SendAsync(request);

        // 4. Assert: Status is 200 OK and valid DTO returned
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<EnrollmentRequestAiSummaryDto>();
        Assert.NotNull(summary);
        Assert.NotNull(summary.SummaryNote);
        Assert.NotNull(summary.Categories);
    }

    // --- TEST 2: NON-ADMIN (STUDENT) TOKEN REJECTION ---
    [Fact]
    public async Task GetSummary_AsStudent_Returns403Forbidden()
    {
        // 1. Authenticate as Student via real auth endpoint
        string studentToken = await GetJwtTokenAsync("teststudent", "StudentPass123!");

        // 2. Call summary endpoint using Student token
        var request = new HttpRequestMessage(HttpMethod.Get, SummaryEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        // 3. Act
        var response = await _client.SendAsync(request);

        // 4. Assert: Expect 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- TEST 3: CACHING BEHAVIOR VERIFICATION ---
    [Fact]
    public async Task GetSummary_CalledTwiceQuickly_ReturnsCachedResult()
    {
        // 1. Authenticate as Admin
        string adminToken = await GetJwtTokenAsync("testadmin", "AdminPass123!");

        // 2. First Call - Triggers initial summary generation
        var request1 = new HttpRequestMessage(HttpMethod.Get, SummaryEndpoint);
        request1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response1 = await _client.SendAsync(request1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        var firstSummary = await response1.Content.ReadFromJsonAsync<EnrollmentRequestAiSummaryDto>();

        // 3. Second Call - Executed immediately after
        var request2 = new HttpRequestMessage(HttpMethod.Get, SummaryEndpoint);
        request2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response2 = await _client.SendAsync(request2);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        var secondSummary = await response2.Content.ReadFromJsonAsync<EnrollmentRequestAiSummaryDto>();

        // 4. Assert: Confirm identical cached summary returned
        Assert.NotNull(firstSummary);
        Assert.NotNull(secondSummary);
        Assert.Equal(firstSummary.SummaryNote, secondSummary.SummaryNote);
        Assert.Equal(firstSummary.TotalPendingRequests, secondSummary.TotalPendingRequests);
    }

    // --- TEST 4: RATE LIMITER ENFORCEMENT (429 TOO MANY REQUESTS) ---
    [Fact]
    public async Task GetSummary_ExceedingRateLimit_Returns429TooManyRequests()
    {
        // 1. Authenticate as Admin
        string adminToken = await GetJwtTokenAsync("testadmin", "AdminPass123!");

        // 2. Fire 20 concurrent requests simultaneously to overflow the limit & queue
        var tasks = Enumerable.Range(0, 20).Select(_ =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, SummaryEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            return _client.SendAsync(request);
        });

        var responses = await Task.WhenAll(tasks);

        // 3. Assert: Confirm rate limiter rejects requests with 429 Too Many Requests
        Assert.Contains(responses, r => r.StatusCode == (HttpStatusCode)429);
    }
}