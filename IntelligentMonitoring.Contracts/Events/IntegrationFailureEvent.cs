namespace IntelligentMonitoring.Contracts.Events;

public sealed record IntegrationFailureEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public int EventVersion { get; init; } = 1;

    public DateTimeOffset OccurredAtUtc { get; init; }
        = DateTimeOffset.UtcNow;

    public string ServiceName { get; init; } = string.Empty;

    public string Environment { get; init; } = string.Empty;

    public string IntegrationType { get; init; } = string.Empty;

    public string TransactionId { get; init; } = string.Empty;

    public string TraceId { get; init; } = string.Empty;

    public string ErrorCode { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;
}