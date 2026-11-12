
using IntelligentMonitoring.Application.Models.Investigations;
using IntelligentMonitoring.Contracts.Events;

namespace IntelligentMonitoring.Application.Abstractions.Investigations;

public interface IFailureEventRepository
{
    Task<bool> SaveIfNotExistsAsync(
        IntegrationFailureEvent failureEvent,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FailureEventWorkItem>> GetDueEventsAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<Guid?> MarkProcessingAsync(
        Guid eventId,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default);

    Task ScheduleRetryAsync(
        Guid eventId,
        Guid leaseId,
        DateTimeOffset nextRetryAtUtc,
        string? lastError,
        CancellationToken cancellationToken = default);

    Task MarkNoLogsFoundAsync(
        Guid eventId,
        Guid leaseId,
        string? lastError,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid eventId,
        Guid leaseId,
        string lastError,
        CancellationToken cancellationToken = default);
}
