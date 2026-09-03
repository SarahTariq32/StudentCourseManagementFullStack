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
    private readonly IUserRepository _userRepository;

    public StudentsController(IStudentService studentService, ICourseService courseService, IUserRepository userRepository)
    {
        _studentService = studentService;
        _courseService = courseService;
        _userRepository = userRepository;
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMyProfile()
    {
        var student = await GetCurrentStudentProfileAsync();
        if (student == null)
        {
            return Ok(new { isVerified = false, message = "Account not linked to an active student record. Please request student registration from Admin." });
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
            return BadRequest(new { message = "No verified student profile linked to your account. Please contact an Admin." });

        var updated = await _studentService.UpdateAsync(student.Id, dto);
        if (!updated) return NotFound(new { message = "Failed to update profile." });

        return Ok(new { message = "Profile updated successfully." });
    }




    [HttpGet]
    [Authorize(Roles = "Admin,admin")]
    public async Task<IActionResult> GetAll([FromQuery] StudentQueryParameters queryParams)
    {
        var result = await _studentService.GetPagedAsync(queryParams);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,admin")]
    public async Task<IActionResult> GetById(int id)
    {
        if (id <= 0) return BadRequest("Invalid student ID.");
        var student = await _studentService.GetByIdAsync(id);
        if (student == null) return NotFound($"Student with ID {id} not found.");
        return Ok(student);
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



    [HttpPost("enroll")]
    [Authorize(Roles = "Student,student")]
    public async Task<IActionResult> EnrollDirectly([FromBody] CreateEnrollmentRequestDto dto)
    {
        if (dto == null || dto.CourseId <= 0)
            return BadRequest("Valid course ID is required.");

        var student = await GetCurrentStudentProfileAsync();
        if (student == null)
            return BadRequest(new { message = "You must be a Verified Student to enroll in courses. Please request student registration first." });

        // Enforce Max 7 Courses Limit
        int currentCount = student.EnrolledCourses?.Count ?? 0;
        if (currentCount >= 7)
            return BadRequest(new { message = "You have reached the maximum limit of 7 enrolled courses." });

        var result = await _courseService.EnrollStudentAsync(student.Id, dto.CourseId);
        if (!result.Success)
            return BadRequest(new { message = result.Message });

        return Ok(new { message = result.Message });
    }

    [HttpPost("request-enrollment")]
    [Authorize(Roles = "Student,student")]
    public async Task<IActionResult> RequestEnrollment([FromBody] CreateEnrollmentRequestDto dto)
    {
        if (IsAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Admins cannot submit enrollment requests.");
        }

        if (dto == null)
            return BadRequest("Request payload is missing.");

        var loggedInUsername = User.FindFirst(ClaimTypes.Name)?.Value;
        var user = await _userRepository.GetByUsernameAsync(loggedInUsername ?? "");
        var student = await GetCurrentStudentProfileAsync();

        // Handle Account Creation / Registration Requests
        if (dto.CourseId <= 0 || (dto.Reason != null && dto.Reason.Contains("ACCOUNT_CREATION_REQUEST")))
        {
            int regStudentId = student?.Id ?? user?.Id ?? 0;
            if (regStudentId <= 0)
                return BadRequest(new { message = "User account could not be found." });

            var regResult = await _courseService.CreateEnrollmentRequestAsync(regStudentId, 0, "Register", dto.Reason);
            if (!regResult.Success)
                return BadRequest(new { message = regResult.Message });

            return Ok(new { message = regResult.Message });
        }

        if (student == null)
            return BadRequest(new { message = "You must be a Verified Student to submit course enrollment requests. Please request student registration first." });

        // Enforce Max 7 Courses Limit on Enrollment Requests
        int enrolledCount = student.EnrolledCourses?.Count ?? 0;
        if (enrolledCount >= 7)
        {
            return BadRequest(new { message = "You cannot request enrollment because you are already enrolled in the maximum limit of 7 courses." });
        }

        int courseId = dto.CourseId;
        if (courseId <= 0 && !string.IsNullOrWhiteSpace(dto.CourseName))
        {
            var allCourses = await _courseService.GetAllAsync();
            var matchedCourse = allCourses.FirstOrDefault(c => string.Equals(c.Name, dto.CourseName, StringComparison.OrdinalIgnoreCase));
            if (matchedCourse != null)
            {
                courseId = matchedCourse.Id;
            }
        }

        if (courseId <= 0)
            return BadRequest(new { message = "Valid course ID or course name is required." });

        var result = await _courseService.CreateEnrollmentRequestAsync(student.Id, courseId, "Enroll", dto.Reason);
        if (!result.Success)
            return BadRequest(new { message = result.Message });

        return Ok(new { message = result.Message });
    }

    [HttpPost("request-unenrollment")]
    [Authorize(Roles = "Student,student")]
    public async Task<IActionResult> RequestUnenrollment([FromBody] CreateEnrollmentRequestDto dto)
    {
        if (IsAdmin())
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Admins cannot submit unenrollment requests.");
        }

        if (dto == null)
            return BadRequest(new { message = "Request payload is missing." });

        var student = await GetCurrentStudentProfileAsync();
        if (student == null)
            return BadRequest(new { message = "You must be a Verified Student to submit unenrollment requests." });

        int courseId = dto.CourseId;

        // Guaranteed courseId resolution by trimming and case-insensitive string matching
        if (courseId <= 0 && !string.IsNullOrWhiteSpace(dto.CourseName))
        {
            var targetName = dto.CourseName.Trim();
            var allCourses = await _courseService.GetAllAsync();

            var matchedCourse = allCourses.FirstOrDefault(c =>
                string.Equals(c.Name?.Trim(), targetName, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Trim().Contains(targetName, StringComparison.OrdinalIgnoreCase));

            if (matchedCourse != null)
            {
                courseId = matchedCourse.Id;
            }
        }

        if (courseId <= 0)
        {
            return BadRequest(new { message = $"Could not match course name '{dto.CourseName}' to a valid database record." });
        }

        var result = await _courseService.CreateEnrollmentRequestAsync(student.Id, courseId, "Unenroll", dto.Reason);
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new { message = result.Message });
    }

    private async Task<StudentDto?> GetCurrentStudentProfileAsync()
    {
        var loggedInUsername = User.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrEmpty(loggedInUsername)) return null;

        var user = await _userRepository.GetByUsernameAsync(loggedInUsername);
        var student = await _studentService.GetByNameAsync(loggedInUsername);
        if (student != null) return student;

        if (user != null)
        {
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                student = await _studentService.GetByNameAsync(user.Email);
                if (student != null) return student;
            }
            if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                student = await _studentService.GetByNameAsync(user.FullName);
                if (student != null) return student;
            }
        }

        return null;
    }

    private bool IsAdmin()
    {
        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
        return string.Equals(roleClaim, "Admin", StringComparison.OrdinalIgnoreCase);
    }



}