using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Domain.Entities;

namespace StudentCourseManagement.Application.Interfaces;

public interface ICourseService
{
    Task<List<CourseDto>> GetAllAsync();
    Task<List<CourseDto>> GetAvailableCoursesForStudentsAsync();
    Task<CourseDto?> GetByIdAsync(int id);
    Task<CourseDto> CreateAsync(CreateCourseDto dto);
    Task<bool> UpdateAsync(int id, UpdateCourseDto dto);
    Task<bool> DeleteAsync(int id);

    Task<(bool Success, string Message)> EnrollStudentAsync(int studentId, int courseId);
    Task<bool> UnenrollStudentAsync(int studentId, int courseId);
    Task<(bool Success, string Message)> CreateEnrollmentRequestAsync(int studentId, int courseId, string requestType, string? reason);
    Task<List<EnrollmentRequestResponseDto>> GetPendingEnrollmentRequestsAsync();
    Task<(bool Success, string Message)> ProcessEnrollmentRequestAsync(int requestId, bool approve);
    Task<PagedResultDto<CourseDto>> GetPagedAsync(CourseQueryParameters queryParams);
}