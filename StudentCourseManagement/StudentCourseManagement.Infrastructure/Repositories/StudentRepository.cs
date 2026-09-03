using Microsoft.EntityFrameworkCore;
using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Interfaces;
using StudentCourseManagement.Domain.Entities;
using StudentCourseManagement.Infrastructure.Data;
using StudentCourseManagement.Infrastructure.Mappings;

using InfrastructureStudent = StudentCourseManagement.Infrastructure.Entities.Student;

namespace StudentCourseManagement.Infrastructure.Repositories;

public class StudentRepository : IStudentRepository
{
    private readonly ApplicationDbContext _context;

    public StudentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Student>> GetAllAsync()
    {
        try
        {
            var students = await _context.Students
                .AsNoTracking()
                .Include(s => s.StudentCourses)
                    .ThenInclude(sc => sc.Course)
                .ToListAsync();

            return students.Select(s => s.ToDomain()).ToList();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving students: {ex.Message}", ex);
        }
    }

    public async Task<Student?> GetByIdAsync(int id)
    {
        try
        {
            var student = await _context.Students
                .AsNoTracking()
                .Include(s => s.StudentCourses)
                    .ThenInclude(sc => sc.Course)
                .FirstOrDefaultAsync(s => s.Id == id);

            return student?.ToDomain();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving student with ID {id}: {ex.Message}", ex);
        }
    }

    public async Task<Student> AddAsync(Student student)
    {
        try
        {
            InfrastructureStudent entity = student.ToInfrastructure();

            _context.Students.Add(entity);
            await _context.SaveChangesAsync();

            return entity.ToDomain();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error adding student: {ex.Message}", ex);
        }
    }

    public async Task UpdateAsync(Student student)
    {
        try
        {
            var entity = await _context.Students
                .FirstOrDefaultAsync(s => s.Id == student.Id);

            if (entity == null)
                return;

            entity.Name = student.Name;
            entity.Email = student.Email;
            entity.Age = student.Age;

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error updating student with ID {student.Id}: {ex.Message}", ex);
        }
    }

    public async Task DeleteAsync(int id)
    {
        try
        {
            var entity = await _context.Students
                .FirstOrDefaultAsync(s => s.Id == id);

            if (entity == null)
                return;

            _context.Students.Remove(entity);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error deleting student with ID {id}: {ex.Message}", ex);
        }
    }

    public async Task<PagedResultDto<Student>> GetPagedAsync(StudentQueryParameters queryParams)
    {

        var query = _context.Students
            .AsNoTracking()
            .Include(s => s.StudentCourses)
                .ThenInclude(sc => sc.Course)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryParams.SearchTerm))
        {
            var term = queryParams.SearchTerm.Trim().ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(term) || s.Email.ToLower().Contains(term));
        }

        if (queryParams.MinAge.HasValue)
            query = query.Where(s => s.Age >= queryParams.MinAge.Value);

        if (queryParams.MaxAge.HasValue)
            query = query.Where(s => s.Age <= queryParams.MaxAge.Value);

        query = queryParams.SortBy.ToLower() switch
        {
            "email" => queryParams.IsDescending ? query.OrderByDescending(s => s.Email) : query.OrderBy(s => s.Email),
            "age" => queryParams.IsDescending ? query.OrderByDescending(s => s.Age) : query.OrderBy(s => s.Age),
            "id" => queryParams.IsDescending ? query.OrderByDescending(s => s.Id) : query.OrderBy(s => s.Id),
            _ => queryParams.IsDescending ? query.OrderByDescending(s => s.Name) : query.OrderBy(s => s.Name)
        };

        int totalCount = await query.CountAsync();

        var items = await query
            .Skip((queryParams.PageIndex - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync();

        return new PagedResultDto<Student>
        {
            Items = items.Select(s => s.ToDomain()).ToList(),
            PageIndex = queryParams.PageIndex,
            PageSize = queryParams.PageSize,
            TotalCount = totalCount
        };
    }
    public async Task<StudentCourseManagement.Domain.Entities.Student?> GetByNameAsync(string name)
    {
        var trimmedName = name.Trim();
        var entity = await _context.Students
            .Include(s => s.StudentCourses)
                .ThenInclude(sc => sc.Course)
            .FirstOrDefaultAsync(s => s.Name.ToLower() == trimmedName.ToLower());

        if (entity == null) return null;

        // Maps Infrastructure entity to Domain entity
        return entity.ToDomain();
    }
}