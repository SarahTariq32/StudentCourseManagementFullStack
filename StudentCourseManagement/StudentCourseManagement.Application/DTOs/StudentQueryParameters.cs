using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentCourseManagement.Application.DTOs;

public class StudentQueryParameters
{
    private const int MaxPageSize = 50;
    private int _pageSize = 10;
    public string? SearchTerm { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public string SortBy { get; set; } = "Name"; 
    public bool IsDescending { get; set; } = false;
    public int PageIndex { get; set; } = 1;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = (value > MaxPageSize) ? MaxPageSize : (value <= 0 ? 10 : value);
    }
}