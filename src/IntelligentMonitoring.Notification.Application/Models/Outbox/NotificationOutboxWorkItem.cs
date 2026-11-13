
namespace IntelligentMonitoring.Notification.Application.Models.Outbox;

public sealed record NotificationOutboxWorkItem
{
    public Guid Id { get; init; }

    public Guid EventId { get; init; }

    public string IdempotencyKey { get; init; } = string.Empty;

    public string PayloadJson { get; init; } = string.Empty;

    public int AttemptCount { get; init; }
}
