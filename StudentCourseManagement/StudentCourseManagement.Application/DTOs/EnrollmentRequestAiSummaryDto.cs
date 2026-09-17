using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentCourseManagement.Application.DTOs;

public class RequestCategoryCountDto
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class EnrollmentRequestAiSummaryDto
{
    public int TotalPendingRequests { get; set; }
    public List<RequestCategoryCountDto> Categories { get; set; } = new();
    public string SummaryNote { get; set; } = string.Empty;
}
