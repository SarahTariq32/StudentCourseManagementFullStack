using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.Application.Services;

public class AiCourseService : IAiCourseService
{
    private readonly Kernel _kernel;
    private readonly ICourseRepository _courseRepository;
    private readonly IStudentRepository _studentRepository;

    public AiCourseService(
        Kernel kernel,
        ICourseRepository courseRepository,
        IStudentRepository studentRepository)
    {
        _kernel = kernel;
        _courseRepository = courseRepository;
        _studentRepository = studentRepository;
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

        var executionSettings = new OpenAIPromptExecutionSettings { Temperature = 0.2, MaxTokens = 800 };
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        var result = await chatCompletion.GetChatMessageContentAsync(prompt, executionSettings);
        return CleanAndParseJsonResponse(result.ToString());
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

        var executionSettings = new OpenAIPromptExecutionSettings { Temperature = 0.7, MaxTokens = 800 };
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        var result = await chatCompletion.GetChatMessageContentAsync(prompt, executionSettings);
        return CleanAndParseJsonResponse(result.ToString());
    }

    public async Task<EnrollmentRequestAiSummaryDto> GetPendingRequestsSummaryAsync()
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

        string categorySummaryText = string.Join(", ", categories.Select(c => $"{c.Category}: {c.Count}"));

        string summaryNote = $"There are {realTotal} pending requests awaiting administrative review: {categorySummaryText}.";

        bool isAiGenerated = false;
        string aiStatusMessage = string.Empty;
        int retryAfterSeconds = 0;

        try
        {
            var groupedDescriptions = pendingRequests
                .GroupBy(r => r.RequestType?.Trim() ?? "General Request")
                .Select(g =>
                    $"{g.Key} ({g.Count()}): " +
                    string.Join(", ", g.Select(r =>
                        r.StudentName + (string.IsNullOrWhiteSpace(r.CourseName) ? "" : $" ({r.CourseName})")
                    ).Distinct().Take(5)));

            string requestsDetail = string.Join("\n", groupedDescriptions);

            string prompt = $@"Output exactly 2 sentences summarizing pending university admin requests. Nothing else.

Example output:
There are 5 pending requests: 3 Enrollment Requests from Alice and Bob, and 2 Registration Requests from Charlie. All items require administrative review before processing.

Now write 2 sentences for:
Total: {realTotal}
Breakdown: {categorySummaryText}
Detail:
{requestsDetail}

Your 2 sentences:";

            var executionSettings = new OpenAIPromptExecutionSettings { Temperature = 0.1, MaxTokens = 300 };
            var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            var response = await chatCompletion.GetChatMessageContentAsync(prompt, executionSettings);
            string rawText = response.ToString().Trim();

            if (rawText.StartsWith("\"") && rawText.EndsWith("\""))
                rawText = rawText.Substring(1, rawText.Length - 2).Trim();

            bool isUnusable =
                rawText.Length < 20 ||
                rawText.Length > 600 ||
                rawText.StartsWith("We need") ||
                rawText.StartsWith("Must ") ||
                rawText.StartsWith("I need") ||
                rawText.StartsWith("Let me") ||
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
        }
        catch (Exception ex)
        {
            isAiGenerated = false;

            if (ex.Message.Contains("429") || ex.Message.ToLower().Contains("rate limit") || ex.Message.ToLower().Contains("quota"))
            {
                retryAfterSeconds = 60;
                aiStatusMessage = "OpenRouter AI model rate limit reached (Free Plan). Displaying database fallback summary.";
            }
            else
            {
                aiStatusMessage = "AI model temporarily unavailable. Displaying database fallback summary.";
            }
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