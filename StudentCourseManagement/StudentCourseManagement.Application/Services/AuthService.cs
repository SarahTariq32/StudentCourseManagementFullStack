//using System;
//using System.Collections.Generic;
//using System.IdentityModel.Tokens.Jwt;
//using System.Linq;
//using System.Security.Claims;
//using System.Security.Cryptography;
//using System.Text;
//using System.Threading.Tasks;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.Extensions.Configuration;
//using Microsoft.IdentityModel.Tokens;
//using StudentCourseManagement.Application.DTOs;
//using StudentCourseManagement.Application.Interfaces;
//using StudentCourseManagement.Domain.Entities;
//using StudentCourseManagement.Domain.Enums;

//namespace StudentCourseManagement.Application.Services;

//public class AuthService : IAuthService
//{
//    private readonly IUserRepository _userRepository;
//    private readonly IConfiguration _configuration;
//    private readonly PasswordHasher<User> _passwordHasher;

//    public AuthService(
//        IUserRepository userRepository,
//        IConfiguration configuration)
//    {
//        _userRepository = userRepository;
//        _configuration = configuration;
//        _passwordHasher = new PasswordHasher<User>();
//    }

//    public async Task<bool> RegisterAsync(RegisterDto dto)
//    {
//        // Check for existing username or email
//        if (await _context.UsersData.AnyAsync(u => u.Username == dto.Username))
//        {
//            throw new Exception("Username already exists.");
//        }

//        if (await _context.UsersData.AnyAsync(u => u.Email == dto.Email))
//        {
//            throw new Exception("Email already registered.");
//        }

//        string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

//        var newUser = new User
//        {
//            FullName = dto.FullName,
//            Email = dto.Email,
//            Username = dto.Username,
//            PasswordHash = passwordHash,
//            Role = dto.Role
//        };

//        _context.UsersData.Add(newUser);
//        await _context.SaveChangesAsync();

//        return true;
//    }

//    public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
//    {
//        var user = await _userRepository
//            .GetByUsernameAsync(dto.Username);

//        if (user == null)
//            return null;

//        var result = _passwordHasher.VerifyHashedPassword(
//            user,
//            user.PasswordHash,
//            dto.Password);

//        if (result == PasswordVerificationResult.Failed)
//            return null;

//        var claims = new[]
//        {
//            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
//            new Claim(ClaimTypes.Name, user.Username),
//            new Claim(ClaimTypes.Role, user.Role.ToString()) 
//        };

//        var jwtKey = _configuration["Jwt:Key"]
//            ?? throw new InvalidOperationException("JWT Secret Key 'Jwt:Key' is not configured. Please define it in appsettings.json or as an environment variable.");

//        var key = new SymmetricSecurityKey(
//            Encoding.UTF8.GetBytes(jwtKey));

//        var credentials = new SigningCredentials(
//            key,
//            SecurityAlgorithms.HmacSha256);

//        var token = new JwtSecurityToken(
//            claims: claims,
//            expires: DateTime.UtcNow.AddHours(2),
//            signingCredentials: credentials);

//        var refreshToken = GenerateSecureRefreshToken();
//        user.RefreshToken = refreshToken;
//        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

//        await _userRepository.UpdateAsync(user);

//        return new AuthResponseDto
//        {
//            Token = new JwtSecurityTokenHandler().WriteToken(token),
//            RefreshToken = user.RefreshToken,
//            RefreshTokenExpiryTime = user.RefreshTokenExpiryTime.Value
//        };
//    }

//    public async Task<AuthResponseDto?> RefreshTokenAsync(RefreshTokenDto dto)
//    {
//        var user = await _userRepository
//            .GetByRefreshTokenAsync(dto.RefreshToken);

//        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
//            return null;

//        var claims = new[]
//        {
//            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
//            new Claim(ClaimTypes.Name, user.Username),
//            new Claim(ClaimTypes.Role, user.Role.ToString()) 
//        };

//        var jwtKey = _configuration["Jwt:Key"]
//            ?? throw new InvalidOperationException("JWT Secret Key 'Jwt:Key' is not configured. Please define it in appsettings.json or as an environment variable.");

//        var key = new SymmetricSecurityKey(
//            Encoding.UTF8.GetBytes(jwtKey));

//        var credentials = new SigningCredentials(
//            key,
//            SecurityAlgorithms.HmacSha256);

//        var token = new JwtSecurityToken(
//            claims: claims,
//            expires: DateTime.UtcNow.AddMinutes(5),
//            signingCredentials: credentials);

//        var newRefreshToken = GenerateSecureRefreshToken();
//        user.RefreshToken = newRefreshToken;
//        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

//        await _userRepository.UpdateAsync(user);

//        return new AuthResponseDto
//        {
//            Token = new JwtSecurityTokenHandler().WriteToken(token),
//            RefreshToken = user.RefreshToken,
//            RefreshTokenExpiryTime = user.RefreshTokenExpiryTime.Value
//        };
//    }

//    private static string GenerateSecureRefreshToken()
//    {
//        var randomNumber = new byte[64];
//        using var rng = RandomNumberGenerator.Create();
//        rng.GetBytes(randomNumber);
//        return Convert.ToBase64String(randomNumber);
//    }
//}

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Interfaces;
using StudentCourseManagement.Domain.Entities;
using StudentCourseManagement.Domain.Enums;

namespace StudentCourseManagement.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher<User> _passwordHasher;

    public AuthService(
        IUserRepository userRepository,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _configuration = configuration;
        _passwordHasher = new PasswordHasher<User>();
    }

    public async Task<bool> RegisterAsync(RegisterDto dto)
    {
        // 1. Check duplicate username
        var existingByUsername = await _userRepository.GetByUsernameAsync(dto.Username);
        if (existingByUsername != null)
            return false;

        // 2. Check duplicate email
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var existingByEmail = await _userRepository.GetByUsernameAsync(dto.Email);
            if (existingByEmail != null)
                return false;
        }

        // 3. Parse role safely
        if (!Enum.TryParse<UserRole>(dto.Role, true, out var parsedRole))
        {
            parsedRole = UserRole.Student;
        }

        // 4. Create domain model instance
        var newUser = new User
        {
            FullName = !string.IsNullOrWhiteSpace(dto.FullName) ? dto.FullName : dto.Username,
            Email = dto.Email ?? string.Empty,
            Username = dto.Username,
            Role = parsedRole
        };

        // 5. Hash password with the instantiated newUser model
        newUser.PasswordHash = _passwordHasher.HashPassword(newUser, dto.Password);

        // 6. Save entity via repository
        try
        {
            await _userRepository.AddAsync(newUser);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return true;
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
    {
        var user = await _userRepository.GetByUsernameAsync(dto.Username);

        if (user == null)
            return null;

        var result = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            dto.Password);

        if (result == PasswordVerificationResult.Failed)
            return null;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT Secret Key 'Jwt:Key' is not configured. Please define it in appsettings.json or as an environment variable.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials);

        var refreshToken = GenerateSecureRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await _userRepository.UpdateAsync(user);

        return new AuthResponseDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken = user.RefreshToken,
            RefreshTokenExpiryTime = user.RefreshTokenExpiryTime.Value
        };
    }

    public async Task<AuthResponseDto?> RefreshTokenAsync(RefreshTokenDto dto)
    {
        var user = await _userRepository.GetByRefreshTokenAsync(dto.RefreshToken);

        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            return null;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT Secret Key 'Jwt:Key' is not configured. Please define it in appsettings.json or as an environment variable.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        var newRefreshToken = GenerateSecureRefreshToken();
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await _userRepository.UpdateAsync(user);

        return new AuthResponseDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken = user.RefreshToken,
            RefreshTokenExpiryTime = user.RefreshTokenExpiryTime.Value
        };
    }

    private static string GenerateSecureRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}