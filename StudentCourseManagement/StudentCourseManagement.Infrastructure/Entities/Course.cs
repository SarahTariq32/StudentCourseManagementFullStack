using System;
using System.Collections.Generic;

namespace StudentCourseManagement.Infrastructure.Entities;

public partial class Course
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int Credits { get; set; }

    public virtual ICollection<StudentCourse> StudentCourses { get; set; } = new List<StudentCourse>();
}
