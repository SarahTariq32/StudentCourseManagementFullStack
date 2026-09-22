namespace StudentCourseManagement.Application.Interfaces;

using StudentCourseManagement.Application.DTOs;

public interface IAiCourseService
{
    Task<CourseRecommendationResponseDto> SearchStrictAsync(string username, string query);
    Task<CourseRecommendationResponseDto> SearchFreeformAsync(string username, string query); // <-- Updated to include username
    Task<EnrollmentRequestAiSummaryDto> GetPendingRequestsSummaryAsync();
    void InvalidatePendingRequestsSummaryCache();
}