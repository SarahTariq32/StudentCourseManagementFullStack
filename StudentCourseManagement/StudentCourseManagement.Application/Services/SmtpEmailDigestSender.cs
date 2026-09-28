using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StudentCourseManagement.Application.DTOs;
using StudentCourseManagement.Application.Interfaces;

namespace StudentCourseManagement.Application.Services;

public class SmtpEmailDigestSender : IEmailDigestSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailDigestSender> _logger;

    public SmtpEmailDigestSender(IConfiguration configuration, ILogger<SmtpEmailDigestSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendDigestAsync(EnrollmentRequestAiSummaryDto summary, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Email:SmtpHost"] ?? throw new InvalidOperationException("Email:SmtpHost not configured.");
        var port = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
        var username = _configuration["Email:Username"] ?? throw new InvalidOperationException("Email:Username not configured.");
        var password = _configuration["Email:Password"] ?? throw new InvalidOperationException("Email:Password not configured.");
        var fromAddress = _configuration["Email:FromAddress"] ?? username;
        var toAddress = _configuration["Email:ToAddress"] ?? throw new InvalidOperationException("Email:ToAddress not configured.");

        var categoryLines = string.Join("\n", summary.Categories.Select(c => $"  - {c.Category}: {c.Count}"));

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(fromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = $"[Admin Digest] {summary.TotalPendingRequests} Pending Requests";
        message.Body = new TextPart("plain")
        {
            Text = $"Pending Requests Digest\n\nTotal Pending: {summary.TotalPendingRequests}\n\nBreakdown:\n{categoryLines}\n\nExecutive Summary:\n{summary.SummaryNote}"
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(username, password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("Digest email sent to {ToAddress}", toAddress);
    }
}