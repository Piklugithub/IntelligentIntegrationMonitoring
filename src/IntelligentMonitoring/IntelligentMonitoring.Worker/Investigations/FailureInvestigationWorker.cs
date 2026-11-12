
using IntelligentMonitoring.Application.Abstractions.Investigations;
using IntelligentMonitoring.Application.Models.Investigations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntelligentMonitoring.Worker.Investigations;

public sealed class FailureInvestigationWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<FailureInvestigationWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10)
    ];

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var pollingInterval = configuration.GetValue<int>(
            "Investigation:PollingIntervalSeconds", 10);

        var batchSize = configuration.GetValue<int>(
            "Investigation:BatchSize", 10);

        var lookbackHours = configuration.GetValue<int>(
            "Investigation:LookbackHours", 24);

        var postFailureBufferMinutes = configuration.GetValue<int>(
            "Investigation:PostFailureBufferMinutes", 15);

        var maxRetries = configuration.GetValue<int>(
            "Investigation:MaxRetries", 5);

        var processingLeaseMinutes = configuration.GetValue<int>(
            "Investigation:ProcessingLeaseMinutes", 5);

        ValidateConfiguration(
            pollingInterval,
            batchSize,
            lookbackHours,
            postFailureBufferMinutes,
            maxRetries,
            processingLeaseMinutes);

        logger.LogInformation(
            """
            Failure investigation worker started.
            PollingIntervalSeconds: {PollingInterval}
            BatchSize: {BatchSize}
            LookbackHours: {LookbackHours}
            PostFailureBufferMinutes: {BufferMinutes}
            MaxRetries: {MaxRetries}
            ProcessingLeaseMinutes: {LeaseMinutes}
            """,
            pollingInterval,
            batchSize,
            lookbackHours,
            postFailureBufferMinutes,
            maxRetries,
            processingLeaseMinutes);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(pollingInterval));

        do
        {
            try
            {
                await ProcessDueEventsAsync(
                    batchSize,
                    lookbackHours,
                    postFailureBufferMinutes,
                    maxRetries,
                    processingLeaseMinutes,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unexpected error while polling pending investigations.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

        } while (!stoppingToken.IsCancellationRequested);

        logger.LogInformation("Failure investigation worker stopped.");
    }

    private async Task ProcessDueEventsAsync(
        int batchSize,
        int lookbackHours,
        int postFailureBufferMinutes,
        int maxRetries,
        int processingLeaseMinutes,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var eventRepository =
            scope.ServiceProvider
                .GetRequiredService<IFailureEventRepository>();

        var dueEvents = await eventRepository.GetDueEventsAsync(
            batchSize,
            DateTimeOffset.UtcNow,
            cancellationToken);

        if (dueEvents.Count == 0)
            return;

        logger.LogInformation(
            "Found {Count} due failure investigation(s).",
            dueEvents.Count);

        foreach (var workItem in dueEvents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var leaseExpiresAtUtc =
                DateTimeOffset.UtcNow.AddMinutes(processingLeaseMinutes);

            // Atomically claim the event and receive its ownership token.
            var leaseId = await eventRepository.MarkProcessingAsync(
                workItem.EventId,
                leaseExpiresAtUtc,
                cancellationToken);

            if (leaseId is null)
            {
                logger.LogInformation(
                    "Event {EventId} was not claimed. It may already be processing.",
                    workItem.EventId);

                continue;
            }

            logger.LogInformation(
                "Event claimed successfully. EventId: {EventId}, LeaseId: {LeaseId}, LeaseExpiresAtUtc: {LeaseExpiresAtUtc}",
                workItem.EventId,
                leaseId.Value,
                leaseExpiresAtUtc);

            await ProcessSingleEventAsync(
                workItem,
                leaseId.Value,
                lookbackHours,
                postFailureBufferMinutes,
                maxRetries,
                cancellationToken);
        }
    }

    private async Task ProcessSingleEventAsync(
        FailureEventWorkItem workItem,
        Guid leaseId,
        int lookbackHours,
        int postFailureBufferMinutes,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Starting investigation. EventId: {EventId}, TransactionId: {TransactionId}, LeaseId: {LeaseId}, RetryCount: {RetryCount}",
            workItem.EventId,
            workItem.TransactionId,
            leaseId,
            workItem.RetryCount);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            var investigationService =
                scope.ServiceProvider
                    .GetRequiredService<IFailureInvestigationService>();

            var failureAnalysisService =
                scope.ServiceProvider
                    .GetRequiredService<IFailureAnalysisService>();

            var investigationRepository =
                scope.ServiceProvider
                    .GetRequiredService<IFailureInvestigationRepository>();

            var startTime =
                workItem.OccurredAtUtc.AddHours(-lookbackHours);

            var endTime = Max(
                DateTimeOffset.UtcNow,
                workItem.OccurredAtUtc.AddMinutes(
                    postFailureBufferMinutes));

            var report =
                await investigationService.InvestigateTransactionAsync(
                    workItem.TransactionId,
                    startTime,
                    endTime,
                    cancellationToken);

            logger.LogInformation(
                "Loki investigation completed. EventId: {EventId}, HasLogs: {HasLogs}, TotalLogs: {TotalLogs}",
                workItem.EventId,
                report.HasLogs,
                report.TotalLogs);

            if (!report.HasLogs)
            {
                await HandleNoLogsAsync(
                    workItem,
                    leaseId,
                    maxRetries,
                    cancellationToken);

                return;
            }

            var aiAnalysis =
                await failureAnalysisService.AnalyzeAsync(
                    report,
                    cancellationToken);

            var record = new FailureInvestigationRecord
            {
                EventId = workItem.EventId,
                EventVersion = workItem.EventVersion,
                OccurredAtUtc = workItem.OccurredAtUtc,
                ServiceName = workItem.ServiceName,
                Environment = workItem.Environment,
                IntegrationType = workItem.IntegrationType,
                TransactionId = workItem.TransactionId,
                TraceId = workItem.TraceId,
                ErrorCode = workItem.ErrorCode,
                ErrorMessage = workItem.ErrorMessage,
                Investigation = report,
                AiAnalysis = aiAnalysis
            };

            await investigationRepository.SaveAsync(
                record,
                leaseId,
                cancellationToken);

            logger.LogInformation(
                "Investigation completed successfully. EventId: {EventId}, TransactionId: {TransactionId}, LeaseId: {LeaseId}",
                workItem.EventId,
                workItem.TransactionId,
                leaseId);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Investigation interrupted. EventId: {EventId}, LeaseId: {LeaseId}. Recovery will be possible after lease expiry.",
                workItem.EventId,
                leaseId);

            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Investigation processing failed. EventId: {EventId}, TransactionId: {TransactionId}, LeaseId: {LeaseId}",
                workItem.EventId,
                workItem.TransactionId,
                leaseId);

            await HandleProcessingErrorAsync(
                workItem,
                leaseId,
                ex,
                maxRetries,
                cancellationToken);
        }
    }

    private async Task HandleNoLogsAsync(
        FailureEventWorkItem workItem,
        Guid leaseId,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var repository =
            scope.ServiceProvider
                .GetRequiredService<IFailureEventRepository>();

        const string message =
            "No matching logs were found in Loki.";

        if (workItem.RetryCount >= maxRetries)
        {
            await repository.MarkNoLogsFoundAsync(
                workItem.EventId,
                leaseId,
                message,
                cancellationToken);

            logger.LogWarning(
                "Maximum retries exhausted. No logs found. EventId: {EventId}, LeaseId: {LeaseId}",
                workItem.EventId,
                leaseId);

            return;
        }

        var delay = GetRetryDelay(workItem.RetryCount);
        var nextRetryAt = DateTimeOffset.UtcNow.Add(delay);

        await repository.ScheduleRetryAsync(
            workItem.EventId,
            leaseId,
            nextRetryAt,
            message,
            cancellationToken);

        logger.LogInformation(
            "No logs found. Retry {RetryNumber} scheduled at {NextRetryAtUtc}. EventId: {EventId}",
            workItem.RetryCount + 1,
            nextRetryAt,
            workItem.EventId);
    }

    private async Task HandleProcessingErrorAsync(
        FailureEventWorkItem workItem,
        Guid leaseId,
        Exception exception,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var repository =
            scope.ServiceProvider
                .GetRequiredService<IFailureEventRepository>();

        if (workItem.RetryCount >= maxRetries)
        {
            await repository.MarkFailedAsync(
                workItem.EventId,
                leaseId,
                exception.Message,
                cancellationToken);

            logger.LogError(
                "Maximum retries exhausted after processing errors. EventId: {EventId}, LeaseId: {LeaseId}",
                workItem.EventId,
                leaseId);

            return;
        }

        var delay = GetRetryDelay(workItem.RetryCount);
        var nextRetryAt = DateTimeOffset.UtcNow.Add(delay);

        await repository.ScheduleRetryAsync(
            workItem.EventId,
            leaseId,
            nextRetryAt,
            exception.Message,
            cancellationToken);

        logger.LogWarning(
            "Processing retry {RetryNumber} scheduled at {NextRetryAtUtc}. EventId: {EventId}",
            workItem.RetryCount + 1,
            nextRetryAt,
            workItem.EventId);
    }

    private static TimeSpan GetRetryDelay(int retryCount)
    {
        var index = Math.Clamp(
            retryCount,
            0,
            RetryDelays.Length - 1);

        return RetryDelays[index];
    }

    private static DateTimeOffset Max(
        DateTimeOffset first,
        DateTimeOffset second)
    {
        return first >= second ? first : second;
    }

    private static void ValidateConfiguration(
        int pollingInterval,
        int batchSize,
        int lookbackHours,
        int postFailureBufferMinutes,
        int maxRetries,
        int processingLeaseMinutes)
    {
        if (pollingInterval <= 0 || pollingInterval > 300)
            throw new InvalidOperationException(
                "PollingIntervalSeconds must be between 1 and 300.");

        if (batchSize <= 0 || batchSize > 100)
            throw new InvalidOperationException(
                "BatchSize must be between 1 and 100.");

        if (lookbackHours <= 0 || lookbackHours > 168)
            throw new InvalidOperationException(
                "LookbackHours must be between 1 and 168.");

        if (postFailureBufferMinutes < 0 ||
            postFailureBufferMinutes > 1440)
            throw new InvalidOperationException(
                "PostFailureBufferMinutes must be between 0 and 1440.");

        if (maxRetries < 0 || maxRetries > 5)
            throw new InvalidOperationException(
                "MaxRetries must be between 0 and 5.");

        if (processingLeaseMinutes < 1 ||
            processingLeaseMinutes > 60)
            throw new InvalidOperationException(
                "ProcessingLeaseMinutes must be between 1 and 60.");
    }
}
