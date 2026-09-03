using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Interfaces;
using StudentCourseManagement.Domain.Entities;

namespace StudentCourseManagement.Application.Services;

public class CourseService : ICourseService
{
    private readonly ICourseRepository _repository;

    public CourseService(ICourseRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<CourseDto>> GetAllAsync()
    {
        try
        {
            var courses = await _repository.GetAllAsync();
            var courseDtos = new List<CourseDto>();

            foreach (var c in courses)
            {
                int enrolledCount = await _repository.GetEnrolledStudentCountAsync(c.Id);
                courseDtos.Add(new CourseDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Credits = c.Credits,
                    EnrolledStudentsCount = enrolledCount
                });
            }

            return courseDtos;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving all courses: {ex.Message}", ex);
        }
    }

    public async Task<List<CourseDto>> GetAvailableCoursesForStudentsAsync()
    {
        try
        {
            var availableCourses = await _repository.GetAvailableCoursesForStudentsAsync();
            return availableCourses.Select(c => new CourseDto
            {
                Id = c.Id,
                Name = c.Name,
                Credits = c.Credits,
                EnrolledStudentsCount = c.StudentCourses?.Count ?? 0
            }).ToList();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving available courses for students: {ex.Message}", ex);
        }
    }

    public async Task<CourseDto?> GetByIdAsync(int id)
    {
        try
        {
            var course = await _repository.GetByIdAsync(id);

            if (course == null)
                return null;

            int enrolledCount = await _repository.GetEnrolledStudentCountAsync(course.Id);

            return new CourseDto
            {
                Id = course.Id,
                Name = course.Name,
                Credits = course.Credits,
                EnrolledStudentsCount = enrolledCount
            };
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving course with ID {id}: {ex.Message}", ex);
        }
    }

    public async Task<CourseDto> CreateAsync(CreateCourseDto dto)
    {
        try
        {
            var course = new Course
            {
                Name = dto.Name,
                Credits = dto.Credits,
            };

            var created = await _repository.AddAsync(course);

            return new CourseDto
            {
                Id = created.Id,
                Name = created.Name,
                Credits = created.Credits,
            };
        }
        catch (Exception ex)
        {
            throw new Exception($"Error creating course: {ex.Message}", ex);
        }
    }

    public async Task<bool> UpdateAsync(int id, UpdateCourseDto dto)
    {
        try
        {
            var course = await _repository.GetByIdAsync(id);

            if (course == null)
                return false;

            course.Name = dto.Name;
            course.Credits = dto.Credits;

            await _repository.UpdateAsync(course);

            return true;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error updating course with ID {id}: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            var course = await _repository.GetByIdAsync(id);

            if (course == null)
                return false;

            await _repository.DeleteAsync(id);

            return true;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error deleting course with ID {id}: {ex.Message}", ex);
        }
    }

    public async Task<(bool Success, string Message)> EnrollStudentAsync(int studentId, int courseId)
    {
        var course = await _repository.GetByIdAsync(courseId);
        if (course == null)
            return (false, "Course does not exist.");

        bool isEnrolled = await _repository.IsStudentEnrolledAsync(studentId, courseId);
        if (isEnrolled)
            return (false, "Student is already enrolled in this course.");

        int studentCourseCount = await _repository.GetStudentEnrolledCoursesCountAsync(studentId);
        if (studentCourseCount >= 7)
            return (false, "Enrollment failed: A student cannot enroll in more than 7 courses simultaneously.");

        int courseStudentCount = await _repository.GetEnrolledStudentCountAsync(courseId);
        if (courseStudentCount >= 50)
            return (false, "Enrollment failed: Course capacity reached (Maximum 50 students). Please contact an Admin for assistance.");

        await _repository.EnrollStudentAsync(studentId, courseId);
        return (true, "Successfully enrolled in the course.");
    }

    public async Task<bool> UnenrollStudentAsync(int studentId, int courseId)
    {
        bool isEnrolled = await _repository.IsStudentEnrolledAsync(studentId, courseId);
        if (!isEnrolled) return false;

        await _repository.UnenrollStudentAsync(studentId, courseId);
        return true;
    }

    public async Task<(bool Success, string Message)> CreateEnrollmentRequestAsync(int studentId, int courseId, string requestType, string? reason)
    {
        return await _repository.CreateEnrollmentRequestAsync(studentId, courseId, requestType, reason);
    }

    public async Task<List<EnrollmentRequestResponseDto>> GetPendingEnrollmentRequestsAsync()
    {
        return await _repository.GetPendingEnrollmentRequestsAsync();
    }

    public async Task<(bool Success, string Message)> ProcessEnrollmentRequestAsync(int requestId, bool approve)
    {
        return await _repository.ProcessEnrollmentRequestAsync(requestId, approve);
    }

    public async Task<PagedResultDto<CourseDto>> GetPagedAsync(CourseQueryParameters queryParams)
    {
        var pagedResult = await _repository.GetPagedAsync(queryParams);

        var dtos = pagedResult.Items.Select(c => new CourseDto
        {
            Id = c.Id,
            Name = c.Name,
            Credits = c.Credits,
            EnrolledStudentsCount = c.StudentCourses?.Count ?? 0
        }).ToList();

        return new PagedResultDto<CourseDto>
        {
            Items = dtos,
            PageIndex = pagedResult.PageIndex,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount
        };
    }
}