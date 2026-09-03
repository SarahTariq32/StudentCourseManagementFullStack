using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Domain.Entities;

namespace StudentCourseManagement.Application.Interfaces;

public interface IStudentRepository
{
    Task<List<Student>> GetAllAsync();

    Task<Student?> GetByIdAsync(int id);

    Task<Student> AddAsync(Student student);

    Task UpdateAsync(Student student);

    Task DeleteAsync(int id);
    Task<PagedResultDto<Student>> GetPagedAsync(StudentQueryParameters queryParams);
    Task<Student?> GetByNameAsync(string name);
}