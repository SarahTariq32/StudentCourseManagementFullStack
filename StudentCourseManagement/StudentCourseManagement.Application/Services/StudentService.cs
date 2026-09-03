using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Interfaces;
using StudentCourseManagement.Domain.Entities;

namespace StudentCourseManagement.Application.Services;

public class StudentService : IStudentService
{
    private readonly IStudentRepository _repository;

    public StudentService(IStudentRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<StudentDto>> GetAllAsync()
    {
        var students = await _repository.GetAllAsync();
        return students.Select(s => new StudentDto
        {
            Id = s.Id,
            Name = s.Name,
            Email = s.Email,
            Age = s.Age,
            EnrolledCourses = s.StudentCourses?
                .Where(sc => sc.Course != null && !string.IsNullOrWhiteSpace(sc.Course.Name))
                .Select(sc => sc.Course.Name)
                .Distinct()
                .ToList() ?? new List<string>()
        }).ToList();
    }

    public async Task<PagedResultDto<StudentDto>> GetPagedAsync(StudentQueryParameters queryParams)
    {
        var pagedResult = await _repository.GetPagedAsync(queryParams);

        var dtos = pagedResult.Items.Select(s => new StudentDto
        {
            Id = s.Id,
            Name = s.Name,
            Email = s.Email,
            Age = s.Age,
            EnrolledCourses = s.StudentCourses?
                .Where(sc => sc.Course != null && !string.IsNullOrWhiteSpace(sc.Course.Name))
                .Select(sc => sc.Course.Name)
                .Distinct()
                .ToList() ?? new List<string>()
        }).ToList();

        return new PagedResultDto<StudentDto>
        {
            Items = dtos,
            PageIndex = pagedResult.PageIndex,
            PageSize = pagedResult.PageSize,
            TotalCount = pagedResult.TotalCount
        };
    }

    public async Task<StudentDto?> GetByIdAsync(int id)
    {
        var student = await _repository.GetByIdAsync(id);
        if (student == null) return null;

        return new StudentDto
        {
            Id = student.Id,
            Name = student.Name,
            Email = student.Email,
            Age = student.Age,
            EnrolledCourses = student.StudentCourses?
                .Where(sc => sc.Course != null && !string.IsNullOrWhiteSpace(sc.Course.Name))
                .Select(sc => sc.Course.Name)
                .Distinct()
                .ToList() ?? new List<string>()
        };
    }

    public async Task<StudentDto> CreateAsync(CreateStudentDto dto)
    {
        var student = new Student
        {
            Name = dto.Name,
            Email = dto.Email,
            Age = dto.Age
        };

        var created = await _repository.AddAsync(student);

        return new StudentDto
        {
            Id = created.Id,
            Name = created.Name,
            Email = created.Email,
            Age = created.Age
        };
    }

    public async Task<bool> UpdateAsync(int id, UpdateStudentDto dto)
    {
        var student = await _repository.GetByIdAsync(id);
        if (student == null) return false;

        student.Name = dto.Name;
        student.Email = dto.Email;
        student.Age = dto.Age;

        await _repository.UpdateAsync(student);
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var student = await _repository.GetByIdAsync(id);
        if (student == null) return false;

        await _repository.DeleteAsync(id);
        return true;
    }
    public async Task<StudentDto?> GetByNameAsync(string name)
    {
        var student = await _repository.GetByNameAsync(name);
        if (student == null) return null;

        return new StudentDto
        {
            Id = student.Id,
            Name = student.Name,
            Email = student.Email,
            Age = student.Age,
            EnrolledCourses = student.StudentCourses?
                .Where(sc => sc.Course != null && !string.IsNullOrWhiteSpace(sc.Course.Name))
                .Select(sc => sc.Course.Name)
                .Distinct()
                .ToList() ?? new List<string>()
        };
    }
}