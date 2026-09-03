using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly ICourseService _courseService;

    public StudentsController(IStudentService studentService, ICourseService courseService)
    {
        _studentService = studentService;
        _courseService = courseService;
    }

    // ==========================================
    // ADMIN ENDPOINTS
    // ==========================================

    [HttpGet]
    [Authorize(Roles = "Admin,admin")]
    public async Task<IActionResult> GetAll([FromQuery] StudentQueryParameters queryParams)
    {
        var result = await _studentService.GetPagedAsync(queryParams);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetById(int id)
    {
        if (id <= 0) return BadRequest("Invalid student ID.");

        var student = await _studentService.GetByIdAsync(id);
        if (student == null) return NotFound($"Student with ID {id} not found.");

        return Ok(student);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,admin")]
    public async Task<IActionResult> Create(CreateStudentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var created = await _studentService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,admin")]
    public async Task<IActionResult> Update(int id, UpdateStudentDto dto)
    {
        if (id <= 0) return BadRequest("Invalid student ID.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _studentService.UpdateAsync(id, dto);
        if (!updated) return NotFound($"Student with ID {id} not found.");

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,admin")]
    public async Task<IActionResult> Delete(int id)
    {
        if (id <= 0) return BadRequest("Invalid student ID.");

        var deleted = await _studentService.DeleteAsync(id);
        if (!deleted) return NotFound($"Student with ID {id} not found.");

        return NoContent();
    }

    // ==========================================
    // STUDENT SELF-SERVICE & VERIFICATION ENDPOINTS
    // ==========================================

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMyProfile()
    {
        var student = await GetCurrentStudentProfileAsync();
        if (student == null)
        {
            return Ok(new { isVerified = false, message = "Profile not linked by Admin yet." });
        }

        return Ok(new { isVerified = true, student });
    }

    [HttpPut("me")]
    [Authorize(Roles = "Student,student")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateStudentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var student = await GetCurrentStudentProfileAsync();
        if (student == null)
            return BadRequest("No student profile linked to your account. Please contact an Admin.");

        var updated = await _studentService.UpdateAsync(student.Id, dto);
        if (!updated) return NotFound("Failed to update profile.");

        return Ok(new { message = "Profile updated successfully." });
    }

    [HttpPost("enroll")]
    [Authorize(Roles = "Student,student")]
    public async Task<IActionResult> EnrollDirectly([FromBody] CreateEnrollmentRequestDto dto)
    {
        if (dto == null || dto.CourseId <= 0)
            return BadRequest("Valid course ID is required.");

        var student = await GetCurrentStudentProfileAsync();
        if (student == null)
            return BadRequest("No student profile linked to your account. Please contact an Admin.");

        var result = await _courseService.EnrollStudentAsync(student.Id, dto.CourseId);
        if (!result.Success)
            return BadRequest(result.Message);

        return Ok(new { message = result.Message });
    }

    [HttpPost("request-enrollment")]
    [Authorize(Roles = "Student,student")]
    public async Task<IActionResult> RequestEnrollment([FromBody] CreateEnrollmentRequestDto dto)
    {
        if (IsAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                "Admins cannot submit enrollment requests.");
        }

        if (dto == null || dto.CourseId <= 0)
            return BadRequest("Valid course ID is required.");

        var student = await GetCurrentStudentProfileAsync();
        if (student == null)
            return BadRequest("No student profile linked to your account. Please contact an Admin.");

        var result = await _courseService.CreateEnrollmentRequestAsync(student.Id, dto.CourseId, "Enroll", dto.Reason);
        if (!result.Success)
            return BadRequest(result.Message);

        return Ok(result.Message);
    }

    [HttpPost("request-unenrollment")]
    [Authorize(Roles = "Student,student")]
    public async Task<IActionResult> RequestUnenrollment([FromBody] CreateEnrollmentRequestDto dto)
    {
        if (IsAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                "Admins cannot submit unenrollment requests.");
        }

        if (dto == null || dto.CourseId <= 0)
            return BadRequest("Valid course ID is required.");

        var student = await GetCurrentStudentProfileAsync();
        if (student == null)
            return BadRequest("No student profile linked to your account. Please contact an Admin.");

        var result = await _courseService.CreateEnrollmentRequestAsync(student.Id, dto.CourseId, "Unenroll", dto.Reason);
        if (!result.Success)
            return BadRequest(result.Message);

        return Ok(result.Message);
    }

    // ==========================================
    // HELPER METHODS
    // ==========================================

    private async Task<StudentDto?> GetCurrentStudentProfileAsync()
    {
        var loggedInUsername = User.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrEmpty(loggedInUsername)) return null;

        return await _studentService.GetByNameAsync(loggedInUsername);
    }

    private bool IsAdmin()
    {
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
        return string.Equals(roleClaim, "Admin", StringComparison.OrdinalIgnoreCase);
    }
}