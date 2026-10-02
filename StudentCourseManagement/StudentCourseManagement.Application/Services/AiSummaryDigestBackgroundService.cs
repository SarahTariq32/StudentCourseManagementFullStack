using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.Application.Services;

public class AiSummaryDigestBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiSummaryDigestBackgroundService> _logger;
    private string? _lastEmailedHash;

    public AiSummaryDigestBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AiSummaryDigestBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cacheMinutes = _configuration.GetValue("AiSummaryJob:CacheRefreshIntervalMinutes", 1);
        var emailMinutes = _configuration.GetValue("AiSummaryJob:EmailDigestIntervalMinutes", 60);

        await Task.WhenAll(
            RunCacheRefreshLoopAsync(TimeSpan.FromMinutes(cacheMinutes), stoppingToken),
            RunEmailDigestLoopAsync(TimeSpan.FromMinutes(emailMinutes), stoppingToken)
        );
    }

    private async Task RunCacheRefreshLoopAsync(TimeSpan interval, CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);
        
        try
        {
            await RegenerateSummaryAsync(stoppingToken);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RegenerateSummaryAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AiSummaryJob] Error during summary generation loop.");
        }
    }

    private async Task RunEmailDigestLoopAsync(TimeSpan interval, CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SendDigestIfChangedAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AiSummaryJob] Error during email digest loop.");
        }
    }

    private async Task<EnrollmentRequestAiSummaryDto?> RegenerateSummaryAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var aiCourseService = scope.ServiceProvider.GetRequiredService<IAiCourseService>();

        try
        {
            EnrollmentRequestAiSummaryDto? finalSummary = null;

            await foreach (var raw in aiCourseService.StreamPendingRequestsSummaryTextAsync(stoppingToken))
            {
                var envelope = JsonSerializer.Deserialize<JsonElement>(raw);
                if (envelope.TryGetProperty("type", out var t) && t.GetString() == "done")
                {
                    finalSummary = JsonSerializer.Deserialize<EnrollmentRequestAiSummaryDto>(
                        envelope.GetProperty("summary").GetRawText(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }

            _logger.LogInformation("[AiSummaryJob] Cache refreshed at {Time}", DateTime.UtcNow);
            return finalSummary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AiSummaryJob] Cache refresh failed.");
            return null;
        }
    }

    private async Task SendDigestIfChangedAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailDigestSender>();

        if (!cache.TryGetValue(AiCourseService.SummaryCacheKey, out EnrollmentRequestAiSummaryDto? summary) || summary == null)
        {
            summary = await RegenerateSummaryAsync(stoppingToken);
            if (summary == null) return;
        }

        var currentHash = ComputeHash($"{summary.TotalPendingRequests}|{summary.SummaryNote}");
        if (currentHash == _lastEmailedHash)
        {
            _logger.LogInformation("[AiSummaryJob] Skipping email — no change since last digest.");
            return;
        }

        try
        {
            await emailSender.SendDigestAsync(summary, stoppingToken);
            _lastEmailedHash = currentHash;
            _logger.LogInformation("[AiSummaryJob] Digest email sent at {Time}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AiSummaryJob] Failed to send digest email.");
        }
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}