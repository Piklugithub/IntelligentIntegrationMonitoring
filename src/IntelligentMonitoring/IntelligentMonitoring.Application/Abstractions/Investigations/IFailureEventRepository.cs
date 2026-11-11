
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

    Task<bool> MarkProcessingAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    Task ScheduleRetryAsync(
        Guid eventId,
        DateTimeOffset nextRetryAtUtc,
        string? lastError,
        CancellationToken cancellationToken = default);

    Task MarkNoLogsFoundAsync(
        Guid eventId,
        string? lastError,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid eventId,
        string lastError,
        CancellationToken cancellationToken = default);
}
