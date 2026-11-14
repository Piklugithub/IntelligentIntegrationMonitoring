
using IntelligentMonitoring.Notification.Application.Models;

namespace IntelligentMonitoring.Notification.Application.Abstractions.Persistence;

public interface INotificationDeliveryRepository
{
    Task<bool> EnqueueAsync(
        NotificationDispatchRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationDeliveryWorkItem>> GetDueDeliveriesAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<Guid?> TryClaimAsync(
        Guid deliveryId,
        DateTimeOffset leaseExpiresAtUtc,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task MarkSentAsync(
        Guid deliveryId,
        Guid leaseId,
        string? externalMessageId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default);

    Task ScheduleRetryAsync(
        Guid deliveryId,
        Guid leaseId,
        DateTimeOffset nextAttemptAtUtc,
        string? lastError,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid deliveryId,
        Guid leaseId,
        string lastError,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default);
}
