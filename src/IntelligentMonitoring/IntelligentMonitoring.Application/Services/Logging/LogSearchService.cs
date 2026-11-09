
using IntelligentMonitoring.Application.Abstractions.Logging;
using IntelligentMonitoring.Application.Models.Logging;

namespace IntelligentMonitoring.Application.Services.Logging;

public sealed class LogSearchService(
    ILokiLogRepository lokiLogRepository) : ILogSearchService
{
    public Task<IReadOnlyList<LokiLogEntry>> SearchByTransactionIdAsync(
        string transactionId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        ValidateSearch(transactionId, startTime, endTime);

        return lokiLogRepository.SearchByTransactionIdAsync(
            transactionId,
            startTime,
            endTime,
            cancellationToken);
    }

    public Task<IReadOnlyList<LokiLogEntry>> SearchByTraceIdAsync(
        string traceId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        ValidateSearch(traceId, startTime, endTime);

        return lokiLogRepository.SearchByTraceIdAsync(
            traceId,
            startTime,
            endTime,
            cancellationToken);
    }

    private static void ValidateSearch(
        string identifier,
        DateTimeOffset startTime,
        DateTimeOffset endTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        if (startTime >= endTime)
        {
            throw new ArgumentException(
                "Start time must be earlier than end time.");
        }
    }
}
