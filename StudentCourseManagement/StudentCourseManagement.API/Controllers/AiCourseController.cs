using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("AiSearchLimit")]
public class AiCourseController : ControllerBase
{
    private readonly IAiCourseService _aiCourseService;

    public AiCourseController(IAiCourseService aiCourseService)
    {
        _aiCourseService = aiCourseService;
    }

    [HttpGet("search/strict")]
    [Authorize]
    public async Task<IActionResult> SearchStrict([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { message = "Query parameter is required." });

        var username = User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized(new { message = "Could not resolve student identity from token." });

        var result = await _aiCourseService.SearchStrictAsync(username, query);
        return Ok(result);
    }

    [HttpGet("search/free")]
    [Authorize]
    public async Task<IActionResult> SearchFreeform([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { message = "Query parameter is required." });

        var result = await _aiCourseService.SearchFreeformAsync(query);
        return Ok(result);
    }

    [HttpGet("enrollmentrequests/ai-summary")]
    [Authorize(Roles = "Admin,admin")]
    public async Task<IActionResult> GetPendingRequestsSummary()
    {
        var summary = await _aiCourseService.GetPendingRequestsSummaryAsync();
        return Ok(summary);
    }
}