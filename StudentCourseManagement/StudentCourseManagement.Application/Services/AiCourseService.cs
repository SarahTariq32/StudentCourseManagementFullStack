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
                AdvisorNote = "Could not find a student record linked to your account."
            };
        }

        var allCourses = await _courseRepository.GetAllAsync();
        var enrolledCourses = new List<object>();

        foreach (var course in allCourses)
        {
            bool isEnrolled = await _courseRepository.IsStudentEnrolledAsync(student.Id, course.Id);
            if (isEnrolled)
            {
                enrolledCourses.Add(new { course.Id, course.Name, course.Credits });
            }
        }

        string enrolledDataJson = JsonSerializer.Serialize(enrolledCourses);

        // REFINED, NATURAL PROMPT
        string prompt = @"You are a helpful, natural academic assistant for a logged-in student.
Below is the list of courses this student is currently ENROLLED in:

<STUDENT_ENROLLED_COURSES>
" + enrolledDataJson + @"
</STUDENT_ENROLLED_COURSES>

RULES FOR YOUR RESPONSE:
1. Answer the student's question directly, naturally, and conversationally.
2. Ground your answer ONLY in <STUDENT_ENROLLED_COURSES>. Do NOT mention, recommend, or discuss any course that is NOT in <STUDENT_ENROLLED_COURSES>.
3. Do NOT repeat boilerplate stats (e.g. 'You are enrolled in X courses totaling Y credits') unless the user explicitly asks for a summary of their total course load.
4. If the user asks about a topic or course that is NOT in their enrolled list, naturally explain that they aren't currently taking a course on that topic.
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
            MaxTokens = 350    
        };

        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        var result = await chatCompletion.GetChatMessageContentAsync(prompt, executionSettings);

        return CleanAndParseJsonResponse(result.ToString());
    }

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

        var executionSettings = new OpenAIPromptExecutionSettings { Temperature = 0.7 };
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        var result = await chatCompletion.GetChatMessageContentAsync(prompt, executionSettings);

        return CleanAndParseJsonResponse(result.ToString());
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