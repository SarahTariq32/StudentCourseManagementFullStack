using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentCourseManagement.Application.DTOs
{
    public class CreateCourseDto
    {
        public string Name { get; set; } = null!;

        public int Credits { get; set; }

    }
}
