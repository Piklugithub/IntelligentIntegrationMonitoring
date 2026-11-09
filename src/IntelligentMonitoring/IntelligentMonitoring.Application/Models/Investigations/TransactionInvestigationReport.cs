
namespace IntelligentMonitoring.Application.Models.Investigations;

public sealed class TransactionInvestigationReport
{
    public string TransactionId { get; set; } = string.Empty;

    public string Status { get; set; } = "Unknown";

    public bool HasLogs { get; set; }

    public bool HasFailures { get; set; }

    public int TotalLogs { get; set; }

    public int InformationCount { get; set; }

    public int WarningCount { get; set; }

    public int ErrorCount { get; set; }

    public DateTimeOffset? FirstLogAt { get; set; }

    public DateTimeOffset? LastLogAt { get; set; }

    public DateTimeOffset? FailureTimestamp { get; set; }

    public string? FailureServiceName { get; set; }

    public string? ErrorCode { get; set; }

    public string? FailureMessage { get; set; }

    public List<string> ServicesInvolved { get; set; } = [];

    public List<InvestigationTimelineEntry> Timeline { get; set; } = [];
}
