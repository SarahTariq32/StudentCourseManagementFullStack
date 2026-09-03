using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentCourseManagement.Application.DTOs
{
    public class UpdateStudentDto
    {
        public string Name { get; set; } = null!;

        public string Email { get; set; } = null!;

        public int Age { get; set; }
    }
}
