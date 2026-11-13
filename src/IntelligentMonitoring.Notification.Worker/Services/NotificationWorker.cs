
using IntelligentMonitoring.Notification.Application.Abstractions;
using IntelligentMonitoring.Notification.Application.Abstractions.Persistence;
using IntelligentMonitoring.Notification.Application.Configuration;
using IntelligentMonitoring.Notification.Application.Models;
using Microsoft.Extensions.Options;

namespace IntelligentMonitoring.Notification.Worker.Services;

public sealed class NotificationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationOptions> options,
    ILogger<NotificationWorker> logger)
    : BackgroundService
{
    private readonly NotificationOptions _options = options.Value;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation(
                "Notification processing is disabled.");

            return;
        }

        ValidateOptions();

        logger.LogInformation(
            "Notification Worker started. Polling interval: {Interval}s, Batch size: {BatchSize}",
            _options.PollingIntervalSeconds,
            _options.BatchSize);

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_options.PollingIntervalSeconds));

        // Process immediately on startup.
        do
        {
            try
            {
                await ProcessDueDeliveriesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Unexpected error while processing notification deliveries.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessDueDeliveriesAsync(
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<INotificationDeliveryRepository>();

        var now = DateTimeOffset.UtcNow;

        var deliveries = await repository.GetDueDeliveriesAsync(
            _options.BatchSize,
            now,
            cancellationToken);

        if (deliveries.Count == 0)
        {
            logger.LogDebug("No notification deliveries are due.");
            return;
        }

        logger.LogInformation(
            "Found {Count} notification deliveries to process.",
            deliveries.Count);

        foreach (var delivery in deliveries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ProcessSingleDeliveryAsync(
                delivery,
                cancellationToken);
        }
    }

    private async Task ProcessSingleDeliveryAsync(
        NotificationDeliveryWorkItem delivery,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<INotificationDeliveryRepository>();

        var leaseStart = DateTimeOffset.UtcNow;

        var leaseId = await repository.TryClaimAsync(
            delivery.Id,
            leaseStart.AddMinutes(_options.ProcessingLeaseMinutes),
            leaseStart,
            cancellationToken);

        if (leaseId is null)
        {
            logger.LogDebug(
                "Notification delivery {DeliveryId} was already claimed by another worker.",
                delivery.Id);

            return;
        }

        logger.LogInformation(
            "Claimed notification delivery {DeliveryId}. Channel: {Channel}, Attempt: {Attempt}",
            delivery.Id,
            delivery.Channel,
            delivery.AttemptCount + 1);

        var dispatcher = scope.ServiceProvider
            .GetRequiredService<INotificationDispatcher>();

        var request = new NotificationDispatchRequest
        {
            Event = delivery.Event,
            Channel = delivery.Channel,
            Destination = delivery.Destination
        };

        NotificationDispatchResult result;

        try
        {
            result = await dispatcher.DispatchAsync(
                request,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Do not release the lease on shutdown.
            // Another worker can reclaim it after expiry.
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unexpected dispatch error for delivery {DeliveryId}.",
                delivery.Id);

            await HandleFailureAsync(
                repository,
                delivery,
                leaseId.Value,
                exception.Message,
                cancellationToken);

            return;
        }

        if (result.IsSuccessful)
        {
            // Deliberately keep persistence outside the dispatch try/catch.
            // If the provider accepted the message but this DB update fails,
            // leave the lease to expire rather than immediately retrying.
            await repository.MarkSentAsync(
                delivery.Id,
                leaseId.Value,
                result.ExternalMessageId,
                DateTimeOffset.UtcNow,
                cancellationToken);

            logger.LogInformation(
                "Notification delivery {DeliveryId} sent successfully.",
                delivery.Id);

            return;
        }

        await HandleFailureAsync(
            repository,
            delivery,
            leaseId.Value,
            result.ErrorMessage ?? "Notification delivery failed.",
            cancellationToken);
    }

    private async Task HandleFailureAsync(
        INotificationDeliveryRepository repository,
        NotificationDeliveryWorkItem delivery,
        Guid leaseId,
        string error,
        CancellationToken cancellationToken)
    {
        // TryClaimAsync increments AttemptCount in the database.
        // The work item contains the count before the current claim.
        var currentAttempt = delivery.AttemptCount + 1;

        if (currentAttempt > _options.MaxRetries)
        {
            await repository.MarkFailedAsync(
                delivery.Id,
                leaseId,
                error,
                DateTimeOffset.UtcNow,
                cancellationToken);

            logger.LogError(
                "Notification delivery {DeliveryId} permanently failed after {Attempts} attempts.",
                delivery.Id,
                currentAttempt);

            return;
        }

        var delayIndex = Math.Min(
            currentAttempt - 1,
            _options.RetryDelaysSeconds.Count - 1);

        var delaySeconds = _options.RetryDelaysSeconds[delayIndex];

        var nextAttempt = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);

        await repository.ScheduleRetryAsync(
            delivery.Id,
            leaseId,
            nextAttempt,
            error,
            cancellationToken);

        logger.LogWarning(
            "Notification delivery {DeliveryId} failed. Retry scheduled at {NextAttempt}. Attempt: {Attempt}",
            delivery.Id,
            nextAttempt,
            currentAttempt);
    }

    private void ValidateOptions()
    {
        if (_options.PollingIntervalSeconds is < 1 or > 300)
            throw new InvalidOperationException(
                "Notifications:PollingIntervalSeconds must be between 1 and 300.");

        if (_options.BatchSize is < 1 or > 100)
            throw new InvalidOperationException(
                "Notifications:BatchSize must be between 1 and 100.");

        if (_options.ProcessingLeaseMinutes is < 1 or > 60)
            throw new InvalidOperationException(
                "Notifications:ProcessingLeaseMinutes must be between 1 and 60.");

        if (_options.MaxRetries is < 0 or > 10)
            throw new InvalidOperationException(
                "Notifications:MaxRetries must be between 0 and 10.");

        if (_options.RetryDelaysSeconds is null ||
            _options.RetryDelaysSeconds.Count == 0 ||
            _options.RetryDelaysSeconds.Any(x => x < 1))
        {
            throw new InvalidOperationException(
                "Notifications:RetryDelaysSeconds must contain positive values.");
        }
    }
}
