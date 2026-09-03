using StudentCourseManagement.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace StudentCourseManagement.Application.Interfaces;

public interface IStudentService
{
    Task<List<StudentDto>> GetAllAsync();
    Task<PagedResultDto<StudentDto>> GetPagedAsync(StudentQueryParameters queryParams);
    Task<StudentDto?> GetByIdAsync(int id);
    Task<StudentDto> CreateAsync(CreateStudentDto dto);
    Task<bool> UpdateAsync(int id, UpdateStudentDto dto);
    Task<bool> DeleteAsync(int id);
    Task<StudentDto?> GetByNameAsync(string name);
}