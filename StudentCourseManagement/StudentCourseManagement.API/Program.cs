using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using StudentCourseManagement.API.Middleware;
using StudentCourseManagement.Application.Interfaces;
using StudentCourseManagement.Application.Services;
using StudentCourseManagement.Application.Validators;
using StudentCourseManagement.Infrastructure.Data;
using StudentCourseManagement.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// --- CONTROLLERS & FLUENT VALIDATION ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<LoginDtoValidator>();

// --- SWAGGER / OPENAPI CONFIGURATION ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "StudentCourseManagement.API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter your JWT token (without 'Bearer ' prefix):",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// --- DATABASE CONTEXT ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- REPOSITORIES DEPENDENCY INJECTION ---
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// --- APPLICATION SERVICES DEPENDENCY INJECTION ---
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAiCourseService, AiCourseService>();

builder.Services.AddHttpClient("OpenRouterClient", client =>
{
    client.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost:4200");
    client.DefaultRequestHeaders.Add("X-Title", "Student Course Management");
});

var openRouterKey = builder.Configuration["OpenRouter:ApiKey"]
    ?? throw new InvalidOperationException(
        "OpenRouter API Key 'OpenRouter:ApiKey' is not configured. " +
        "Run 'dotnet user-secrets set OpenRouter:ApiKey <key>' in StudentCourseManagement.API.");

string modelId = "qwen/qwen-2.5-72b-instruct:free";

builder.Services.AddSingleton<Kernel>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("OpenRouterClient");

    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: modelId,
        apiKey: openRouterKey,
        endpoint: new Uri("https://openrouter.ai/api/v1", UriKind.Absolute),
        httpClient: httpClient
    );

    return kernelBuilder.Build();
});

// --- JWT AUTHENTICATION CONFIGURATION ---
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "JWT Secret Key 'Jwt:Key' is not configured. " +
        "Please define it in appsettings.json or as an environment variable.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// --- CORS CONFIGURATION ---
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAngularDev", p =>
        p.WithOrigins("http://localhost:4200")
         .AllowAnyMethod()
         .AllowAnyHeader()));

// --- RATE LIMITING ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("AiSearchLimit", httpContext =>
    {
        var username = httpContext.User.Identity?.Name
                       ?? httpContext.Connection.RemoteIpAddress?.ToString()
                       ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(username, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    options.AddPolicy("AiAdminLimit", httpContext =>
    {
        var username = httpContext.User.Identity?.Name
                       ?? httpContext.Connection.RemoteIpAddress?.ToString()
                       ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter($"admin_{username}", _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 2,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });
});

var app = builder.Build();

// --- MIDDLEWARE PIPELINE ---
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngularDev");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

app.Run();