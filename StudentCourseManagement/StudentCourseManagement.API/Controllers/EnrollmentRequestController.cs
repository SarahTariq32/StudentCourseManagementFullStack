using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.API.Controllers;

[ApiController]
[Route("api/enrollmentrequests")]
[Authorize(Roles = "Admin,admin")]
public class EnrollmentRequestsController : ControllerBase
{
    private readonly ICourseService _courseService;
    private readonly IAiCourseService _aiCourseService;

    public EnrollmentRequestsController(ICourseService courseService, IAiCourseService aiCourseService)
    {
        _courseService = courseService;
        _aiCourseService = aiCourseService;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingRequests()
    {
        var requests = await _courseService.GetPendingEnrollmentRequestsAsync();
        return Ok(requests);
    }

    [HttpPost("process/{requestId}")]
    public async Task<IActionResult> ProcessRequest(int requestId, [FromQuery] bool approve)
    {
        var result = await _courseService.ProcessEnrollmentRequestAsync(requestId, approve);
        if (!result.Success)
            return BadRequest(new { message = result.Message });

        return Ok(new { message = result.Message });
    }

    // NEW ENDPOINT: GET /api/enrollmentrequests/ai-summary
    [HttpGet("ai-summary")]
    public async Task<IActionResult> GetAiSummary()
    {
        var summary = await _aiCourseService.GetPendingRequestsSummaryAsync();
        return Ok(summary);
    }
}