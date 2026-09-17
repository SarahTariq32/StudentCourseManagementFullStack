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
                AdvisorNote = "Could not find a student record linked to your account. " +
                              "Please contact an administrator."
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

        string prompt = @"You are a helpful, natural academic assistant for a logged-in student.
Below is the list of courses this student is currently ENROLLED in:

<STUDENT_ENROLLED_COURSES>
" + enrolledDataJson + @"
</STUDENT_ENROLLED_COURSES>

RULES FOR YOUR RESPONSE:
1. Answer the student's question directly, naturally, and conversationally.
2. Ground your answer ONLY in <STUDENT_ENROLLED_COURSES>. Do NOT mention, recommend, or discuss any course NOT listed there.
3. Do NOT repeat boilerplate stats unless the user explicitly asks for a summary of their total course load.
4. If the user asks about a topic NOT in their enrolled list, naturally explain they aren't currently taking a course on that topic.
5. Return your response strictly as a JSON object matching this schema (no markdown, no extra text):
   {
     ""matchedCourses"": [
       {
         ""id"": 1,
         ""name"": ""Course Title"",
         ""reason"": ""Specific answer addressing their question""
       }
     ],
     ""advisorNote"": ""Direct, natural answer to the student's question""
   }

Student Question: """ + query + @"""";

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.2,
            MaxTokens = 800
        };

        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        var result = await chatCompletion.GetChatMessageContentAsync(prompt, executionSettings);

        return CleanAndParseJsonResponse(result.ToString());
    }

    /// <summary>
    /// FREEFORM MODE: General advice using the full available course catalog.
    /// Higher temperature (0.7) allows more creative recommendations — contrasts with
    /// Strict Mode's grounded, low-temperature responses for the comparison demo.
    /// </summary>
    public async Task<CourseRecommendationResponseDto> SearchFreeformAsync(string query)
    {
        var catalogCourses = await _courseRepository.GetAvailableCoursesForStudentsAsync();
        var simplifiedCatalog = catalogCourses.Select(c => new { c.Id, c.Name, c.Credits });
        string catalogDataJson = JsonSerializer.Serialize(simplifiedCatalog);

        string prompt = @"You are a general academic advisor with access to all university catalog courses listed below:

<UNIVERSITY_CATALOG_COURSES>
" + catalogDataJson + @"
</UNIVERSITY_CATALOG_COURSES>

RULES:
1. Recommend courses from <UNIVERSITY_CATALOG_COURSES> when relevant, or provide general academic guidance.
2. Return your response strictly as a JSON object matching this schema (no markdown, no extra text):
   {
     ""matchedCourses"": [
       {
         ""id"": 0,
         ""name"": ""Course Title"",
         ""reason"": ""Recommendation rationale""
       }
     ],
     ""advisorNote"": ""General academic advice""
   }

Student Query: """ + query + @"""";

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.7,
            MaxTokens = 800
        };

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
                SummaryNote = "No pending registration or course requests found."
            };
        }

        int realTotal = pendingRequests.Count;
        var formattedRequests = pendingRequests.Select(r => new
        {
            Student = r.StudentName,
            Type = r.RequestType,
            Course = string.IsNullOrWhiteSpace(r.CourseName) ? "N/A" : r.CourseName,
            Reason = string.IsNullOrWhiteSpace(r.Reason) ? "No reason specified" : r.Reason
        }).ToList();

        string requestsJson = JsonSerializer.Serialize(formattedRequests);
        string prompt = $@"You are an administrative AI summarizer.
Analyze these {realTotal} pending student requests:

{requestsJson}

CRITICAL MANDATES:
1. Use the EXACT value of the ""Type"" field from each request as the category name. Do NOT rename, reword, or append words like 'Request' to the category. If the type is ""Enrollment Request"", the category must be ""Enrollment Request"" — not ""Course Enrollment Request"" or ""Enrollment Request Request"".
2. Ensure the sum of all category counts EXACTLY equals {realTotal}.
3. Write a short 2-sentence executive summary mentioning specific student names or reason patterns.
4. OUTPUT FORMAT: Reply ONLY with a raw JSON object. No markdown backticks, no intro text, no explanations.

Use this exact schema:
{{
  ""totalPendingRequests"": {realTotal},
  ""categories"": [
    {{ ""category"": ""<exact Type value from data>"", ""count"": 1 }}
  ],
  ""summaryNote"": ""2-sentence executive summary here.""
}}";

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.1,
            MaxTokens = 800
        };

        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        var response = await chatCompletion.GetChatMessageContentAsync(prompt, executionSettings);
        string rawText = response.ToString().Trim();

        if (rawText.StartsWith("```"))
        {
            int firstNewLine = rawText.IndexOf('\n');
            if (firstNewLine != -1) rawText = rawText.Substring(firstNewLine + 1);
            if (rawText.EndsWith("```")) rawText = rawText.Substring(0, rawText.Length - 3);
        }
        
        int jsonStart = rawText.IndexOf('{');
        int jsonEnd = rawText.LastIndexOf('}');
        if (jsonStart != -1 && jsonEnd != -1 && jsonEnd > jsonStart)
            rawText = rawText.Substring(jsonStart, jsonEnd - jsonStart + 1);

        EnrollmentRequestAiSummaryDto summary;
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            summary = JsonSerializer.Deserialize<EnrollmentRequestAiSummaryDto>(rawText, options)
                      ?? new EnrollmentRequestAiSummaryDto();
        }
        catch (JsonException)
        {

            var manualCategories = pendingRequests
                .GroupBy(r => string.IsNullOrWhiteSpace(r.RequestType) ? "General Request" : r.RequestType)
                .Select(g => new RequestCategoryCountDto
                {
                    Category = g.Key, 
                    Count = g.Count()
                })
                .ToList();

            summary = new EnrollmentRequestAiSummaryDto
            {
                TotalPendingRequests = realTotal,
                Categories = manualCategories,
                SummaryNote = $"There are {realTotal} pending requests awaiting administrative review."
            };
        }

        summary.TotalPendingRequests = realTotal;

        if (summary.Categories == null || !summary.Categories.Any())
        {
            summary.Categories = new List<RequestCategoryCountDto>
            {
                new RequestCategoryCountDto { Category = "Pending Review", Count = realTotal }
            };
        }

        int currentSum = summary.Categories.Sum(c => c.Count);
        if (currentSum != realTotal)
        {
            int diff = realTotal - currentSum;
            summary.Categories.First().Count += diff;
        }

        return summary;
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