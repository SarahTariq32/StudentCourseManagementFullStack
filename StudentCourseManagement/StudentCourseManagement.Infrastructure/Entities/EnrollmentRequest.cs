using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentCourseManagement.Infrastructure.Entities;

public class EnrollmentRequest
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public int CourseId { get; set; }
    public virtual Course Course { get; set; } = null!;

    public string RequestType { get; set; } = "Enroll";
    public string? Reason { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime RequestedOn { get; set; } = DateTime.UtcNow;
}