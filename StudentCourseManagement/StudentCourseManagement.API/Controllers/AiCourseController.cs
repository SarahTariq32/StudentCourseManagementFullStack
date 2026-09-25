using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AiCourseController : ControllerBase
{
    private readonly IAiCourseService _aiCourseService;

    public AiCourseController(IAiCourseService aiCourseService)
    {
        _aiCourseService = aiCourseService;
    }

    [HttpGet("search/strict")]
    [Authorize]
    [EnableRateLimiting("AiSearchLimit")]
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
    [EnableRateLimiting("AiSearchLimit")]
    public async Task<IActionResult> SearchFreeform([FromQuery] string query)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }

        var result = await _aiCourseService.SearchFreeformAsync(username, query);
        return Ok(result);
    }

   


    [HttpGet("enrollmentrequests/ai-summary/stream")]
    [Authorize(Roles = "Admin,admin")]
    public async Task StreamPendingRequestsSummary(CancellationToken cancellationToken)
    {
        // 1. Disable response buffering & set SSE content type
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no"); // Prevents proxy buffering

        // 2. Stream tokens directly to the client as they arrive from Semantic Kernel
        await foreach (var chunk in _aiCourseService.StreamPendingRequestsSummaryTextAsync(cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk))
            {
                // Format as standard Server-Sent Event (SSE) data frame
                var jsonChunk = JsonSerializer.Serialize(chunk);
                await Response.WriteAsync($"data: {jsonChunk}\n\n", cancellationToken);

                // 3. FORCE FLUSH: Sends token over the TCP socket immediately
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
    }
}