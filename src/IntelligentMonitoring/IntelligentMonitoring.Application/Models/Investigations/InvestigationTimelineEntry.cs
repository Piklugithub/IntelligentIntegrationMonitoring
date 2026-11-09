
namespace IntelligentMonitoring.Application.Models.Investigations;

public sealed class InvestigationTimelineEntry
{
    public DateTimeOffset Timestamp { get; set; }

    public string? ServiceName { get; set; }

    public string Level { get; set; } = "Unknown";

    public string? EventType { get; set; }

    public string? Status { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? ErrorCode { get; set; }

    public double? DurationMs { get; set; }
}
