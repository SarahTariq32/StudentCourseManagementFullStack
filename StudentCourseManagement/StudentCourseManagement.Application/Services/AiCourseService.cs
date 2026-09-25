using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Interfaces;
using StudentCourseManagement.Application.Plugins;
using System.Text.Json;

namespace StudentCourseManagement.Application.Services;

public class AiCourseService : IAiCourseService
{
    private readonly Kernel _kernel;
    private readonly ICourseRepository _courseRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IMemoryCache _cache;

    private const string SummaryCacheKey = "PendingRequests_AiSummary_CacheKey";
    private static readonly SemaphoreSlim _summaryCacheLock = new SemaphoreSlim(1, 1);

    private static readonly JsonSerializerOptions SseJsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    public AiCourseService(
        Kernel kernel,
        ICourseRepository courseRepository,
        IStudentRepository studentRepository,
        IMemoryCache cache)
    {
        _kernel = kernel;
        _courseRepository = courseRepository;
        _studentRepository = studentRepository;
        _cache = cache;
    }

    public async Task<CourseRecommendationResponseDto> SearchStrictAsync(string username, string query)
    {
        var student = await _studentRepository.GetByNameAsync(username);

        if (student == null)
        {
            return new CourseRecommendationResponseDto
            {
                AdvisorNote = "Could not find a student record linked to your account. Please contact an administrator."
            };
        }

        var scopedKernel = _kernel.Clone();
        var plugin = new CoursePlugin(_courseRepository, student.Id);
        scopedKernel.Plugins.AddFromObject(plugin, "CoursePlugin");

        string prompt = $@"You are a friendly, professional academic advisor assisting a student.

BEHAVIOR RULES:
1. Speak naturally as a helpful academic advisor. NEVER mention terms like 'Strict Enrolled Mode', 'functions', 'tools', 'API', or 'backend'.
2. If the student asks about their enrolled courses, schedule, or current classes (e.g., 'tell me about my courses', 'what am I taking?'), IMMEDIATELY call the 'GetEnrolledCoursesAsync' function. NEVER ask the student for permission to retrieve their data—just fetch it directly.
3. For simple greetings or general non-schedule questions, answer directly without invoking tools.

When returning course details, format your output strictly as pure JSON matching this structure:
{{
  ""matchedCourses"": [
    {{ ""id"": 1, ""name"": ""Course Title"", ""reason"": ""Currently enrolled"" }}
  ],
  ""advisorNote"": ""A natural summary of your current schedule.""
}}

Student Query: ""{query}""";

        try
        {
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.1,
                MaxTokens = 800,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var chatCompletion = scopedKernel.GetRequiredService<IChatCompletionService>();
            var result = await chatCompletion.GetChatMessageContentAsync(prompt, executionSettings, scopedKernel);
            return CleanAndParseJsonResponse(result.ToString());
        }
        catch (Exception ex)
        {
            return new CourseRecommendationResponseDto
            {
                AdvisorNote = $"AI Service Error: {(ex.InnerException != null ? ex.InnerException.Message : ex.Message)}"
            };
        }
    }

    public async Task<CourseRecommendationResponseDto> SearchFreeformAsync(string username, string query)
    {
        var student = await _studentRepository.GetByNameAsync(username);

        if (student == null)
        {
            return new CourseRecommendationResponseDto
            {
                AdvisorNote = "Could not find a student record linked to your account. Please contact an administrator."
            };
        }

        var scopedKernel = _kernel.Clone();
        var plugin = new CoursePlugin(_courseRepository, student.Id);
        scopedKernel.Plugins.AddFromObject(plugin, "CoursePlugin");

        string prompt = $@"You are a creative, proactive academic advisor assisting a student with course discovery.

BEHAVIOR RULES:
1. Speak naturally as a helpful academic advisor. NEVER mention system technical details like 'Freeform Catalog Mode', 'functions', 'tools', or 'API'.
2. If the student asks for recommendations or what other courses to take, call 'GetEnrolledCoursesAsync' to check their current schedule, AND call 'GetSimilarAvailableCoursesAsync' to explore catalog options.
3. Cross-reference both lists and recommend 2 to 3 catalog courses that the student IS NOT currently enrolled in. Do NOT ask for permission—just call the functions and offer recommendations.
4. For general greetings or general guidance, answer directly without calling tools.

When returning recommendations, format your output strictly as pure JSON matching this structure:
{{
  ""matchedCourses"": [
    {{ ""id"": 101, ""name"": ""Course Title"", ""reason"": ""Why this fits your goals"" }}
  ],
  ""advisorNote"": ""A helpful breakdown explaining why these new courses complement your current schedule.""
}}

Student Query: ""{query}""";

        try
        {
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.7,
                MaxTokens = 800,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var chatCompletion = scopedKernel.GetRequiredService<IChatCompletionService>();
            var result = await chatCompletion.GetChatMessageContentAsync(prompt, executionSettings, scopedKernel);
            return CleanAndParseJsonResponse(result.ToString());
        }
        catch (Exception ex)
        {
            return new CourseRecommendationResponseDto
            {
                AdvisorNote = $"AI Service Error: {(ex.InnerException != null ? ex.InnerException.Message : ex.Message)}"
            };
        }
    }

    

   

    public void InvalidatePendingRequestsSummaryCache()
    {
        _cache.Remove(SummaryCacheKey);
    }

    

    private CourseRecommendationResponseDto CleanAndParseJsonResponse(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return new CourseRecommendationResponseDto
            {
                AdvisorNote = "I evaluated your query, but received an empty output from the model. Please try asking again."
            };
        }

        string cleaned = rawResponse.Trim();

        if (cleaned.Contains("User Safety:"))
        {
            var lines = cleaned.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                               .Where(l => !l.StartsWith("User Safety:", StringComparison.OrdinalIgnoreCase))
                               .ToList();
            cleaned = string.Join("\n", lines).Trim();
        }

        if (cleaned.StartsWith("```json")) cleaned = cleaned.Substring(7);
        else if (cleaned.StartsWith("```")) cleaned = cleaned.Substring(3);
        if (cleaned.EndsWith("```")) cleaned = cleaned.Substring(0, cleaned.Length - 3);
        cleaned = cleaned.Trim();

        int firstBrace = cleaned.IndexOf('{');
        int lastBrace = cleaned.LastIndexOf('}');

        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            string jsonSubstring = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<CourseRecommendationResponseDto>(jsonSubstring, options);

                if (parsed != null && (!string.IsNullOrWhiteSpace(parsed.AdvisorNote) || (parsed.MatchedCourses != null && parsed.MatchedCourses.Count > 0)))
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
                // Fall through to plain text if JSON extraction fails
            }
        }

        return new CourseRecommendationResponseDto
        {
            AdvisorNote = cleaned
        };
    }
    public async IAsyncEnumerable<string> StreamPendingRequestsSummaryTextAsync(
[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
    
        if (_cache.TryGetValue(SummaryCacheKey, out EnrollmentRequestAiSummaryDto? cachedSummary) && cachedSummary != null)
        {
            yield return JsonSerializer.Serialize(new { type = "meta", total = cachedSummary.TotalPendingRequests, categories = cachedSummary.Categories }, SseJsonOptions);
            yield return JsonSerializer.Serialize(new { type = "chunk", text = cachedSummary.SummaryNote }, SseJsonOptions);
            yield return JsonSerializer.Serialize(new { type = "done", summary = cachedSummary }, SseJsonOptions);
            yield break;
        }

        var pendingRequests = await _courseRepository.GetPendingEnrollmentRequestsAsync();

        if (pendingRequests == null || pendingRequests.Count == 0)
        {
            var emptySummary = new EnrollmentRequestAiSummaryDto
            {
                TotalPendingRequests = 0,
                Categories = new List<RequestCategoryCountDto>(),
                SummaryNote = "No pending registration or course requests found.",
                IsAiGenerated = false,
                AiStatusMessage = "No active requests to analyze."
            };

            yield return JsonSerializer.Serialize(new { type = "meta", total = 0, categories = emptySummary.Categories }, SseJsonOptions);
            yield return JsonSerializer.Serialize(new { type = "chunk", text = emptySummary.SummaryNote }, SseJsonOptions);
            yield return JsonSerializer.Serialize(new { type = "done", summary = emptySummary }, SseJsonOptions);
            yield break;
        }

        int realTotal = pendingRequests.Count;

        var categories = pendingRequests
            .GroupBy(r => string.IsNullOrWhiteSpace(r.RequestType) ? "General Request" : r.RequestType.Trim())
            .Select(g => new RequestCategoryCountDto { Category = g.Key, Count = g.Count() })
            .OrderByDescending(c => c.Count)
            .ToList();

        int currentSum = categories.Sum(c => c.Count);
        if (currentSum != realTotal)
            categories.First().Count += (realTotal - currentSum);

        yield return JsonSerializer.Serialize(new { type = "meta", total = realTotal, categories }, SseJsonOptions);

        string categoryCountsText = string.Join(", ", categories.Select(c => $"{c.Count} {c.Category}s"));

        var groupedDescriptions = pendingRequests
            .GroupBy(r => r.RequestType?.Trim() ?? "General Request")
            .Select(g =>
                $"Category '{g.Key}' ({g.Count()} requests):\n" +
                string.Join("\n", g.Select(r =>
                    $"  - Student: {r.StudentName}, Course: {r.CourseName ?? "N/A"}, Reason: '{r.Reason ?? "No reason provided"}'"
                ).Take(10)));

        string requestsDetail = string.Join("\n\n", groupedDescriptions);

        string prompt = $@"You are an executive university administrative assistant.
Summarize the following pending student requests in a clear, 3-sentence executive paragraph.

<DATA>
Total Pending: {realTotal}
Counts: {categoryCountsText}

Details by Category:
{requestsDetail}
</DATA>

REQUIRED SENTENCE STRUCTURE:
Sentence 1: State that there are currently {realTotal} pending requests ({categoryCountsText}).
Sentence 2 & 3: Summarize the underlying reasons students provided for each request category (e.g. why they are enrolling, unenrolling, or registering).

Write ONLY the paragraph text. Do not output markdown code blocks or quotes.";

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.3,
            MaxTokens = 1500
        };

        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        var fullAccumulatedText = new System.Text.StringBuilder();
        await foreach (var chunk in chatCompletion.GetStreamingChatMessageContentsAsync(prompt, executionSettings, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                fullAccumulatedText.Append(chunk.Content);
                yield return JsonSerializer.Serialize(new { type = "chunk", text = chunk.Content }, SseJsonOptions);
            }
        }

        string finalText = fullAccumulatedText.ToString().Trim();
        if (finalText.StartsWith("```")) finalText = finalText.Replace("```", "").Trim();
        if (finalText.StartsWith("\"") && finalText.EndsWith("\"") && finalText.Length > 2)
            finalText = finalText.Substring(1, finalText.Length - 2).Trim();

        bool isUnusable =
    string.IsNullOrWhiteSpace(finalText) ||
    finalText.Length < 25 ||
    finalText.Contains("User Safety") ||
    finalText.Contains("content policy") ||
    finalText.ToLower().StartsWith("i cannot") ||
    finalText.ToLower().StartsWith("as an ai");

        var completeSummaryObj = new EnrollmentRequestAiSummaryDto
        {
            TotalPendingRequests = realTotal,
            Categories = categories,
            SummaryNote = isUnusable
                ? $"There are {realTotal} pending requests awaiting administrative review ({categoryCountsText})."
                : finalText,
            IsAiGenerated = !isUnusable,
            AiStatusMessage = isUnusable
                ? "Model output was unusable or blank. Using database fallback."
                : "AI summary generated successfully via Qwen model."
        };
        _cache.Set(SummaryCacheKey, completeSummaryObj, TimeSpan.FromSeconds(60));
        yield return JsonSerializer.Serialize(new { type = "done", summary = completeSummaryObj }, SseJsonOptions);
    }

}