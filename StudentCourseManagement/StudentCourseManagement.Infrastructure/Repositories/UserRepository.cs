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
        if (string.IsNullOrWhiteSpace(username)) return null;
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
        bool usernameTaken = await _context.UsersData.AnyAsync(u => u.Username == user.Username);
        if (usernameTaken)
            throw new InvalidOperationException($"Username '{user.Username}' is already taken.");

        bool emailTaken = !string.IsNullOrWhiteSpace(user.Email)
            && await _context.UsersData.AnyAsync(u => u.Email == user.Email);
        if (emailTaken)
            throw new InvalidOperationException($"Email '{user.Email}' is already registered.");

        var entity = new UsersDatum
        {
            FullName = user.FullName ?? string.Empty,
            Email = user.Email ?? string.Empty,
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
            Role = parsedRole,
            RefreshToken = entity.RefreshToken,
            RefreshTokenExpiryTime = entity.RefreshTokenExpiryTime
        };
    }
}