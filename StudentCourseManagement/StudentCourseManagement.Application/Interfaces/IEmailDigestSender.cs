using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentCourseManagement.Application.Interfaces;

using StudentCourseManagement.Application.DTOs;

public interface IEmailDigestSender
{
    Task SendDigestAsync(EnrollmentRequestAiSummaryDto summary, CancellationToken cancellationToken = default);
}
