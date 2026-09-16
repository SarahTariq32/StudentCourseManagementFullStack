using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace StudentCourseManagement.Application.DTOs;

public class CourseRecommendationResponseDto
{
    public List<RecommendedCourseDto> MatchedCourses { get; set; } = new();
    public string AdvisorNote { get; set; } = string.Empty;
}

public class RecommendedCourseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}