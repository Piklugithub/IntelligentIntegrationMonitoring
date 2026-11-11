
using IntelligentMonitoring.Application.Abstractions.Investigations;
using IntelligentMonitoring.Application.Models.Investigations;
using IntelligentMonitoring.Contracts.Events;
using IntelligentMonitoring.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntelligentMonitoring.Infrastructure.Persistence;

public sealed class FailureEventRepository(
    MonitoringDbContext dbContext)
    : IFailureEventRepository
{
    public async Task<bool> SaveIfNotExistsAsync(
        IntegrationFailureEvent failureEvent,
        CancellationToken cancellationToken = default)
    {
        var alreadyExists = await dbContext.IntegrationFailures
            .AnyAsync(
                x => x.EventId == failureEvent.EventId,
                cancellationToken);

        if (alreadyExists)
            return false;

        var now = DateTimeOffset.UtcNow;

        var entity = new IntegrationFailureEntity
        {
            EventId = failureEvent.EventId,
            EventVersion = failureEvent.EventVersion,
            OccurredAtUtc = failureEvent.OccurredAtUtc,
            ServiceName = failureEvent.ServiceName,
            Environment = failureEvent.Environment,
            IntegrationType = failureEvent.IntegrationType,
            TransactionId = failureEvent.TransactionId,
            TraceId = failureEvent.TraceId,
            ErrorCode = failureEvent.ErrorCode,
            ErrorMessage = failureEvent.ErrorMessage,

            InvestigationStatus = "Pending",
            RetryCount = 0,
            NextRetryAtUtc = now,
            LastAttemptAtUtc = null,
            LastError = null,
            CompletedAtUtc = null,

            ProcessedAtUtc = now
        };

        dbContext.IntegrationFailures.Add(entity);

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<FailureEventWorkItem>>
        GetDueEventsAsync(
            int batchSize,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        var events = await dbContext.IntegrationFailures
            .AsNoTracking()
            .Where(x =>
                (x.InvestigationStatus == "Pending" ||
                 x.InvestigationStatus == "Retrying") &&
                (x.NextRetryAtUtc == null ||
                 x.NextRetryAtUtc <= now))
            .OrderBy(x => x.NextRetryAtUtc)
            .ThenBy(x => x.ProcessedAtUtc)
            .Take(batchSize)
            .Select(x => new FailureEventWorkItem
            {
                EventId = x.EventId,
                EventVersion = x.EventVersion,
                OccurredAtUtc = x.OccurredAtUtc,
                ServiceName = x.ServiceName,
                Environment = x.Environment,
                IntegrationType = x.IntegrationType,
                TransactionId = x.TransactionId,
                TraceId = x.TraceId,
                ErrorCode = x.ErrorCode,
                ErrorMessage = x.ErrorMessage,
                RetryCount = x.RetryCount
            })
            .ToListAsync(cancellationToken);

        return events;
    }

    public async Task<bool> MarkProcessingAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var affectedRows = await dbContext.IntegrationFailures
            .Where(x =>
                x.EventId == eventId &&
                (x.InvestigationStatus == "Pending" ||
                 x.InvestigationStatus == "Retrying") &&
                (x.NextRetryAtUtc == null ||
                 x.NextRetryAtUtc <= now))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.InvestigationStatus,
                        "Processing")
                    .SetProperty(
                        x => x.LastAttemptAtUtc,
                        now)
                    .SetProperty(
                        x => x.NextRetryAtUtc,
                        (DateTimeOffset?)null),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task ScheduleRetryAsync(
        Guid eventId,
        DateTimeOffset nextRetryAtUtc,
        string? lastError,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await dbContext.IntegrationFailures
            .Where(x =>
                x.EventId == eventId &&
                x.InvestigationStatus == "Processing")
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.InvestigationStatus,
                        "Retrying")
                    .SetProperty(
                        x => x.RetryCount,
                        x => x.RetryCount + 1)
                    .SetProperty(
                        x => x.NextRetryAtUtc,
                        nextRetryAtUtc)
                    .SetProperty(
                        x => x.LastError,
                        lastError),
                cancellationToken);

        EnsureOneRowUpdated(affectedRows, eventId, "schedule retry");
    }

    public async Task MarkNoLogsFoundAsync(
        Guid eventId,
        string? lastError,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var affectedRows = await dbContext.IntegrationFailures
            .Where(x =>
                x.EventId == eventId &&
                x.InvestigationStatus == "Processing")
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.InvestigationStatus,
                        "NoLogsFound")
                    .SetProperty(
                        x => x.NextRetryAtUtc,
                        (DateTimeOffset?)null)
                    .SetProperty(
                        x => x.LastError,
                        lastError)
                    .SetProperty(
                        x => x.CompletedAtUtc,
                        now),
                cancellationToken);

        EnsureOneRowUpdated(affectedRows, eventId, "mark no logs found");
    }

    public async Task MarkFailedAsync(
        Guid eventId,
        string lastError,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var affectedRows = await dbContext.IntegrationFailures
            .Where(x =>
                x.EventId == eventId &&
                x.InvestigationStatus == "Processing")
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        x => x.InvestigationStatus,
                        "Failed")
                    .SetProperty(
                        x => x.NextRetryAtUtc,
                        (DateTimeOffset?)null)
                    .SetProperty(
                        x => x.LastError,
                        lastError)
                    .SetProperty(
                        x => x.CompletedAtUtc,
                        now),
                cancellationToken);

        EnsureOneRowUpdated(affectedRows, eventId, "mark failed");
    }

    private static void EnsureOneRowUpdated(
        int affectedRows,
        Guid eventId,
        string operation)
    {
        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Could not {operation} for EventId {eventId}. " +
                $"Expected one updated row, but found {affectedRows}.");
        }
    }
}
