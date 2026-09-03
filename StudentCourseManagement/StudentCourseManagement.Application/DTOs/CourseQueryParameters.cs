using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentCourseManagement.Application.DTOs;

public class CourseQueryParameters
{
    private const int MaxPageSize = 50;
    private int _pageSize = 10;
    public string? SearchTerm { get; set; }

    public int? MinCredits { get; set; }
    public int? MaxCredits { get; set; }
    public string SortBy { get; set; } = "Name"; 
    public bool IsDescending { get; set; } = false;
    public int PageIndex { get; set; } = 1;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = (value > MaxPageSize) ? MaxPageSize : (value <= 0 ? 10 : value);
    }
}
