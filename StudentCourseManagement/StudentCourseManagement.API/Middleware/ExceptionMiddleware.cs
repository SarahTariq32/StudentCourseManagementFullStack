using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace StudentCourseManagement.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}. Path: {Path}, Method: {Method}", 
                ex.Message, context.Request.Path, context.Request.Method);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";

        var statusCode = HttpStatusCode.InternalServerError;
        var message = "An unexpected error occurred. Please try again later.";

        if (ex is StudentCourseManagement.Application.Exceptions.EmbeddingProviderUnavailableException ||
            ex is System.Net.Http.HttpRequestException)
        {
            statusCode = HttpStatusCode.BadGateway;
            message = ex.Message;
        }
        else if (ex is KeyNotFoundException)
        {
            statusCode = HttpStatusCode.NotFound;
            message = ex.Message;
        }
        else if (ex is UnauthorizedAccessException)
        {
            statusCode = HttpStatusCode.Unauthorized;
            message = ex.Message;
        }
        else if (ex is ArgumentException or FormatException)
        {
            statusCode = HttpStatusCode.BadRequest;
            message = ex.Message;
        }
        else if (ex is InvalidOperationException)
        {
            statusCode = HttpStatusCode.Conflict;
            message = ex.Message;
        }

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            statusCode = (int)statusCode,
            message = message
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}