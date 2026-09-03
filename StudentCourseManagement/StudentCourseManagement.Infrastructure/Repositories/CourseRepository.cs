using Microsoft.EntityFrameworkCore;
using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Interfaces;
using StudentCourseManagement.Domain.Entities;
using StudentCourseManagement.Infrastructure.Data;
using StudentCourseManagement.Infrastructure.Mappings;

using InfrastructureCourse = StudentCourseManagement.Infrastructure.Entities.Course;

namespace StudentCourseManagement.Infrastructure.Repositories;

public class CourseRepository : ICourseRepository
{
    private readonly ApplicationDbContext _context;

    public CourseRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Course>> GetAllAsync()
    {
        try
        {
            var courses = await _context.Courses.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
            return courses.Select(c => c.ToDomain()).ToList();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving all courses: {ex.Message}", ex);
        }
    }

    public async Task<Course?> GetByIdAsync(int id)
    {
        try
        {
            var course = await _context.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            return course?.ToDomain();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error retrieving course with ID {id}: {ex.Message}", ex);
        }
    }

    public async Task<Course> AddAsync(Course course)
    {
        try
        {
            InfrastructureCourse entity = course.ToInfrastructure();

            _context.Courses.Add(entity);
            await _context.SaveChangesAsync();

            return entity.ToDomain();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error adding course: {ex.Message}", ex);
        }
    }

    public async Task UpdateAsync(Course course)
    {
        try
        {
            var entity = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == course.Id);

            if (entity == null)
                return;

            entity.Name = course.Name;
            entity.Credits = course.Credits;

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error updating course with ID {course.Id}: {ex.Message}", ex);
        }
    }

    public async Task DeleteAsync(int id)
    {
        try
        {
            var entity = await _context.Courses
                .FirstOrDefaultAsync(c => c.Id == id);

            if (entity == null)
                return;

            _context.Courses.Remove(entity);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error deleting course with ID {id}: {ex.Message}", ex);
        }
    }

    public async Task<int> GetEnrolledStudentCountAsync(int courseId)
    {
        return await _context.StudentCourses.CountAsync(sc => sc.CourseId == courseId);
    }

    public async Task<bool> IsStudentEnrolledAsync(int studentId, int courseId)
    {
        return await _context.StudentCourses.AnyAsync(sc => sc.StudentId == studentId && sc.CourseId == courseId);
    }

    public async Task EnrollStudentAsync(int studentId, int courseId)
    {
        var enrollment = new StudentCourseManagement.Infrastructure.Entities.StudentCourse
        {
            StudentId = studentId,
            CourseId = courseId,
            EnrolledOn = DateTime.UtcNow
        };

        await _context.StudentCourses.AddAsync(enrollment);

        var pendingRequests = await _context.EnrollmentRequests
            .Where(r => r.StudentId == studentId && r.CourseId == courseId && r.Status == "Pending")
            .ToListAsync();

        if (pendingRequests.Any())
        {
            _context.EnrollmentRequests.RemoveRange(pendingRequests);
        }

        await _context.SaveChangesAsync();
    }

    public async Task UnenrollStudentAsync(int studentId, int courseId)
    {
        var enrollment = await _context.StudentCourses
            .FirstOrDefaultAsync(sc => sc.StudentId == studentId && sc.CourseId == courseId);

        if (enrollment != null)
        {
            _context.StudentCourses.Remove(enrollment);
        }

        var pendingRequests = await _context.EnrollmentRequests
            .Where(r => r.StudentId == studentId && r.CourseId == courseId && r.Status == "Pending")
            .ToListAsync();

        if (pendingRequests.Any())
        {
            _context.EnrollmentRequests.RemoveRange(pendingRequests);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> GetStudentEnrolledCoursesCountAsync(int studentId)
    {
        return await _context.StudentCourses.CountAsync(sc => sc.StudentId == studentId);
    }

    public async Task<(bool Success, string Message)> CreateEnrollmentRequestAsync(int studentId, int courseId, string requestType, string? reason)
    {
        if (courseId > 0)
        {
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
            if (course == null)
                return (false, "Course does not exist.");

            bool isEnrolled = await _context.StudentCourses.AnyAsync(sc => sc.StudentId == studentId && sc.CourseId == courseId);

            if (requestType == "Enroll" && isEnrolled)
                return (false, "You are already enrolled in this course.");
        }

        var existingPending = await _context.EnrollmentRequests
            .AnyAsync(r => r.StudentId == studentId && r.CourseId == courseId && r.RequestType == requestType && r.Status == "Pending");

        if (existingPending)
            return (false, $"You already have a pending {requestType.ToLower()} request for this course.");

        var request = new StudentCourseManagement.Infrastructure.Entities.EnrollmentRequest
        {
            StudentId = studentId,
            CourseId = courseId,
            RequestType = requestType,
            Reason = reason ?? $"Requesting {requestType.ToLower()}.",
            Status = "Pending",
            RequestedOn = DateTime.UtcNow
        };

        await _context.EnrollmentRequests.AddAsync(request);
        await _context.SaveChangesAsync();

        return (true, $"{requestType} request submitted successfully to the Admin.");
    }

    public async Task<List<EnrollmentRequestResponseDto>> GetPendingEnrollmentRequestsAsync()
    {
        var requests = await _context.EnrollmentRequests
            .AsNoTracking()
            .Include(r => r.Student)
            .Include(r => r.Course)
            .Where(r => r.Status == "Pending")
            .ToListAsync();

        var result = new List<EnrollmentRequestResponseDto>();

        foreach (var r in requests)
        {
            string studentName;
            if (r.Student != null)
            {
                studentName = r.Student.Name;
            }
            else
            {
                var user = await _context.UsersData.FirstOrDefaultAsync(u => u.Id == r.StudentId);
                studentName = user != null && !string.IsNullOrWhiteSpace(user.FullName)
                    ? user.FullName
                    : (user?.Username ?? "Unknown Student");
            }

            result.Add(new EnrollmentRequestResponseDto
            {
                RequestId = r.Id,
                StudentId = r.StudentId,
                StudentName = studentName,
                CourseId = r.CourseId,
                CourseName = r.Course != null ? r.Course.Name : "N/A (Account Verification)",
                RequestType = (r.CourseId <= 0 || (r.Reason != null && r.Reason.Contains("ACCOUNT_CREATION_REQUEST")))
                    ? "Registration Request"
                    : (r.RequestType == "Unenroll" ? "Unenrollment Request" : "Enrollment Request"),
                Reason = r.Reason ?? string.Empty,
                Status = r.Status,
                RequestedOn = r.RequestedOn
            });
        }

        return result;
    }

    public async Task<(bool Success, string Message)> ProcessEnrollmentRequestAsync(int requestId, bool approve)
    {
        var request = await _context.EnrollmentRequests.FirstOrDefaultAsync(r => r.Id == requestId);
        if (request == null)
            return (false, $"Request ticket #{requestId} was not found or has already been processed.");

        if (!approve)
        {
            _context.EnrollmentRequests.Remove(request);
            await _context.SaveChangesAsync();
            return (true, $"Request #{requestId} was rejected and removed from the queue.");
        }

        if (request.CourseId <= 0 || (request.Reason != null && request.Reason.Contains("ACCOUNT_CREATION_REQUEST")))
        {
            var existingStudent = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId);
            if (existingStudent == null)
            {
                var user = await _context.UsersData.FirstOrDefaultAsync(u => u.Id == request.StudentId);
                string studentName = user != null && !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : (user?.Username ?? "Student #" + request.StudentId);
                string studentEmail = user?.Email ?? string.Empty;

                existingStudent = await _context.Students.FirstOrDefaultAsync(s => s.Name.ToLower() == studentName.ToLower() || (s.Email != "" && s.Email.ToLower() == studentEmail.ToLower()));

                if (existingStudent == null)
                {
                    var newStudent = new StudentCourseManagement.Infrastructure.Entities.Student
                    {
                        Name = studentName,
                        Email = studentEmail,
                        Age = 20
                    };
                    await _context.Students.AddAsync(newStudent);
                }
            }

            _context.EnrollmentRequests.Remove(request);
            await _context.SaveChangesAsync();
            return (true, $"Account registration request #{requestId} approved successfully.");
        }

        if (request.RequestType == "Enroll")
        {
            bool isEnrolled = await _context.StudentCourses.AnyAsync(sc => sc.StudentId == request.StudentId && sc.CourseId == request.CourseId);
            if (isEnrolled)
            {
                _context.EnrollmentRequests.Remove(request);
                await _context.SaveChangesAsync();
                return (true, "Student was already enrolled. Request ticket cleared from queue.");
            }

            int studentCourseCount = await _context.StudentCourses.CountAsync(sc => sc.StudentId == request.StudentId);
            if (studentCourseCount >= 7)
                return (false, "Cannot approve: Student has already reached the maximum limit of 7 enrolled courses.");

            int courseStudentCount = await _context.StudentCourses.CountAsync(sc => sc.CourseId == request.CourseId);
            if (courseStudentCount >= 50)
                return (false, "Cannot approve: Course capacity has reached the maximum of 50 students.");

            var enrollment = new StudentCourseManagement.Infrastructure.Entities.StudentCourse
            {
                StudentId = request.StudentId,
                CourseId = request.CourseId,
                EnrolledOn = DateTime.UtcNow
            };
            await _context.StudentCourses.AddAsync(enrollment);
        }
        else if (request.RequestType == "Unenroll")
        {
            var enrollment = await _context.StudentCourses
                .FirstOrDefaultAsync(sc => sc.StudentId == request.StudentId && sc.CourseId == request.CourseId);

            if (enrollment != null)
            {
                _context.StudentCourses.Remove(enrollment);
            }
        }

        _context.EnrollmentRequests.Remove(request);
        await _context.SaveChangesAsync();

        return (true, $"Request #{requestId} approved! Student successfully {request.RequestType.ToLower()}ed.");
    }

    public async Task<PagedResultDto<Course>> GetPagedAsync(CourseQueryParameters queryParams)
    {
        var query = _context.Courses
            .AsNoTracking()
            .Include(c => c.StudentCourses)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryParams.SearchTerm))
        {
            var term = queryParams.SearchTerm.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term));
        }

        if (queryParams.MinCredits.HasValue)
        {
            query = query.Where(c => c.Credits >= queryParams.MinCredits.Value);
        }

        if (queryParams.MaxCredits.HasValue)
        {
            query = query.Where(c => c.Credits <= queryParams.MaxCredits.Value);
        }

        query = queryParams.SortBy.ToLower() switch
        {
            "credits" => queryParams.IsDescending ? query.OrderByDescending(c => c.Credits) : query.OrderBy(c => c.Credits),
            "name" => queryParams.IsDescending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
            _ => queryParams.IsDescending ? query.OrderByDescending(c => c.Id) : query.OrderBy(c => c.Id)
        };

        int totalCount = await query.CountAsync();

        var items = await query
            .Skip((queryParams.PageIndex - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync();

        return new PagedResultDto<Course>
        {
            Items = items.Select(c => c.ToDomain()).ToList(),
            PageIndex = queryParams.PageIndex,
            PageSize = queryParams.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<List<Course>> GetAvailableCoursesForStudentsAsync(int? studentId = null)
    {
        var query = _context.Courses
            .AsNoTracking()
            .Include(c => c.StudentCourses)
            .AsQueryable();

        if (studentId.HasValue && studentId.Value > 0)
        {
            query = query.Where(c => !c.StudentCourses.Any(sc => sc.StudentId == studentId.Value));
        }

        query = query.Where(c => c.StudentCourses.Count < 50);

        var courses = await query.ToListAsync();
        return courses.Select(c => c.ToDomain()).ToList();
    }
}