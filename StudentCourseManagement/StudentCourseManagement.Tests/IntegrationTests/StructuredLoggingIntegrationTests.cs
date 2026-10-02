using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Tests.TestDoubles;
using Xunit;
using Xunit.Abstractions;

namespace StudentCourseManagement.Tests.IntegrationTests;

public class StructuredLoggingIntegrationTests : RagIntegrationTestBase, IClassFixture<CustomWebApplicationFactory>
{
    private readonly ITestOutputHelper _output;

    private const string PolicyHandbook =
        "Tuition refund policy. Tuition refunds are seventy percent before week six and forty percent before week nine. " +
        "No refunds are given after week nine.\f" +
        "Campus library policy. The university library opens at eight in the morning and closes at ten in the evening.";

    public StructuredLoggingIntegrationTests(CustomWebApplicationFactory factory, ITestOutputHelper output) 
        : base(factory)
    {
        _output = output;
    }

    [Fact]
    public async Task Scenario1_SuccessfulSearch_LogsRagRetrievalAndAiResponse_WithCorrelationId()
    {
        // Arrange
        var customCorrelationId = $"test-success-corr-{Guid.NewGuid():N}";
        var adminToken = await LoginAsync("testadmin", "AdminPass123!");
        var upload = await UploadHandbookAsync(adminToken, "rag-logging-success.txt", PolicyHandbook);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var studentToken = await LoginAsync("teststudent", "StudentPass123!");
        var query = "how much tuition refund do i get before week six";

        // Act - execute search with custom X-Correlation-ID header
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/AiCourse/search/strict?query={Uri.EscapeDataString(query)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        request.Headers.Add("X-Correlation-ID", customCorrelationId);

        var response = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify Correlation ID header in HTTP response
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
        var returnedCorrelationId = response.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
        Assert.Equal(customCorrelationId, returnedCorrelationId);

        var body = await response.Content.ReadFromJsonAsync<CourseRecommendationResponseDto>();
        Assert.NotNull(body);
        Assert.Contains("[Source: rag-logging-success.txt, Page: 1]", body.AdvisorNote);

        // Retrieve the complete logs for this scenario using its correlation ID
        var logs = Factory.GetLogsByCorrelationId(customCorrelationId);
        var formattedLogs = Factory.GetFormattedLogs(customCorrelationId);

        _output.WriteLine($"=== RETRIEVED {logs.Count} LOG ENTRIES FOR SCENARIO 1 (CORRELATION ID: {customCorrelationId}) ===");
        foreach (var entry in formattedLogs)
        {
            _output.WriteLine(entry);
        }

        Assert.NotEmpty(logs);

        // Verify RAG vector search logging
        var ragSearchLog = logs.FirstOrDefault(l => l.MessageTemplate.Text.Contains("RAG vector search initiated"));
        Assert.NotNull(ragSearchLog);
        Assert.True(ragSearchLog.Properties.ContainsKey("QueryLength"));

        // Verify RAG retrieval chunks logging
        var ragRetrievalLog = logs.FirstOrDefault(l => l.MessageTemplate.Text.Contains("RAG retrieval completed"));
        Assert.NotNull(ragRetrievalLog);
        Assert.True(ragRetrievalLog.Properties.ContainsKey("RetrievedChunks"));
        Assert.True(ragRetrievalLog.Properties.ContainsKey("ResultCount"));

        // Verify AI search execution & completion logging
        var aiFinishLog = logs.FirstOrDefault(l => l.MessageTemplate.Text.Contains("AI final result"));
        Assert.NotNull(aiFinishLog);
        Assert.True(aiFinishLog.Properties.ContainsKey("ResponseLength"));
        Assert.True(aiFinishLog.Properties.ContainsKey("IsRefusal"));
    }

    [Fact]
    public async Task Scenario2_RagQueryRefusal_LogsZeroCandidatesAndRefusal_WithCorrelationId()
    {
        // Arrange
        var customCorrelationId = $"test-refusal-corr-{Guid.NewGuid():N}";
        var studentToken = await LoginAsync("teststudent", "StudentPass123!");
        var query = "what is the submarine docking fee on jupiter";

        // Act - query for non-existent handbook topic
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/AiCourse/search/strict?query={Uri.EscapeDataString(query)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        request.Headers.Add("X-Correlation-ID", customCorrelationId);

        var response = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CourseRecommendationResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("not found in the documents", body.AdvisorNote);

        // Retrieve the complete logs for this scenario using its correlation ID
        var logs = Factory.GetLogsByCorrelationId(customCorrelationId);
        var formattedLogs = Factory.GetFormattedLogs(customCorrelationId);

        _output.WriteLine($"=== RETRIEVED {logs.Count} LOG ENTRIES FOR SCENARIO 2 (CORRELATION ID: {customCorrelationId}) ===");
        foreach (var entry in formattedLogs)
        {
            _output.WriteLine(entry);
        }

        Assert.NotEmpty(logs);

        // Verify RAG logged 0 matching candidates or refusal guidance
        var candidateLog = logs.FirstOrDefault(l => 
            l.MessageTemplate.Text.Contains("RAG retrieval returned 0 matching candidates") ||
            l.MessageTemplate.Text.Contains("No matching handbook passages found"));
        Assert.NotNull(candidateLog);

        // Verify AI refusal was logged with structured IsRefusal = true
        var refusalLog = logs.FirstOrDefault(l => l.MessageTemplate.Text.Contains("AI final result"));
        Assert.NotNull(refusalLog);
        Assert.True(refusalLog.Properties.TryGetValue("IsRefusal", out var isRefusalProp));
        Assert.Equal("True", isRefusalProp.ToString());
    }

    [Fact]
    public async Task Scenario3_DeliberatelyTriggeredError_LogsUnhandledExceptionAndReturnsCorrelationId()
    {
        // Arrange
        var customCorrelationId = $"test-error-corr-{Guid.NewGuid():N}";

        // Act - call deliberately triggered test error endpoint
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/AiCourse/test-error");
        request.Headers.Add("X-Correlation-ID", customCorrelationId);

        var response = await Client.SendAsync(request);

        // Assert response status code is 500 (or mapped conflict/error)
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); // InvalidOperationException maps to 409 Conflict in ExceptionMiddleware

        // Verify Correlation ID is present in response headers
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
        Assert.Equal(customCorrelationId, response.Headers.GetValues("X-Correlation-ID").First());

        // Verify Correlation ID is present in response JSON body
        var responseContent = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(responseContent);
        Assert.True(jsonDoc.RootElement.TryGetProperty("correlationId", out var bodyCorrId));
        Assert.Equal(customCorrelationId, bodyCorrId.GetString());

        // Retrieve the complete logs for this scenario using its correlation ID
        var logs = Factory.GetLogsByCorrelationId(customCorrelationId);
        var formattedLogs = Factory.GetFormattedLogs(customCorrelationId);

        _output.WriteLine($"=== RETRIEVED {logs.Count} LOG ENTRIES FOR SCENARIO 3 (CORRELATION ID: {customCorrelationId}) ===");
        foreach (var entry in formattedLogs)
        {
            _output.WriteLine(entry);
        }

        Assert.NotEmpty(logs);

        // Verify ExceptionMiddleware logged the unhandled exception with full details
        var exceptionLog = logs.FirstOrDefault(l => l.MessageTemplate.Text.Contains("An unhandled exception occurred"));
        Assert.NotNull(exceptionLog);
        Assert.True(exceptionLog.Properties.ContainsKey("ErrorMessage"));
        Assert.True(exceptionLog.Properties.ContainsKey("Path"));
        Assert.True(exceptionLog.Properties.ContainsKey("CorrelationId"));
    }

    [Fact]
    public async Task Security_SensitiveCredentialsAndTokens_AreNotExposedInLogs()
    {
        // Arrange
        var customCorrelationId = $"test-security-corr-{Guid.NewGuid():N}";
        var studentPassword = "StudentPass123!";

        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginDto
            {
                Username = "teststudent",
                Password = studentPassword
            })
        };
        loginRequest.Headers.Add("X-Correlation-ID", customCorrelationId);

        // Act
        var response = await Client.SendAsync(loginRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(authResponse);

        // Retrieve logs for the login request
        var logs = Factory.GetLogsByCorrelationId(customCorrelationId);
        var formattedLogs = Factory.GetFormattedLogs(customCorrelationId);

        // Assert: password and raw JWT token must NEVER appear in logs
        foreach (var logString in formattedLogs)
        {
            Assert.DoesNotContain(studentPassword, logString);
            Assert.DoesNotContain(authResponse.Token, logString);
            Assert.DoesNotContain(authResponse.RefreshToken, logString);
        }
    }
}
