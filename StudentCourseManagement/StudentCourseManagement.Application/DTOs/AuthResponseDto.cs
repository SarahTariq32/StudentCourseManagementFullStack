using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentCourseManagement.Application.DTOs
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = null!;

        public string RefreshToken { get; set; } = null!;

        public DateTime RefreshTokenExpiryTime { get; set; }
    }
}
