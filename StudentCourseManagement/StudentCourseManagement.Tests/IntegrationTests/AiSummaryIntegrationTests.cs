using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using StudentCourseManagement.Application.DTOs;
using Xunit;

namespace StudentCourseManagement.Tests.IntegrationTests;

public class AiSummaryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    private const string SummaryEndpoint = "/api/AiCourse/enrollmentrequests/ai-summary";

    public AiSummaryIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }


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

    [Fact]
    public async Task GetSummary_AsAdmin_Returns200OkWithValidSummaryDto()
    {
        string adminToken = await GetJwtTokenAsync("testadmin", "AdminPass123!");

        var request = new HttpRequestMessage(HttpMethod.Get, SummaryEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<EnrollmentRequestAiSummaryDto>();
        Assert.NotNull(summary);
        Assert.NotNull(summary.SummaryNote);
        Assert.NotNull(summary.Categories);
    }

    [Fact]
    public async Task GetSummary_AsStudent_Returns403Forbidden()
    {
        string studentToken = await GetJwtTokenAsync("teststudent", "StudentPass123!");
        var request = new HttpRequestMessage(HttpMethod.Get, SummaryEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSummary_CalledTwiceQuickly_ReturnsCachedResult()
    {
        string adminToken = await GetJwtTokenAsync("testadmin", "AdminPass123!");

        var request1 = new HttpRequestMessage(HttpMethod.Get, SummaryEndpoint);
        request1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response1 = await _client.SendAsync(request1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        var firstSummary = await response1.Content.ReadFromJsonAsync<EnrollmentRequestAiSummaryDto>();

        var request2 = new HttpRequestMessage(HttpMethod.Get, SummaryEndpoint);
        request2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response2 = await _client.SendAsync(request2);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        var secondSummary = await response2.Content.ReadFromJsonAsync<EnrollmentRequestAiSummaryDto>();

        Assert.NotNull(firstSummary);
        Assert.NotNull(secondSummary);
        Assert.Equal(firstSummary.SummaryNote, secondSummary.SummaryNote);
        Assert.Equal(firstSummary.TotalPendingRequests, secondSummary.TotalPendingRequests);
    }

    [Fact]
    public async Task GetSummary_ExceedingRateLimit_Returns429TooManyRequests()
    {
        string adminToken = await GetJwtTokenAsync("testadmin", "AdminPass123!");

        var tasks = Enumerable.Range(0, 20).Select(_ =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, SummaryEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            return _client.SendAsync(request);
        });

        var responses = await Task.WhenAll(tasks);

        Assert.Contains(responses, r => r.StatusCode == (HttpStatusCode)429);
    }
}