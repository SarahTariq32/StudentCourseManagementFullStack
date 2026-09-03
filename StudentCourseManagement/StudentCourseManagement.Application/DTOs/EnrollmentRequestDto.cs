using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentCourseManagement.Application.DTOs;

public class CreateEnrollmentRequestDto
{
    public int CourseId { get; set; }
    public string? Reason { get; set; }
}

public class EnrollmentRequestResponseDto
{
    public int RequestId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public int CourseId { get; set; }
    public string CourseName { get; set; } = null!;
    public string RequestType { get; set; } = null!; 
    public string Reason { get; set; } = null!;
    public string Status { get; set; } = null!; 
    public DateTime RequestedOn { get; set; }
}