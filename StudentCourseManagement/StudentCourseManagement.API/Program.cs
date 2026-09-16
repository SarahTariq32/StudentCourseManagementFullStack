using Microsoft.EntityFrameworkCore;
using StudentCourseManagement.Infrastructure.Data;
using StudentCourseManagement.Application.Interfaces;
using StudentCourseManagement.Application.Services;
using StudentCourseManagement.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using FluentValidation;
using FluentValidation.AspNetCore;
using StudentCourseManagement.Application.Validators;
using StudentCourseManagement.API.Middleware;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

// --- CONTROLLERS & VALIDATION ---
builder.Services.AddControllers();
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
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// --- REPOSITORIES DEPENDENCY INJECTION ---
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// --- APPLICATION SERVICES DEPENDENCY INJECTION ---
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAiCourseService, AiCourseService>();

// --- SEMANTIC KERNEL & OPENROUTER AI CONFIGURATION ---
var openRouterKey = builder.Configuration["OpenRouter:ApiKey"]
    ?? throw new InvalidOperationException("OpenRouter API Key 'OpenRouter:ApiKey' is not configured. Run 'dotnet user-secrets set OpenRouter:ApiKey <key>' in StudentCourseManagement.API.");

// OpenRouter free router — automatically selects from all currently available free models.
// Using this instead of a specific free slug (e.g. qwen/qwen-2.5-72b-instruct:free) because
// free model slugs on OpenRouter are retired without notice and cause 404 errors.
string modelId = "openrouter/free";

// Configure HttpClient with OpenRouter required headers
var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost:4200");
httpClient.DefaultRequestHeaders.Add("X-Title", "Student Course Management");

// Registered as Singleton because the Kernel is stateless between requests.
// Chat history lives in the prompt string passed per request, not in the Kernel itself.
// Scoped would rebuild the entire Kernel (and re-register the connector) on every HTTP request.
builder.Services.AddSingleton<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    // endpoint must be /api/v1 — the OpenAI SDK appends /chat/completions to this base,
    // producing https://openrouter.ai/api/v1/chat/completions (the correct OpenRouter route).
    // Using /api (without /v1) produced a 404 because the assembled path was wrong.
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
    ?? throw new InvalidOperationException("JWT Secret Key 'Jwt:Key' is not configured. Please define it in appsettings.json or as an environment variable.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)),
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

app.MapControllers();

app.Run();