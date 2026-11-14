
namespace IntelligentMonitoring.Notification.Contracts.Events;

public sealed record InvestigationCompletedEvent
{
    public Guid EventId { get; init; }

    public string TransactionId { get; init; } = string.Empty;

    public string TraceId { get; init; } = string.Empty;

    public string ServiceName { get; init; } = string.Empty;

    public string Environment { get; init; } = string.Empty;

    public string IntegrationType { get; init; } = string.Empty;

    public string InvestigationStatus { get; init; } = string.Empty;

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public string? AiSummary { get; init; }

    public string? AiProbableRootCause { get; init; }

    public string? AiBusinessImpact { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }

    // Stable identifier used for duplicate-event detection.
    public string IdempotencyKey =>
        $"investigation-completed:{EventId}";
}
