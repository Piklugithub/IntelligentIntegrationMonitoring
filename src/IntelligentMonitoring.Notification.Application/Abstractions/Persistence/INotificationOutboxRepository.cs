
using IntelligentMonitoring.Notification.Application.Models.Outbox;

namespace IntelligentMonitoring.Notification.Application.Abstractions.Persistence;

public interface INotificationOutboxRepository
{
    Task<IReadOnlyList<NotificationOutboxWorkItem>> GetDueEventsAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<Guid?> TryClaimAsync(
        Guid outboxId,
        DateTimeOffset leaseExpiresAtUtc,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(
        Guid outboxId,
        Guid leaseId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default);

    Task ScheduleRetryAsync(
        Guid outboxId,
        Guid leaseId,
        DateTimeOffset nextAttemptAtUtc,
        string lastError,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid outboxId,
        Guid leaseId,
        string lastError,
        DateTimeOffset failedAtUtc,
        CancellationToken cancellationToken = default);
}
