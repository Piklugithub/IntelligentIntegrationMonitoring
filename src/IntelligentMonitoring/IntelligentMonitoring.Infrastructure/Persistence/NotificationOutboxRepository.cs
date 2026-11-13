
using IntelligentMonitoring.Infrastructure.Persistence.Entities;
using IntelligentMonitoring.Notification.Application.Abstractions.Persistence;
using IntelligentMonitoring.Notification.Application.Models.Outbox;
using Microsoft.EntityFrameworkCore;

namespace IntelligentMonitoring.Infrastructure.Persistence;

public sealed class NotificationOutboxRepository(
    MonitoringDbContext dbContext)
    : INotificationOutboxRepository
{
    public async Task<IReadOnlyList<NotificationOutboxWorkItem>> GetDueEventsAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        return await dbContext.NotificationOutbox
            .AsNoTracking()
            .Where(x =>
                (
                    (x.Status == "Pending" || x.Status == "Retrying") &&
                    (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now)
                )
                ||
                (
                    x.Status == "Processing" &&
                    (
                        x.ProcessingLeaseExpiresAtUtc == null ||
                        x.ProcessingLeaseExpiresAtUtc <= now
                    )
                ))
            .OrderBy(x => x.NextAttemptAtUtc ?? x.CreatedAtUtc)
            .Take(batchSize)
            .Select(x => new NotificationOutboxWorkItem
            {
                Id = x.Id,
                EventId = x.EventId,
                IdempotencyKey = x.IdempotencyKey,
                PayloadJson = x.PayloadJson,
                AttemptCount = x.AttemptCount
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid?> TryClaimAsync(
        Guid outboxId,
        DateTimeOffset leaseExpiresAtUtc,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var leaseId = Guid.NewGuid();

        var affectedRows = await dbContext.NotificationOutbox
            .Where(x =>
                x.Id == outboxId &&
                (
                    (
                        (x.Status == "Pending" || x.Status == "Retrying") &&
                        (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now)
                    )
                    ||
                    (
                        x.Status == "Processing" &&
                        (
                            x.ProcessingLeaseExpiresAtUtc == null ||
                            x.ProcessingLeaseExpiresAtUtc <= now
                        )
                    )
                ))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "Processing")
                .SetProperty(x => x.LeaseId, (Guid?)leaseId)
                .SetProperty(x => x.ProcessingLeaseExpiresAtUtc, leaseExpiresAtUtc)
                .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                .SetProperty(x => x.LastAttemptAtUtc, now)
                .SetProperty(x => x.UpdatedAtUtc, now),
                cancellationToken);

        return affectedRows == 1 ? leaseId : null;
    }

    public async Task MarkProcessedAsync(
        Guid outboxId,
        Guid leaseId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await dbContext.NotificationOutbox
            .Where(x =>
                x.Id == outboxId &&
                x.Status == "Processing" &&
                x.LeaseId == leaseId &&
                x.ProcessingLeaseExpiresAtUtc != null &&
                x.ProcessingLeaseExpiresAtUtc > completedAtUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "Processed")
                .SetProperty(x => x.CompletedAtUtc, completedAtUtc)
                .SetProperty(x => x.NextAttemptAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.LastError, (string?)null)
                .SetProperty(x => x.LeaseId, (Guid?)null)
                .SetProperty(x => x.ProcessingLeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.UpdatedAtUtc, completedAtUtc),
                cancellationToken);

        EnsureLeaseOwned(affectedRows, outboxId);
    }

    public async Task ScheduleRetryAsync(
        Guid outboxId,
        Guid leaseId,
        DateTimeOffset nextAttemptAtUtc,
        string lastError,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await dbContext.NotificationOutbox
            .Where(x =>
                x.Id == outboxId &&
                x.Status == "Processing" &&
                x.LeaseId == leaseId &&
                x.ProcessingLeaseExpiresAtUtc != null &&
                x.ProcessingLeaseExpiresAtUtc > attemptedAtUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "Retrying")
                .SetProperty(x => x.NextAttemptAtUtc, nextAttemptAtUtc)
                .SetProperty(x => x.LastError, lastError)
                .SetProperty(x => x.LeaseId, (Guid?)null)
                .SetProperty(x => x.ProcessingLeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.UpdatedAtUtc, attemptedAtUtc),
                cancellationToken);

        EnsureLeaseOwned(affectedRows, outboxId);
    }

    public async Task MarkFailedAsync(
        Guid outboxId,
        Guid leaseId,
        string lastError,
        DateTimeOffset failedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await dbContext.NotificationOutbox
            .Where(x =>
                x.Id == outboxId &&
                x.Status == "Processing" &&
                x.LeaseId == leaseId &&
                x.ProcessingLeaseExpiresAtUtc != null &&
                x.ProcessingLeaseExpiresAtUtc > failedAtUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "Failed")
                .SetProperty(x => x.CompletedAtUtc, failedAtUtc)
                .SetProperty(x => x.NextAttemptAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.LastError, lastError)
                .SetProperty(x => x.LeaseId, (Guid?)null)
                .SetProperty(x => x.ProcessingLeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.UpdatedAtUtc, failedAtUtc),
                cancellationToken);

        EnsureLeaseOwned(affectedRows, outboxId);
    }

    private static void EnsureLeaseOwned(int affectedRows, Guid outboxId)
    {
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Cannot update outbox event {outboxId}. " +
                "The lease is expired, invalid, or no longer owned by this worker.");
        }
    }
}
