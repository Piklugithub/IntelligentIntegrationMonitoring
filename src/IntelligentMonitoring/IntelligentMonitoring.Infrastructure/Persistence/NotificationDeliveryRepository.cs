
using System.Text.Json;
using IntelligentMonitoring.Infrastructure.Persistence.Entities;
using IntelligentMonitoring.Notification.Application.Abstractions.Persistence;
using IntelligentMonitoring.Notification.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelligentMonitoring.Infrastructure.Persistence;

public sealed class NotificationDeliveryRepository(
    MonitoringDbContext dbContext)
    : INotificationDeliveryRepository
{
    private const string Pending = "Pending";
    private const string Processing = "Processing";
    private const string Retrying = "Retrying";
    private const string Sent = "Sent";
    private const string Failed = "Failed";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<bool> EnqueueAsync(
        NotificationDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Event);

        if (string.IsNullOrWhiteSpace(request.Destination))
            throw new ArgumentException(
                "Notification destination cannot be empty.",
                nameof(request));

        if (request.Event.EventId == Guid.Empty)
            throw new ArgumentException(
                "EventId cannot be empty.",
                nameof(request));

        var idempotencyKey = request.IdempotencyKey;

        var delivery = new NotificationDeliveryEntity
        {
            Id = Guid.NewGuid(),
            EventId = request.Event.EventId,
            IdempotencyKey = idempotencyKey,
            Channel = request.Channel.ToString(),
            Destination = request.Destination,
            PayloadJson = JsonSerializer.Serialize(
                request.Event,
                JsonOptions),
            Status = Pending,
            AttemptCount = 0,
            NextAttemptAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.NotificationDeliveries.Add(delivery);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Detach the failed insert before checking for a duplicate.
            dbContext.Entry(delivery).State = EntityState.Detached;

            var alreadyExists = await dbContext.NotificationDeliveries
                .AsNoTracking()
                .AnyAsync(
                    x => x.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (alreadyExists)
                return false;

            // The database error was not caused by a duplicate key.
            throw;
        }
    }

    public async Task<IReadOnlyList<NotificationDeliveryWorkItem>>
        GetDueDeliveriesAsync(
            int batchSize,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
    {
        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        var deliveries = await dbContext.NotificationDeliveries
            .AsNoTracking()
            .Where(x =>
                (
                    (x.Status == Pending || x.Status == Retrying) &&
                    (x.NextAttemptAtUtc == null ||
                     x.NextAttemptAtUtc <= now)
                )
                ||
                (
                    x.Status == Processing &&
                    x.ProcessingLeaseExpiresAtUtc != null &&
                    x.ProcessingLeaseExpiresAtUtc <= now
                ))
            .OrderBy(x => x.NextAttemptAtUtc)
            .ThenBy(x => x.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var workItems = new List<NotificationDeliveryWorkItem>();

        foreach (var delivery in deliveries)
        {
            var notificationEvent =
                JsonSerializer.Deserialize<
                    IntelligentMonitoring.Notification.Contracts.Events.InvestigationCompletedEvent>(
                    delivery.PayloadJson,
                    JsonOptions);

            if (notificationEvent is null)
            {
                throw new InvalidOperationException(
                    $"Notification payload is invalid. DeliveryId: {delivery.Id}");
            }

            if (!Enum.TryParse<NotificationChannel>(
                    delivery.Channel,
                    true,
                    out var channel) ||
                !Enum.IsDefined(channel))
            {
                throw new InvalidOperationException(
                    $"Unknown notification channel '{delivery.Channel}'. " +
                    $"DeliveryId: {delivery.Id}");
            }

            workItems.Add(new NotificationDeliveryWorkItem
            {
                Id = delivery.Id,
                EventId = delivery.EventId,
                Event = notificationEvent,
                Channel = channel,
                Destination = delivery.Destination,
                AttemptCount = delivery.AttemptCount
            });
        }

        return workItems;
    }

    public async Task<Guid?> TryClaimAsync(
        Guid deliveryId,
        DateTimeOffset leaseExpiresAtUtc,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (leaseExpiresAtUtc <= now)
            throw new ArgumentException(
                "Lease expiry must be later than the current time.",
                nameof(leaseExpiresAtUtc));

        var leaseId = Guid.NewGuid();

        var affectedRows = await dbContext.NotificationDeliveries
            .Where(x =>
                x.Id == deliveryId &&
                (
                    (
                        (x.Status == Pending || x.Status == Retrying) &&
                        (x.NextAttemptAtUtc == null ||
                         x.NextAttemptAtUtc <= now)
                    )
                    ||
                    (
                        x.Status == Processing &&
                        x.ProcessingLeaseExpiresAtUtc != null &&
                        x.ProcessingLeaseExpiresAtUtc <= now
                    )
                ))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, Processing)
                .SetProperty(x => x.LeaseId, (Guid?)leaseId)
                .SetProperty(
                    x => x.ProcessingLeaseExpiresAtUtc,
                    (DateTimeOffset?)leaseExpiresAtUtc)
                .SetProperty(x => x.NextAttemptAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.LastAttemptAtUtc, (DateTimeOffset?)now)
                .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                .SetProperty(x => x.UpdatedAtUtc, now),
                cancellationToken);

        return affectedRows == 1 ? leaseId : null;
    }

    public async Task MarkSentAsync(
        Guid deliveryId,
        Guid leaseId,
        string? externalMessageId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await dbContext.NotificationDeliveries
            .Where(x =>
                x.Id == deliveryId &&
                x.Status == Processing &&
                x.LeaseId == leaseId &&
                x.ProcessingLeaseExpiresAtUtc != null &&
                x.ProcessingLeaseExpiresAtUtc > completedAtUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, Sent)
                .SetProperty(x => x.ExternalMessageId, externalMessageId)
                .SetProperty(x => x.CompletedAtUtc, (DateTimeOffset?)completedAtUtc)
                .SetProperty(x => x.NextAttemptAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.LastError, (string?)null)
                .SetProperty(x => x.LeaseId, (Guid?)null)
                .SetProperty(
                    x => x.ProcessingLeaseExpiresAtUtc,
                    (DateTimeOffset?)null)
                .SetProperty(x => x.UpdatedAtUtc, completedAtUtc),
                cancellationToken);

        EnsureOneRowUpdated(affectedRows, deliveryId);
    }

    public async Task ScheduleRetryAsync(
        Guid deliveryId,
        Guid leaseId,
        DateTimeOffset nextAttemptAtUtc,
        string? lastError,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var affectedRows = await dbContext.NotificationDeliveries
            .Where(x =>
                x.Id == deliveryId &&
                x.Status == Processing &&
                x.LeaseId == leaseId &&
                x.ProcessingLeaseExpiresAtUtc != null &&
                x.ProcessingLeaseExpiresAtUtc > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, Retrying)
                .SetProperty(x => x.NextAttemptAtUtc, (DateTimeOffset?)nextAttemptAtUtc)
                .SetProperty(x => x.LastError, TruncateError(lastError))
                .SetProperty(x => x.CompletedAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.LeaseId, (Guid?)null)
                .SetProperty(
                    x => x.ProcessingLeaseExpiresAtUtc,
                    (DateTimeOffset?)null)
                .SetProperty(x => x.UpdatedAtUtc, now),
                cancellationToken);

        EnsureOneRowUpdated(affectedRows, deliveryId);
    }

    public async Task MarkFailedAsync(
        Guid deliveryId,
        Guid leaseId,
        string lastError,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await dbContext.NotificationDeliveries
            .Where(x =>
                x.Id == deliveryId &&
                x.Status == Processing &&
                x.LeaseId == leaseId &&
                x.ProcessingLeaseExpiresAtUtc != null &&
                x.ProcessingLeaseExpiresAtUtc > completedAtUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, Failed)
                .SetProperty(x => x.LastError, TruncateError(lastError))
                .SetProperty(x => x.CompletedAtUtc, (DateTimeOffset?)completedAtUtc)
                .SetProperty(x => x.NextAttemptAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.LeaseId, (Guid?)null)
                .SetProperty(
                    x => x.ProcessingLeaseExpiresAtUtc,
                    (DateTimeOffset?)null)
                .SetProperty(x => x.UpdatedAtUtc, completedAtUtc),
                cancellationToken);

        EnsureOneRowUpdated(affectedRows, deliveryId);
    }

    private static void EnsureOneRowUpdated(
        int affectedRows,
        Guid deliveryId)
    {
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Notification delivery {deliveryId} could not be updated. " +
                "The delivery may no longer be owned by this worker, " +
                "or its lease may have expired.");
        }
    }

    private static string? TruncateError(string? error)
    {
        const int maxLength = 4000;

        if (string.IsNullOrEmpty(error))
            return error;

        return error.Length <= maxLength
            ? error
            : error[..maxLength];
    }
}
