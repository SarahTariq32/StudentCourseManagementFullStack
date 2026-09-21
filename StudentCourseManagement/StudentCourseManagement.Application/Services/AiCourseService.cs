using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Interfaces;
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

        var allCourses = await _courseRepository.GetAllAsync();
        var enrolledCourses = new List<object>();

        foreach (var course in allCourses)
        {
            bool isEnrolled = await _courseRepository.IsStudentEnrolledAsync(student.Id, course.Id);
            if (isEnrolled)
                enrolledCourses.Add(new { course.Id, course.Name, course.Credits });
        }

        string enrolledDataJson = JsonSerializer.Serialize(enrolledCourses);

        string prompt = @"You are a helpful academic assistant. The student's enrolled courses are below.

<STUDENT_ENROLLED_COURSES>
" + enrolledDataJson + @"
</STUDENT_ENROLLED_COURSES>

RULES:
1. Answer ONLY based on <STUDENT_ENROLLED_COURSES>. Do not mention any other courses.
2. If the list is empty, say the student is not enrolled in any courses.
3. Reply ONLY with this JSON (no markdown, no extra text):
{
  ""matchedCourses"": [{ ""id"": 1, ""name"": ""Course Title"", ""reason"": ""Why relevant"" }],
  ""advisorNote"": ""Your answer here""
}

Student Question: """ + query + @"""";

        try
        {
            var executionSettings = new OpenAIPromptExecutionSettings { Temperature = 0.2, MaxTokens = 800 };
            var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatCompletion.GetChatMessageContentAsync(prompt, executionSettings);
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

    public async Task<CourseRecommendationResponseDto> SearchFreeformAsync(string query)
    {
        var catalogCourses = await _courseRepository.GetAvailableCoursesForStudentsAsync();
        var simplifiedCatalog = catalogCourses.Select(c => new { c.Id, c.Name, c.Credits });
        string catalogDataJson = JsonSerializer.Serialize(simplifiedCatalog);

        string prompt = @"You are a general academic advisor. Available courses are below.

<UNIVERSITY_CATALOG_COURSES>
" + catalogDataJson + @"
</UNIVERSITY_CATALOG_COURSES>

RULES:
1. Recommend from <UNIVERSITY_CATALOG_COURSES> or give general academic guidance.
2. Reply ONLY with this JSON (no markdown, no extra text):
{
  ""matchedCourses"": [{ ""id"": 0, ""name"": ""Course Title"", ""reason"": ""Why relevant"" }],
  ""advisorNote"": ""Your advice here""
}

Student Query: """ + query + @"""";

        try
        {
            var executionSettings = new OpenAIPromptExecutionSettings { Temperature = 0.7, MaxTokens = 800 };
            var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatCompletion.GetChatMessageContentAsync(prompt, executionSettings);
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
    public async Task<EnrollmentRequestAiSummaryDto> GetPendingRequestsSummaryAsync()
    {
        if (_cache.TryGetValue(SummaryCacheKey, out EnrollmentRequestAiSummaryDto? cachedSummary) && cachedSummary != null)
        {
            return cachedSummary;
        }
        await _summaryCacheLock.WaitAsync();
        try
        {

            if (_cache.TryGetValue(SummaryCacheKey, out cachedSummary) && cachedSummary != null)
            {
                return cachedSummary;
            }

            var freshSummary = await GenerateSummaryInternalAsync();
            _cache.Set(SummaryCacheKey, freshSummary, TimeSpan.FromSeconds(60));

            return freshSummary;
        }
        finally
        {
            _summaryCacheLock.Release();
        }
    }

    public void InvalidatePendingRequestsSummaryCache()
    {
        _cache.Remove(SummaryCacheKey);
    }

    private async Task<EnrollmentRequestAiSummaryDto> GenerateSummaryInternalAsync()
    {
        var pendingRequests = await _courseRepository.GetPendingEnrollmentRequestsAsync();

        if (pendingRequests == null || pendingRequests.Count == 0)
        {
            return new EnrollmentRequestAiSummaryDto
            {
                TotalPendingRequests = 0,
                Categories = new List<RequestCategoryCountDto>(),
                SummaryNote = "No pending registration or course requests found.",
                IsAiGenerated = false,
                AiStatusMessage = "No active requests to analyze."
            };
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

        string categoryCountsText = string.Join(", ", categories.Select(c => $"{c.Count} {c.Category}s"));
        string categorySummaryText = string.Join(", ", categories.Select(c => $"{c.Category}: {c.Count}"));

        string summaryNote = $"There are {realTotal} pending requests awaiting administrative review ({categoryCountsText}).";

        bool isAiGenerated = false;
        string aiStatusMessage = string.Empty;
        int retryAfterSeconds = 0;

        try
        {
            var groupedDescriptions = pendingRequests
                .GroupBy(r => r.RequestType?.Trim() ?? "General Request")
                .Select(g =>
                    $"Category '{g.Key}' ({g.Count()} requests):\n" +
                    string.Join("\n", g.Select(r =>
                        $"  - Student: {r.StudentName}, Course: {r.CourseName ?? "N/A"}, Reason: '{r.Reason ?? "No reason provided"}'"
                    ).Take(10)));

            string requestsDetail = string.Join("\n\n", groupedDescriptions);
            string prompt = $@"You are an executive university administrative assistant.
Analyze the following pending student requests and summarize them following the EXACT TEMPLATE below.

<PENDING_REQUESTS>
Total Pending: {realTotal}
Counts Breakdown: {categoryCountsText}

Details by Category:
{requestsDetail}
</PENDING_REQUESTS>

EXACT TEMPLATE TO FOLLOW:
""There are currently {realTotal} pending requests ({categoryCountsText}). Enrollment requests are generally because of [summarize enrollment reasons]. Unenrollment requests are generally because of [summarize unenrollment reasons]. Registration requests are generally because of [summarize registration reasons].""

RULES:
1. Start sentence 1 with the exact total and the explicit breakdown numbers in parentheses: ({categoryCountsText}).
2. Explain the reasons for each category based on the student notes provided in <PENDING_REQUESTS>.
3. Output ONLY the completed text paragraph following the template structure. No quotes, no markdown blocks, no extra intro.";

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.1,
                MaxTokens = 2000
            };

            var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            var response = await chatCompletion.GetChatMessageContentAsync(prompt, executionSettings);
            string rawText = response.ToString().Trim();

            if (rawText.StartsWith("\"") && rawText.EndsWith("\""))
                rawText = rawText.Substring(1, rawText.Length - 2).Trim();

            bool isUnusable =
                rawText.Length < 20 ||
                rawText.StartsWith("We need") ||
                rawText.StartsWith("Must ") ||
                rawText.Contains("User Safety") ||
                rawText.Contains("content policy") ||
                rawText.ToLower().Contains("i cannot") ||
                rawText.ToLower().Contains("as an ai");

            if (!string.IsNullOrWhiteSpace(rawText) && !isUnusable)
            {
                summaryNote = rawText;
                isAiGenerated = true;
                aiStatusMessage = "AI summary generated successfully via Qwen model.";
            }
            else
            {
                aiStatusMessage = $"Model output was unusable: '{rawText}'";
            }
        }
        catch (Exception ex)
        {
            isAiGenerated = false;
            string details = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            aiStatusMessage = $"EXACT ERROR: {ex.GetType().Name} - {details}";
        }

        return new EnrollmentRequestAiSummaryDto
        {
            TotalPendingRequests = realTotal,
            Categories = categories,
            SummaryNote = summaryNote,
            IsAiGenerated = isAiGenerated,
            AiStatusMessage = aiStatusMessage,
            RetryAfterSeconds = retryAfterSeconds
        };
    }

    private CourseRecommendationResponseDto CleanAndParseJsonResponse(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
            return new CourseRecommendationResponseDto();

        string cleaned = rawResponse.Trim();
        if (cleaned.StartsWith("```json")) cleaned = cleaned.Substring(7);
        else if (cleaned.StartsWith("```")) cleaned = cleaned.Substring(3);
        if (cleaned.EndsWith("```")) cleaned = cleaned.Substring(0, cleaned.Length - 3);
        cleaned = cleaned.Trim();

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<CourseRecommendationResponseDto>(cleaned, options)
                   ?? new CourseRecommendationResponseDto();
        }
        catch (JsonException)
        {
            return new CourseRecommendationResponseDto { AdvisorNote = rawResponse };
        }
    }
}