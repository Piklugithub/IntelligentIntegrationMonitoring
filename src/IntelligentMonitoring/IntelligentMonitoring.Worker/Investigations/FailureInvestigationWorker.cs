
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

        ValidateConfiguration(
            pollingInterval,
            batchSize,
            lookbackHours,
            postFailureBufferMinutes,
            maxRetries);

        logger.LogInformation(
            """
            Failure investigation worker started.
            PollingIntervalSeconds: {PollingInterval}
            BatchSize: {BatchSize}
            LookbackHours: {LookbackHours}
            PostFailureBufferMinutes: {BufferMinutes}
            MaxRetries: {MaxRetries}
            """,
            pollingInterval,
            batchSize,
            lookbackHours,
            postFailureBufferMinutes,
            maxRetries);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(pollingInterval));

        // Run immediately at startup.
        do
        {
            try
            {
                await ProcessDueEventsAsync(
                    batchSize,
                    lookbackHours,
                    postFailureBufferMinutes,
                    maxRetries,
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

        logger.LogInformation(
            "Failure investigation worker stopped.");
    }

    private async Task ProcessDueEventsAsync(
        int batchSize,
        int lookbackHours,
        int postFailureBufferMinutes,
        int maxRetries,
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

            // Only one worker execution can claim an eligible event.
            var claimed = await eventRepository.MarkProcessingAsync(
                workItem.EventId,
                cancellationToken);

            if (!claimed)
            {
                logger.LogDebug(
                    "Event was already claimed or is no longer due. EventId: {EventId}",
                    workItem.EventId);

                continue;
            }

            await ProcessSingleEventAsync(
                workItem,
                lookbackHours,
                postFailureBufferMinutes,
                maxRetries,
                cancellationToken);
        }
    }

    private async Task ProcessSingleEventAsync(
        FailureEventWorkItem workItem,
        int lookbackHours,
        int postFailureBufferMinutes,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Starting investigation. EventId: {EventId}, TransactionId: {TransactionId}, RetryCount: {RetryCount}",
            workItem.EventId,
            workItem.TransactionId,
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

            // Logs may arrive after the Kafka event.
            if (!report.HasLogs)
            {
                await HandleNoLogsAsync(
                    workItem,
                    maxRetries,
                    cancellationToken);

                return;
            }

            // Analyze only when evidence is available.
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
                cancellationToken);

            logger.LogInformation(
                "Investigation completed successfully. EventId: {EventId}, TransactionId: {TransactionId}",
                workItem.EventId,
                workItem.TransactionId);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Do not convert application shutdown into a failed investigation.
            // Recovery of abandoned Processing events will be implemented
            // through a processing lease.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Investigation processing failed. EventId: {EventId}, TransactionId: {TransactionId}",
                workItem.EventId,
                workItem.TransactionId);

            await HandleProcessingErrorAsync(
                workItem,
                ex,
                maxRetries,
                cancellationToken);
        }
    }

    private async Task HandleNoLogsAsync(
        FailureEventWorkItem workItem,
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
                message,
                cancellationToken);

            logger.LogWarning(
                "Maximum retries exhausted. No logs found. EventId: {EventId}",
                workItem.EventId);

            return;
        }

        var delay = GetRetryDelay(workItem.RetryCount);
        var nextRetryAt = DateTimeOffset.UtcNow.Add(delay);

        await repository.ScheduleRetryAsync(
            workItem.EventId,
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
                exception.Message,
                cancellationToken);

            logger.LogError(
                "Maximum retries exhausted after processing errors. EventId: {EventId}",
                workItem.EventId);

            return;
        }

        var delay = GetRetryDelay(workItem.RetryCount);
        var nextRetryAt = DateTimeOffset.UtcNow.Add(delay);

        await repository.ScheduleRetryAsync(
            workItem.EventId,
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
        int maxRetries)
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
    }
}
