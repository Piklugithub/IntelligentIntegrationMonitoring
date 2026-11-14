
using IntelligentMonitoring.Notification.Application.Configuration;
using IntelligentMonitoring.Notification.Application.Services;
using Microsoft.Extensions.Options;

namespace IntelligentMonitoring.Notification.Worker.Services;

public sealed class NotificationOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationOptions> options,
    ILogger<NotificationOutboxWorker> logger)
    : BackgroundService
{
    private readonly NotificationOptions _options = options.Value;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation(
                "Notification processing is disabled. Outbox worker will not start.");

            return;
        }

        logger.LogInformation("Notification outbox worker started.");

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(_options.PollingIntervalSeconds));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();

                    var processor = scope.ServiceProvider
                        .GetRequiredService<NotificationOutboxProcessor>();

                    await processor.ProcessBatchAsync(stoppingToken);
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
                        "An error occurred while processing the notification outbox batch.");
                }

                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }

        logger.LogInformation("Notification outbox worker stopped.");
    }
}
