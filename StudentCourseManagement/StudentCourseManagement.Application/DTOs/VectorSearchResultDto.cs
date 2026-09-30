using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentCourseManagement.Application.DTOs;

public class VectorSearchResultDto
{
    public string DocumentName { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public int ChunkIndex { get; set; }
    public string TextContent { get; set; } = string.Empty;
    public double Score { get; set; }
}