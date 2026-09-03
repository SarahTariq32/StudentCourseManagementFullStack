using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _service;
    private readonly IStudentService _studentService;

    public CoursesController(ICourseService service, IStudentService studentService)
    {
        _service = service;
        _studentService = studentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] CourseQueryParameters queryParams)
    {
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.Equals(roleClaim, "Student", StringComparison.OrdinalIgnoreCase))
        {
            var loggedInUsername = User.FindFirst(ClaimTypes.Name)?.Value;
            int? studentId = null;
            if (!string.IsNullOrEmpty(loggedInUsername))
            {
                var student = await _studentService.GetByNameAsync(loggedInUsername);
                studentId = student?.Id;
            }

            var availableCourses = await _service.GetAvailableCoursesForStudentsAsync(studentId);
            return Ok(availableCourses);
        }

        var result = await _service.GetPagedAsync(queryParams);
        return Ok(result);
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableCourses()
    {
        var loggedInUsername = User.FindFirst(ClaimTypes.Name)?.Value;
        int? studentId = null;
        if (!string.IsNullOrEmpty(loggedInUsername))
        {
            var student = await _studentService.GetByNameAsync(loggedInUsername);
            studentId = student?.Id;
        }

        var availableCourses = await _service.GetAvailableCoursesForStudentsAsync(studentId);
        return Ok(availableCourses);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (id <= 0) return BadRequest("Invalid course ID.");
        var course = await _service.GetByIdAsync(id);
        if (course == null) return NotFound($"Course with ID {id} not found.");
        return Ok(course);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,admin")]
    public async Task<IActionResult> Create(CreateCourseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var course = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = course.Id }, course);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,admin")]
    public async Task<IActionResult> Update(int id, UpdateCourseDto dto)
    {
        if (id <= 0) return BadRequest("Invalid course ID.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _service.UpdateAsync(id, dto);
        if (!updated) return NotFound($"Course with ID {id} not found.");
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,admin")]
    public async Task<IActionResult> Delete(int id)
    {
        if (id <= 0) return BadRequest("Invalid course ID.");
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound($"Course with ID {id} not found.");
        return NoContent();
    }

    [HttpGet("requests")]
    [HttpGet("pending-requests")]
    [Authorize(Roles = "Admin,admin")]
    public async Task<IActionResult> GetPendingRequests()
    {
        var requests = await _service.GetPendingEnrollmentRequestsAsync();
        return Ok(requests);
    }

    [HttpPost("requests/{requestId}/process")]
    [Authorize(Roles = "Admin,admin")]
    public async Task<IActionResult> ProcessRequest(int requestId, [FromQuery] bool approve)
    {
        if (requestId <= 0)
            return BadRequest(new { message = "Invalid request ID." });

        var result = await _service.ProcessEnrollmentRequestAsync(requestId, approve);
        if (!result.Success)
            return BadRequest(new { message = result.Message });

        return Ok(new { message = result.Message });
    }

    [HttpPost("process-request")]
    [Authorize(Roles = "Admin,admin")]
    public async Task<IActionResult> ProcessRequestFromBody([FromBody] ProcessRequestDto body)
    {
        if (body == null || body.RequestId <= 0)
            return BadRequest(new { message = "Invalid request ID." });

        var result = await _service.ProcessEnrollmentRequestAsync(body.RequestId, body.Approve);
        if (!result.Success)
            return BadRequest(new { message = result.Message });

        return Ok(new { message = result.Message });
    }
}

public class ProcessRequestDto
{
    public int RequestId { get; set; }
    public bool Approve { get; set; }
}