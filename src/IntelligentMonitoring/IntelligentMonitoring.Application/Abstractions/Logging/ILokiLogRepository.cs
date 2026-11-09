using IntelligentMonitoring.Application.Models.Logging;

namespace IntelligentMonitoring.Application.Abstractions.Logging;

public interface ILokiLogRepository
{
    Task<IReadOnlyList<LokiLogEntry>> SearchByTransactionIdAsync(
        string transactionId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LokiLogEntry>> SearchByTraceIdAsync(
        string traceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);
}