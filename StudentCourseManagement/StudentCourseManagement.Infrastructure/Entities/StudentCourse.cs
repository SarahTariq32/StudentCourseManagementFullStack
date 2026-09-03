using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentCourseManagement.Infrastructure.Entities;

public partial class StudentCourse
{
    public int StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public int CourseId { get; set; }
    public virtual Course Course { get; set; } = null!;

    public DateTime EnrolledOn { get; set; }
}
