using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudentCourseManagement.Infrastructure.AuthEntities;
using StudentCourseManagement.Infrastructure.Data;
using StudentCourseManagement.Infrastructure.Entities;

namespace StudentCourseManagement.Tests.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration (SQL Server)
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Register In-Memory Database
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase("IntegrationTestDb");
            });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();

            SeedTestData(db);
        });
    }

    private static void SeedTestData(ApplicationDbContext db)
    {
        var hasher = new PasswordHasher<UsersDatum>();

        // 1. Seed test Admin user into UsersData using ASP.NET Core Identity PasswordHasher
        if (!db.UsersData.Any(u => u.Username == "testadmin"))
        {
            var adminUser = new UsersDatum
            {
                Username = "testadmin",
                FullName = "Test Admin",
                Email = "admin@university.edu",
                Role = "Admin"
            };
            adminUser.PasswordHash = hasher.HashPassword(adminUser, "AdminPass123!");
            db.UsersData.Add(adminUser);
        }

        // 2. Seed test Student user into UsersData & Students table
        if (!db.UsersData.Any(u => u.Username == "teststudent"))
        {
            var studentUser = new UsersDatum
            {
                Username = "teststudent",
                FullName = "Test Student",
                Email = "student@university.edu",
                Role = "Student"
            };
            studentUser.PasswordHash = hasher.HashPassword(studentUser, "StudentPass123!");
            db.UsersData.Add(studentUser);

            db.Students.Add(new Student
            {
                Name = "teststudent",
                Email = "student@university.edu"
            });
        }

        db.SaveChanges();
    }
}