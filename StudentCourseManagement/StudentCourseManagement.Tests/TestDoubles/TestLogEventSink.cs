using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace StudentCourseManagement.Tests.TestDoubles;

public class TestLogEventSink : ILogEventSink
{
    private static readonly ConcurrentQueue<LogEvent> _events = new();
    private static readonly MessageTemplateTextFormatter Formatter = new(
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{CorrelationId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");

    public void Emit(LogEvent logEvent)
    {
        _events.Enqueue(logEvent);
    }

    public IReadOnlyList<LogEvent> GetAllLogs() => _events.ToList();

    public IReadOnlyList<LogEvent> GetLogsByCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            return Array.Empty<LogEvent>();

        return _events
            .Where(e =>
            {
                if (e.Properties.TryGetValue("CorrelationId", out var propValue))
                {
                    var val = propValue.ToString().Trim('"');
                    if (string.Equals(val, correlationId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                // Fallback check if rendered message contains correlationId
                return e.RenderMessage().Contains(correlationId, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
    }

    public IReadOnlyList<string> GetFormattedLogs(string correlationId)
    {
        var logs = GetLogsByCorrelationId(correlationId);
        var result = new List<string>(logs.Count);

        foreach (var log in logs)
        {
            using var writer = new StringWriter();
            Formatter.Format(log, writer);
            result.Add(writer.ToString().TrimEnd());
        }

        return result;
    }

    public void Clear()
    {
        _events.Clear();
    }
}
