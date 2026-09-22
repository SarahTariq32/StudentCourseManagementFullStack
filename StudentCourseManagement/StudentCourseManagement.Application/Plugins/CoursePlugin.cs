using System.ComponentModel;
using Microsoft.SemanticKernel;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.Application.Plugins;

public class CoursePlugin
{
    private readonly ICourseRepository _courseRepository;
    private readonly int _authenticatedStudentId;

    public CoursePlugin(ICourseRepository courseRepository, int authenticatedStudentId)
    {
        _courseRepository = courseRepository;
        _authenticatedStudentId = authenticatedStudentId;
    }

    [KernelFunction, Description("Gets the minimal list of courses (id, name, credits) that the authenticated student is currently enrolled in.")]
    public async Task<List<object>> GetEnrolledCoursesAsync()
    {
        var allCourses = await _courseRepository.GetAllAsync();
        var enrolledList = new List<object>();

        foreach (var course in allCourses)
        {
            bool isEnrolled = await _courseRepository.IsStudentEnrolledAsync(_authenticatedStudentId, course.Id);
            if (isEnrolled)
            {
                enrolledList.Add(new
                {
                    id = course.Id,
                    name = course.Name,
                    credits = course.Credits
                });
            }
        }

        return enrolledList;
    }

    [KernelFunction, Description("Searches and returns up to 5 available catalog courses matching a keyword or topic. Never returns the full catalog.")]
    public async Task<List<object>> GetSimilarAvailableCoursesAsync(
        [Description("Keyword or topic to filter available catalog courses by (e.g. 'Database', 'Math', 'AI').")] string keyword)
    {
        var availableCourses = await _courseRepository.GetAvailableCoursesForStudentsAsync(_authenticatedStudentId);
        var filteredQuery = availableCourses.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string term = keyword.Trim().ToLower();
            filteredQuery = filteredQuery.Where(c => c.Name.ToLower().Contains(term));
        }

        var matched = filteredQuery.Take(5).ToList();
        if (matched.Count == 0)
        {
            matched = availableCourses.Take(5).ToList();
        }

        return matched.Select(c => new
        {
            id = c.Id,
            name = c.Name,
            credits = c.Credits
        }).ToList<object>();
    }
}