namespace StudentCourseManagement.Application.Interfaces;

using StudentCourseManagement.Application.DTOs;

public interface IAiCourseService
{
    Task<CourseRecommendationResponseDto> SearchStrictAsync(string username, string query);
    Task<CourseRecommendationResponseDto> SearchFreeformAsync(string username, string query);

    IAsyncEnumerable<string> StreamPendingRequestsSummaryTextAsync(CancellationToken cancellationToken = default);
    void InvalidatePendingRequestsSummaryCache();
}