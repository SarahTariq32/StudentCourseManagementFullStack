using DomainCourse = StudentCourseManagement.Domain.Entities.Course;
using InfrastructureCourse = StudentCourseManagement.Infrastructure.Entities.Course;

namespace StudentCourseManagement.Infrastructure.Mappings;

public static class CourseMapping
{
    public static DomainCourse ToDomain(this InfrastructureCourse course)
    {
        return new DomainCourse
        {
            Id = course.Id,
            Name = course.Name,
            Credits = course.Credits
        };
    }

    public static InfrastructureCourse ToInfrastructure(this DomainCourse course)
    {
        return new InfrastructureCourse
        {
            Id = course.Id,
            Name = course.Name,
            Credits = course.Credits
        };
    }
}