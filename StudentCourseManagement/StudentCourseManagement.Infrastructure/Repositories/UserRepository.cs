using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StudentCourseManagement.Application.Interfaces;
using StudentCourseManagement.Domain.Entities;
using StudentCourseManagement.Domain.Enums;
using StudentCourseManagement.Infrastructure.AuthEntities;
using StudentCourseManagement.Infrastructure.Data;

namespace StudentCourseManagement.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        var user = await _context.UsersData
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user == null)
            return null;

        return MapToDomain(user);
    }

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken)
    {
        var user = await _context.UsersData
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);

        if (user == null)
            return null;

        return MapToDomain(user);
    }

    public async Task AddAsync(User user)
    {
        var entity = new UsersDatum
        {
            FullName = user.FullName,
            Email = user.Email,
            Username = user.Username,
            PasswordHash = user.PasswordHash,
            Role = user.Role.ToString()
        };

        await _context.UsersData.AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        var entity = await _context.UsersData
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        if (entity == null)
            return;

        entity.FullName = user.FullName;
        entity.Email = user.Email;
        entity.RefreshToken = user.RefreshToken;
        entity.RefreshTokenExpiryTime = user.RefreshTokenExpiryTime;

        await _context.SaveChangesAsync();
    }

    private static User MapToDomain(UsersDatum entity)
    {
        if (!Enum.TryParse<UserRole>(entity.Role, true, out var parsedRole))
        {
            parsedRole = UserRole.Student;
        }

        return new User
        {
            Id = entity.Id,
            FullName = entity.FullName,
            Email = entity.Email,
            Username = entity.Username,
            PasswordHash = entity.PasswordHash,
            Role = parsedRole, // <--- LINE 91: Assign parsedRole directly as UserRole enum
            RefreshToken = entity.RefreshToken,
            RefreshTokenExpiryTime = entity.RefreshTokenExpiryTime
        };
    }
}