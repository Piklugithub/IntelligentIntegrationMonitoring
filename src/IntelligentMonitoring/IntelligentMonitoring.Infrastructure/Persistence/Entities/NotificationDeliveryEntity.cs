
namespace IntelligentMonitoring.Infrastructure.Persistence.Entities;

public sealed class NotificationDeliveryEntity
{
    public Guid Id { get; set; }

    // Original investigation event identifier
    public Guid EventId { get; set; }

    // Prevents duplicate notification records
    public string IdempotencyKey { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    // Slack channel ID, email address, etc.
    public string Destination { get; set; } = string.Empty;

    // Notification content stored as JSON
    public string PayloadJson { get; set; } = string.Empty;

    // Pending, Processing, Sent, Retrying, Failed
    public string Status { get; set; } = "Pending";

    public int AttemptCount { get; set; }

    public DateTimeOffset? NextAttemptAtUtc { get; set; }

    public DateTimeOffset? LastAttemptAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? ExternalMessageId { get; set; }

    public string? LastError { get; set; }

    // Concurrent worker ownership
    public Guid? LeaseId { get; set; }

    public DateTimeOffset? ProcessingLeaseExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
