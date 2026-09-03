using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Domain.Entities;

namespace StudentCourseManagement.Application.Interfaces;

public interface ICourseRepository
{
    Task<List<Course>> GetAllAsync();
    Task<PagedResultDto<Course>> GetPagedAsync(CourseQueryParameters queryParams);
    Task<List<Course>> GetAvailableCoursesForStudentsAsync();
    Task<Course?> GetByIdAsync(int id);
    Task<Course> AddAsync(Course course);
    Task UpdateAsync(Course course);
    Task DeleteAsync(int id);

    Task<int> GetEnrolledStudentCountAsync(int courseId);
    Task<int> GetStudentEnrolledCoursesCountAsync(int studentId);
    Task<bool> IsStudentEnrolledAsync(int studentId, int courseId);
    Task EnrollStudentAsync(int studentId, int courseId);
    Task UnenrollStudentAsync(int studentId, int courseId);

    Task<(bool Success, string Message)> CreateEnrollmentRequestAsync(int studentId, int courseId, string requestType, string? reason);
    Task<List<EnrollmentRequestResponseDto>> GetPendingEnrollmentRequestsAsync();
    Task<(bool Success, string Message)> ProcessEnrollmentRequestAsync(int requestId, bool approve);
}