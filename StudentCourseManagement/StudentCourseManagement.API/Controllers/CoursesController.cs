using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.API.Controllers;

[Authorize(Roles = "Admin,admin")]
[ApiController]
[Route("api/[controller]")]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _service;

    public CoursesController(ICourseService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] CourseQueryParameters queryParams)
    {
        var result = await _service.GetPagedAsync(queryParams);
        return Ok(result);
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
    public async Task<IActionResult> Create(CreateCourseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var course = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = course.Id }, course);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateCourseDto dto)
    {
        if (id <= 0) return BadRequest("Invalid course ID.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _service.UpdateAsync(id, dto);
        if (!updated) return NotFound($"Course with ID {id} not found.");
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (id <= 0) return BadRequest("Invalid course ID.");
        var deleted = await _service.DeleteAsync(id);
        if (!deleted) return NotFound($"Course with ID {id} not found.");
        return NoContent();
    }


    [HttpGet("requests")]
    public async Task<IActionResult> GetPendingRequests()
    {
        var requests = await _service.GetPendingEnrollmentRequestsAsync();
        return Ok(requests);
    }

    [HttpPost("requests/{requestId}/process")]
    public async Task<IActionResult> ProcessRequest(int requestId, [FromQuery] bool approve)
    {
        if (requestId <= 0)
            return BadRequest("Invalid request ID.");

        var result = await _service.ProcessEnrollmentRequestAsync(requestId, approve);
        if (!result.Success)
            return BadRequest(result.Message);

        return Ok(result.Message);
    }
}