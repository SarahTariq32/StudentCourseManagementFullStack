using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCourseManagement.Application.Interfaces;
using System.Security.Claims;

namespace StudentCourseManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AiCourseController : ControllerBase
{
    private readonly IAiCourseService _aiCourseService;

    public AiCourseController(IAiCourseService aiCourseService)
    {
        _aiCourseService = aiCourseService;
    }

    /// <summary>
    /// STRICT MODE: Answers questions ONLY about the logged-in student's enrolled courses.
    /// Resolves the student's identity from the JWT username claim (ClaimTypes.Name),
    /// because the JWT only stores the UsersData ID — not the Students table ID.
    /// The username is used inside AiCourseService to look up the real Student record.
    /// </summary>
    [HttpGet("search/strict")]
    public async Task<IActionResult> SearchStrict([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { message = "Query parameter is required." });

        // JWT stores username under ClaimTypes.Name (set in AuthService.LoginAsync)
        var username = User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized(new { message = "Could not resolve student identity from token." });

        var result = await _aiCourseService.SearchStrictAsync(username, query);
        return Ok(result);
    }

    /// <summary>
    /// FREEFORM MODE: Answers questions using the whole catalog + generic academic knowledge.
    /// No student identity needed — catalog is the same for everyone.
    /// </summary>
    [HttpGet("search/free")]
    public async Task<IActionResult> SearchFreeform([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { message = "Query parameter is required." });

        var result = await _aiCourseService.SearchFreeformAsync(query);
        return Ok(result);
    }
}