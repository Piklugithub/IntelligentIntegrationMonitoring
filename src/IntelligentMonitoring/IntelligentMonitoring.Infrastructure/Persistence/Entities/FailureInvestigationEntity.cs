
namespace IntelligentMonitoring.Infrastructure.Persistence.Entities;

public sealed class FailureInvestigationEntity
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    // Deterministic investigation
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

    // AI investigation
    public string? AiSummary { get; set; }

    public string? AiFailureStage { get; set; }

    public string? AiProbableRootCause { get; set; }

    public string? AiRootCauseConfidence { get; set; }

    public string? AiBusinessImpact { get; set; }

    public string AiSupportingEvidenceJson { get; set; } = "[]";

    public string AiRecommendedActionsJson { get; set; } = "[]";

    public string AiInformationGapsJson { get; set; } = "[]";

    public string? AiAnalysisStatus { get; set; }

    public string? AiModelName { get; set; }

    public DateTimeOffset? AiAnalyzedAtUtc { get; set; }

    // Persistence metadata
    public DateTimeOffset CreatedAtUtc { get; set; }

    public IntegrationFailureEntity? Failure { get; set; }
}
