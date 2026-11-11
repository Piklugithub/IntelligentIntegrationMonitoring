namespace IntelligentMonitoring.Application.Models.Investigations;

public sealed class FailureInvestigationRecord
{
    public Guid EventId { get; set; }

    public int EventVersion { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public string Environment { get; set; } = string.Empty;

    public string IntegrationType { get; set; } = string.Empty;

    public string TransactionId { get; set; } = string.Empty;

    public string TraceId { get; set; } = string.Empty;

    public string ErrorCode { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;
    public FailureAnalysisResult? AiAnalysis { get; set; }

    public TransactionInvestigationReport Investigation { get; set; } = new();
}