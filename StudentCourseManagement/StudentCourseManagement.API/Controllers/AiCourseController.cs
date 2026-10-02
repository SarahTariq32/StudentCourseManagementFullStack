using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudentCourseManagement.Application.Exceptions;
using StudentCourseManagement.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StudentCourseManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AiCourseController : ControllerBase
{
    private readonly IAiCourseService _aiCourseService;

    public AiCourseController(IAiCourseService aiCourseService)
    {
        _aiCourseService = aiCourseService;
    }

    [HttpGet("search/strict")]
    [Authorize]
    [EnableRateLimiting("AiSearchLimit")]
    public async Task<IActionResult> SearchStrict([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { message = "Query parameter is required." });

        var username = ResolveUsername();

        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized(new { message = "Could not resolve student identity from token." });

        var result = await _aiCourseService.SearchStrictAsync(username, query);
        return Ok(result);
    }

    [HttpGet("search/free")]
    [Authorize]
    [EnableRateLimiting("AiSearchLimit")]
    public async Task<IActionResult> SearchFreeform([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { message = "Query parameter is required." });

        var username = ResolveUsername();

        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized(new { message = "Could not resolve student identity from token." });

        var result = await _aiCourseService.SearchFreeformAsync(username, query);
        return Ok(result);
    }

    [HttpGet("documents")]
    [Authorize(Roles = "Admin,admin")]
    [EnableRateLimiting("AiAdminLimit")]
    public async Task<IActionResult> GetDocuments([FromServices] IVectorStore vectorStore)
    {
        var documents = await vectorStore.GetIndexedDocumentsAsync();
        return Ok(documents);
    }

    private string? ResolveUsername()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.FindFirst("unique_name")?.Value
            ?? User.FindFirst("name")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.Identity?.Name;
    }

    [HttpGet("enrollmentrequests/ai-summary")]
    [Authorize(Roles = "Admin,admin")]
    [EnableRateLimiting("AiAdminLimit")]
    public async Task<IActionResult> GetPendingRequestsSummary(CancellationToken cancellationToken)
    {
        var summary = await _aiCourseService.GetPendingRequestsSummaryAsync(cancellationToken);
        return Ok(summary);
    }

    [HttpGet("enrollmentrequests/ai-summary/stream")]
    [Authorize(Roles = "Admin,admin")]
    [EnableRateLimiting("AiAdminLimit")]
    public async Task StreamPendingRequestsSummary(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        try
        {
            await foreach (var chunk in _aiCourseService.StreamPendingRequestsSummaryTextAsync(cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk))
                {
                    var jsonChunk = JsonSerializer.Serialize(chunk);
                    await Response.WriteAsync($"data: {jsonChunk}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var errorPayload = JsonSerializer.Serialize(new { type = "error", message = ex.Message, canRetry = true });
            var jsonChunk = JsonSerializer.Serialize(errorPayload);
            await Response.WriteAsync($"data: {jsonChunk}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpPost("upload-handbook")]
    [Authorize(Roles = "Admin,admin")]
    [EnableRateLimiting("AiAdminLimit")]
    public async Task<IActionResult> UploadHandbook(
        IFormFile file,
        [FromServices] IDocumentIngestionService ingestionService)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded." });

        try
        {
            var result = await ingestionService.IngestDocumentAsync(
                file.FileName, file.ContentType, file.OpenReadStream());

            return Ok(new
            {
                message = $"Successfully processed and indexed {result.ChunkCount} chunks across {result.PageCount} page(s) for {file.FileName}.",
                result.ChunkCount,
                result.PageCount
            });
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (EmbeddingProviderUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Failed to parse document: {ex.Message}" });
        }
    }

    [HttpDelete("documents/{**documentName}")]
    [Authorize(Roles = "Admin,admin")]
    [EnableRateLimiting("AiAdminLimit")]
    public async Task<IActionResult> DeleteDocument(
        string documentName,
        [FromServices] IVectorStore vectorStore)
    {
        if (string.IsNullOrWhiteSpace(documentName))
            return BadRequest(new { message = "Document name is required." });

        await vectorStore.DeleteDocumentAsync(documentName);

        return Ok(new { message = $"Document '{documentName}' and all its indexed chunks were deleted." });
    }

#if DEBUG
    [HttpGet("test-error")]
    [AllowAnonymous]
    public IActionResult TriggerTestError()
    {
        throw new InvalidOperationException("Deliberately triggered test error for structured logging verification.");
    }
#endif
}