
namespace IntelligentMonitoring.Infrastructure.Persistence.Entities;

public sealed class NotificationOutboxEntity
{
    public Guid Id { get; set; }

    // Original completed investigation event.
    public Guid EventId { get; set; }

    // Stable key for duplicate detection.
    public string IdempotencyKey { get; set; } = string.Empty;

    // Serialized InvestigationCompletedEvent.
    public string PayloadJson { get; set; } = string.Empty;

    // Pending, Processing, Processed, Failed.
    public string Status { get; set; } = "Pending";

    public int AttemptCount { get; set; }

    public DateTimeOffset? NextAttemptAtUtc { get; set; }

    public DateTimeOffset? LastAttemptAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? LastError { get; set; }

    // Concurrent worker ownership.
    public Guid? LeaseId { get; set; }

    public DateTimeOffset? ProcessingLeaseExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
