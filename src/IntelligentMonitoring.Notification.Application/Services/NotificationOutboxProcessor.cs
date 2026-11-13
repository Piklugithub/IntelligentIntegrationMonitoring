
using System.Text.Json;
using IntelligentMonitoring.Notification.Application.Abstractions.Persistence;
using IntelligentMonitoring.Notification.Application.Configuration;
using IntelligentMonitoring.Notification.Application.Models;
using IntelligentMonitoring.Notification.Application.Models.Outbox;
using IntelligentMonitoring.Notification.Contracts.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelligentMonitoring.Notification.Application.Services;

public sealed class NotificationOutboxProcessor(
    INotificationOutboxRepository outboxRepository,
    INotificationDeliveryRepository deliveryRepository,
    IOptions<NotificationOptions> options,
    ILogger<NotificationOutboxProcessor> logger)
{
    private readonly NotificationOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task ProcessBatchAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var workItems = await outboxRepository.GetDueEventsAsync(
            _options.BatchSize,
            now,
            cancellationToken);

        foreach (var workItem in workItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var claimTime = DateTimeOffset.UtcNow;

            var leaseId = await outboxRepository.TryClaimAsync(
                workItem.Id,
                claimTime.AddMinutes(_options.ProcessingLeaseMinutes),
                claimTime,
                cancellationToken);

            if (leaseId is null)
                continue;

            var currentAttempt = workItem.AttemptCount + 1;

            try
            {
                var completedEvent =
                    JsonSerializer.Deserialize<InvestigationCompletedEvent>(
                        workItem.PayloadJson,
                        JsonOptions);

                if (completedEvent is null)
                {
                    throw new InvalidOperationException(
                        $"Unable to deserialize outbox event {workItem.Id}.");
                }

                var enabledChannels = _options.Channels
                    .Where(x => x.Enabled)
                    .ToList();

                if (enabledChannels.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Notifications are enabled, but no notification channels are configured.");
                }

                var deliveryCount = 0;

                foreach (var channelOptions in enabledChannels)
                {
                    if (!Enum.TryParse<NotificationChannel>(
                            channelOptions.Channel,
                            true,
                            out var channel))
                    {
                        throw new InvalidOperationException(
                            $"Unsupported notification channel: {channelOptions.Channel}");
                    }

                    var destinations = channelOptions.Destinations
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (destinations.Count == 0)
                    {
                        logger.LogWarning(
                            "No destinations configured for notification channel {Channel}.",
                            channel);
                        continue;
                    }

                    foreach (var destination in destinations)
                    {
                        var request = new NotificationDispatchRequest
                        {
                            Event = completedEvent,
                            Channel = channel,
                            Destination = destination
                        };

                        // EnqueueAsync is idempotent. If a previous attempt
                        // already inserted this delivery, it will not create
                        // a duplicate delivery record.
                        await deliveryRepository.EnqueueAsync(
                            request,
                            cancellationToken);

                        deliveryCount++;
                    }
                }

                if (deliveryCount == 0)
                {
                    throw new InvalidOperationException(
                        "No notification deliveries were created. " +
                        "Check enabled channels and configured destinations.");
                }

                await outboxRepository.MarkProcessedAsync(
                    workItem.Id,
                    leaseId.Value,
                    DateTimeOffset.UtcNow,
                    cancellationToken);

                logger.LogInformation(
                    "Outbox event {OutboxId} processed. Created or confirmed {DeliveryCount} deliveries.",
                    workItem.Id,
                    deliveryCount);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Leave the lease to expire. Another worker can recover it.
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to process outbox event {OutboxId}. Attempt {Attempt}.",
                    workItem.Id,
                    currentAttempt);

                var attemptedAt = DateTimeOffset.UtcNow;
                var error = exception.Message.Length > 4000
                    ? exception.Message[..4000]
                    : exception.Message;

                if (currentAttempt > _options.MaxRetries)
                {
                    await outboxRepository.MarkFailedAsync(
                        workItem.Id,
                        leaseId.Value,
                        error,
                        attemptedAt,
                        cancellationToken);

                    logger.LogError(
                        "Outbox event {OutboxId} permanently failed after {Attempt} attempts.",
                        workItem.Id,
                        currentAttempt);
                }
                else
                {
                    var delayIndex = Math.Min(
                        currentAttempt - 1,
                        _options.RetryDelaysSeconds.Count - 1);

                    var delaySeconds =
                        _options.RetryDelaysSeconds[delayIndex];

                    await outboxRepository.ScheduleRetryAsync(
                        workItem.Id,
                        leaseId.Value,
                        attemptedAt.AddSeconds(delaySeconds),
                        error,
                        attemptedAt,
                        cancellationToken);

                    logger.LogWarning(
                        "Outbox event {OutboxId} scheduled for retry in {DelaySeconds} seconds.",
                        workItem.Id,
                        delaySeconds);
                }
            }
        }
    }
}
