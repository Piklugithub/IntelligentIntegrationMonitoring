namespace IntelligentMonitoring.Infrastructure.Persistence.Entities;

public sealed class FailureInvestigationEntity
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public string Status { get; set; } = string.Empty;

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

    public string ServicesInvolvedJson { get; set; } = "[]";

    public string TimelineJson { get; set; } = "[]";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public IntegrationFailureEntity? Failure { get; set; }
}