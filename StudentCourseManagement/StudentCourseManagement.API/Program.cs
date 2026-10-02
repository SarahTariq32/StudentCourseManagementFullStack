using System.IO;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using StudentCourseManagement.API.Hubs;
using StudentCourseManagement.API.Middleware;
using StudentCourseManagement.Application.Interfaces;
using StudentCourseManagement.Application.Services;
using StudentCourseManagement.Application.Validators;
using StudentCourseManagement.Infrastructure.Data;
using StudentCourseManagement.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// --- SERILOG STRUCTURED LOGGING CONFIGURATION ---
builder.Host.UseSerilog((context, services, configuration) =>
{
    var logsDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
    if (!Directory.Exists(logsDirectory))
    {
        Directory.CreateDirectory(logsDirectory);
    }

    configuration
        .ReadFrom.Configuration(context.Configuration)
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{CorrelationId}] [{UserId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            formatter: new CompactJsonFormatter(),
            path: Path.Combine(logsDirectory, "app-.json"),
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true,
            retainedFileCountLimit: null,
            retainedFileTimeLimit: TimeSpan.FromDays(3));
});

// --- CONTROLLERS, FLUENT VALIDATION & SIGNALR ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();

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
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// --- DOCUMENT INGESTION (RAG) ---
builder.Services.AddSingleton<DocumentChunker>();
builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

// --- APPLICATION SERVICES DEPENDENCY INJECTION ---
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IAiCourseService, AiCourseService>();
builder.Services.AddScoped<IEmailDigestSender, SmtpEmailDigestSender>();
builder.Services.AddScoped<IAdminNotificationService, AdminNotificationService>();

builder.Services.AddHttpClient("OpenRouterClient", client =>
{
    client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
    client.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost:4200");
    client.DefaultRequestHeaders.Add("X-Title", "Student Course Management");
});

var openRouterKey = builder.Configuration["OpenRouter:ApiKey"]
    ?? throw new InvalidOperationException(
        "OpenRouter API Key 'OpenRouter:ApiKey' is not configured. " +
        "Run 'dotnet user-secrets set OpenRouter:ApiKey <key>' in StudentCourseManagement.API.");

string modelId = builder.Configuration["OpenRouter:ModelId"] ?? "openrouter/free";

builder.Services.AddScoped<IVectorStore, VectorStore>();

builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:11434/");
    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddSingleton(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("OpenRouterClient");

    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.AddOpenAIChatCompletion(
        modelId: modelId,
        apiKey: openRouterKey,
        endpoint: new Uri("https://openrouter.ai/api/v1"),
        httpClient: httpClient
    );

    return kernelBuilder.Build();
});

// --- JWT AUTHENTICATION CONFIGURATION WITH SIGNALR WEBSOCKET SUPPORT ---
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

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// --- CORS CONFIGURATION (ALLOW CREDENTIALS FOR WEBSOCKETS) ---
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAngularDev", p =>
        p.WithOrigins("http://localhost:4200")
         .AllowAnyMethod()
         .AllowAnyHeader()
         .AllowCredentials()));

// --- RATE LIMITING ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("AiSearchLimit", httpContext =>
    {
        var username = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                       ?? httpContext.User.FindFirst("unique_name")?.Value
                       ?? httpContext.User.FindFirst("name")?.Value
                       ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                       ?? httpContext.User.FindFirst("sub")?.Value
                       ?? httpContext.User.Identity?.Name
                       ?? httpContext.Connection.RemoteIpAddress?.ToString()
                       ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(username, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });

    options.AddPolicy("AiAdminLimit", httpContext =>
    {
        var username = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                       ?? httpContext.User.FindFirst("unique_name")?.Value
                       ?? httpContext.User.FindFirst("name")?.Value
                       ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                       ?? httpContext.User.FindFirst("sub")?.Value
                       ?? httpContext.User.Identity?.Name
                       ?? httpContext.Connection.RemoteIpAddress?.ToString()
                       ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter($"admin_{username}", _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });
});

builder.Services.AddHostedService<AiSummaryDigestBackgroundService>();

var app = builder.Build();

// --- MIDDLEWARE PIPELINE ---
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        var correlationId = httpContext.Items["CorrelationId"]?.ToString() 
            ?? httpContext.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName].ToString();
        diagnosticContext.Set("CorrelationId", correlationId ?? "unknown");
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            var internalId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                ?? httpContext.User.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(internalId))
            {
                diagnosticContext.Set("AuthenticatedUser", internalId);
            }
        }
    };
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngularDev");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                 ?? context.User?.FindFirst("sub")?.Value;

    if (!string.IsNullOrEmpty(userId))
    {
        using (Serilog.Context.LogContext.PushProperty("UserId", userId))
        {
            await next(context);
        }
    }
    else
    {
        await next(context);
    }
});

app.UseRateLimiter();

app.MapControllers();
app.MapHub<AdminHub>("/hubs/admin");

app.Run();

public partial class Program { }