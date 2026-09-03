using DomainStudent = StudentCourseManagement.Domain.Entities.Student;
using InfrastructureStudent = StudentCourseManagement.Infrastructure.Entities.Student;

namespace StudentCourseManagement.Infrastructure.Mappings;

public static class StudentMapping
{
    public static DomainStudent ToDomain(this InfrastructureStudent student)
    {
        var domainStudent = new DomainStudent
        {
            Id = student.Id,
            Name = student.Name,
            Email = student.Email,
            Age = student.Age
        };

        if (student.StudentCourses != null && student.StudentCourses.Any())
        {
            foreach (var sc in student.StudentCourses)
            {
                if (sc.Course != null)
                {
                    domainStudent.StudentCourses.Add(new Domain.Entities.StudentCourse
                    {
                        StudentId = sc.StudentId,
                        CourseId = sc.CourseId,
                        Course = new Domain.Entities.Course
                        {
                            Id = sc.Course.Id,
                            Name = sc.Course.Name,
                            Credits = sc.Course.Credits
                        }
                    });
                }
            }
        }

        return domainStudent;
    }

    public static InfrastructureStudent ToInfrastructure(this DomainStudent student)
    {
        return new InfrastructureStudent
        {
            Id = student.Id,
            Name = student.Name,
            Email = student.Email,
            Age = student.Age
        };
    }
}