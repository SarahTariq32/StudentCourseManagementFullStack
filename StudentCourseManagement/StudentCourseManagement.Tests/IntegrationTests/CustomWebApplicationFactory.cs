using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using StudentCourseManagement.Application.Interfaces;
using StudentCourseManagement.Infrastructure.AuthEntities;
using StudentCourseManagement.Infrastructure.Data;
using StudentCourseManagement.Infrastructure.Entities;
using StudentCourseManagement.Tests.TestDoubles;

using Serilog;
using Serilog.Events;

namespace StudentCourseManagement.Tests.IntegrationTests;

/// <summary>
/// Boots the full API with offline test doubles so integration tests never touch the network:
/// the Semantic Kernel is backed by a scripted <see cref="FakeChatCompletionService"/> and the
/// embedding service is the deterministic <see cref="FakeEmbeddingService"/> (FNV-1a bag-of-words).
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public FakeChatCompletionService FakeChat { get; } = new();
    public TestLogEventSink LogSink { get; } = new();

    private readonly string _databaseName = $"IntegrationTestDb_{Guid.NewGuid():N}";

    public IReadOnlyList<LogEvent> GetLogsByCorrelationId(string correlationId) =>
        LogSink.GetLogsByCorrelationId(correlationId);

    public IReadOnlyList<string> GetFormattedLogs(string correlationId) =>
        LogSink.GetFormattedLogs(correlationId);

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseSerilog((context, services, configuration) =>
        {
            configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Sink(LogSink)
                .WriteTo.Console();
        });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The app throws on startup without an OpenRouter key; tests never call the real provider.
        builder.UseSetting("OpenRouter:ApiKey", "integration-test-key");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.ConfigureWarnings(w =>
                    w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            });

            // Swap the OpenRouter-backed Kernel for one backed by the scripted fake chat model.
            services.RemoveAll<Kernel>();
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton<IChatCompletionService>(FakeChat);
            services.AddSingleton(kernelBuilder.Build());

            // Swap the Ollama embedding client for the deterministic offline fake.
            services.RemoveAll<IEmbeddingService>();
            services.AddSingleton<IEmbeddingService, FakeEmbeddingService>();

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
