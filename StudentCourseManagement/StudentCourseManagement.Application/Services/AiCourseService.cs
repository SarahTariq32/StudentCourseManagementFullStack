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
            {
                enrolledCourses.Add(new { course.Id, course.Name, course.Credits });
            }
        }
        string enrolledDataJson = JsonSerializer.Serialize(enrolledCourses);

        string prompt = @"You are a personal academic advisor for a student.
Below is the SINGLE SOURCE OF TRUTH containing ONLY the courses this student is currently ENROLLED in:

<STUDENT_ENROLLED_COURSES>
" + enrolledDataJson + @"
</STUDENT_ENROLLED_COURSES>

CRITICAL RULES:
1. Base your response ONLY on the courses listed in <STUDENT_ENROLLED_COURSES>.
2. State clearly how many courses they are enrolled in based strictly on <STUDENT_ENROLLED_COURSES>.
3. If <STUDENT_ENROLLED_COURSES> is empty or '[]', state: 'You are not currently enrolled in any courses.'
4. Return your response strictly as a JSON object matching this schema (no markdown, no extra text):
   {
     ""matchedCourses"": [
       {
         ""id"": 1,
         ""name"": ""Course Title"",
         ""reason"": ""Why this course is relevant to the student's question""
       }
     ],
     ""advisorNote"": ""Summary advice regarding their active enrolled courses""
   }

Student Query: """ + query + @"""";

        var executionSettings = new OpenAIPromptExecutionSettings { Temperature = 0.1 };
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