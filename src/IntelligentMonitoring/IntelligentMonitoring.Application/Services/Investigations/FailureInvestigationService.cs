
using IntelligentMonitoring.Application.Abstractions.Investigations;
using IntelligentMonitoring.Application.Abstractions.Logging;
using IntelligentMonitoring.Application.Models.Investigations;
using IntelligentMonitoring.Application.Models.Logging;

namespace IntelligentMonitoring.Application.Services.Investigations;

public sealed class FailureInvestigationService(
    ILogSearchService logSearchService)
    : IFailureInvestigationService
{
    public async Task<TransactionInvestigationReport>
        InvestigateTransactionAsync(
            string transactionId,
            DateTimeOffset startTime,
            DateTimeOffset endTime,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);

        if (startTime >= endTime)
        {
            throw new ArgumentException(
                "Start time must be earlier than end time.");
        }

        // 1. Retrieve transaction logs from Loki.
        var logs = await logSearchService.SearchByTransactionIdAsync(
            transactionId,
            startTime,
            endTime,
            cancellationToken);

        // 2. Sort logs chronologically.
        var orderedLogs = logs
            .OrderBy(x => x.Timestamp)
            .ToList();

        var report = new TransactionInvestigationReport
        {
            TransactionId = transactionId,
            HasLogs = orderedLogs.Count > 0,
            TotalLogs = orderedLogs.Count
        };

        if (orderedLogs.Count == 0)
        {
            report.Status = "NoLogsFound";
            return report;
        }

        // 3. Calculate log statistics.
        report.InformationCount = orderedLogs.Count(
            x => IsLevel(x.Level, "Information", "Info"));

        report.WarningCount = orderedLogs.Count(
            x => IsLevel(x.Level, "Warning", "Warn"));

        report.ErrorCount = orderedLogs.Count(IsFailure);

        // 4. Identify the first and last log timestamps.
        report.FirstLogAt = orderedLogs.First().Timestamp;
        report.LastLogAt = orderedLogs.Last().Timestamp;

        // 5. Identify services involved in the transaction.
        report.ServicesInvolved = orderedLogs
            .Where(x => !string.IsNullOrWhiteSpace(x.ServiceName))
            .Select(x => x.ServiceName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        // 6. Identify the first failure event.
        var firstFailure = orderedLogs.FirstOrDefault(IsFailure);

        report.HasFailures = firstFailure is not null;

        if (firstFailure is not null)
        {
            report.Status = "Failed";
            report.FailureTimestamp = firstFailure.Timestamp;
            report.FailureServiceName = firstFailure.ServiceName;
            report.ErrorCode = firstFailure.ErrorCode;
            report.FailureMessage = firstFailure.Message;
        }
        else if (orderedLogs.Any(
                     x => IsLevel(x.Level, "Information", "Info") &&
                          string.Equals(
                              x.Status,
                              "Success",
                              StringComparison.OrdinalIgnoreCase)))
        {
            report.Status = "Succeeded";
        }
        else
        {
            report.Status = "InProgress";
        }

        // 7. Build the transaction timeline.
        report.Timeline = orderedLogs
            .Select(MapTimelineEntry)
            .ToList();

        return report;
    }

    private static bool IsFailure(LokiLogEntry log)
    {
        return IsLevel(
                   log.Level,
                   "Error",
                   "Critical",
                   "Fatal")
               || IsLevel(log.Status, "Failed", "Failure")
               || (log.EventType?.Contains(
                       "Failed",
                       StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static bool IsLevel(
        string? actual,
        params string[] expected)
    {
        return expected.Any(value =>
            string.Equals(
                actual,
                value,
                StringComparison.OrdinalIgnoreCase));
    }

    private static InvestigationTimelineEntry MapTimelineEntry(
        LokiLogEntry log)
    {
        return new InvestigationTimelineEntry
        {
            Timestamp = log.Timestamp,
            ServiceName = log.ServiceName,
            Level = log.Level,
            EventType = log.EventType,
            Status = log.Status,
            Message = log.Message,
            ErrorCode = log.ErrorCode,
            DurationMs = log.DurationMs
        };
    }
}
