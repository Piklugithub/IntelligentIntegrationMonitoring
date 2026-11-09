
using System.Text.Json;
using Confluent.Kafka;
using IntelligentMonitoring.Contracts.Events;
using IntelligentMonitoring.Application.Abstractions.Investigations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IntelligentMonitoring.Worker.Messaging.Kafka;

public sealed class KafkaFailureConsumer(
    IConfiguration configuration,
    ILogger<KafkaFailureConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : BackgroundService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var bootstrapServers =
            configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException(
                "Kafka bootstrap servers are not configured.");

        var topic =
            configuration["Kafka:FailureTopic"]
            ?? throw new InvalidOperationException(
                "Kafka failure topic is not configured.");

        var consumerGroup =
            configuration["Kafka:ConsumerGroup"]
            ?? throw new InvalidOperationException(
                "Kafka consumer group is not configured.");

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = consumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer =
            new ConsumerBuilder<string, string>(consumerConfig)
                .Build();

        consumer.Subscribe(topic);

        logger.LogInformation(
            "Kafka consumer started. Topic: {Topic}, Group: {GroupId}",
            topic,
            consumerGroup);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;

            try
            {
                // 1. Consume the next Kafka message.
                result = consumer.Consume(stoppingToken);

                if (result?.Message?.Value is null ||
                    string.IsNullOrWhiteSpace(result.Message.Value))
                {
                    logger.LogWarning(
                        "Received an empty Kafka message. Partition: {Partition}, Offset: {Offset}",
                        result?.Partition,
                        result?.Offset);

                    if (result is not null)
                        consumer.Commit(result);

                    continue;
                }

                // 2. Deserialize the failure event.
                var failureEvent =
                    JsonSerializer.Deserialize<IntegrationFailureEvent>(
                        result.Message.Value,
                        _jsonOptions);

                // 3. Validate the deserialized event.
                if (failureEvent is null)
                {
                    logger.LogWarning(
                        "Received an empty failure event. Partition: {Partition}, Offset: {Offset}",
                        result.Partition,
                        result.Offset);

                    consumer.Commit(result);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(failureEvent.TransactionId))
                {
                    throw new InvalidOperationException(
                        "Failure event does not contain a TransactionId.");
                }

                logger.LogError(
                    "Failure event received. EventId: {EventId}, TransactionId: {TransactionId}, TraceId: {TraceId}, Service: {ServiceName}, ErrorCode: {ErrorCode}, Message: {ErrorMessage}",
                    failureEvent.EventId,
                    failureEvent.TransactionId,
                    failureEvent.TraceId,
                    failureEvent.ServiceName,
                    failureEvent.ErrorCode,
                    failureEvent.ErrorMessage);

                // 4. Create a dependency injection scope.
                using var scope = scopeFactory.CreateScope();

                var investigationService =
                    scope.ServiceProvider
                        .GetRequiredService<IFailureInvestigationService>();

                // 5. Define the investigation time window.
                var startTime =
                    failureEvent.OccurredAtUtc.AddMinutes(-15);

                var endTime = Max(
                    DateTimeOffset.UtcNow,
                    failureEvent.OccurredAtUtc.AddMinutes(15));

                // 6. Investigate the transaction.
                var report =
                    await investigationService.InvestigateTransactionAsync(
                        failureEvent.TransactionId,
                        startTime,
                        endTime,
                        stoppingToken);

                // 7. Log the investigation summary.
                logger.LogInformation(
                    """
                    Transaction investigation completed.
                    EventId: {EventId}
                    TransactionId: {TransactionId}
                    Status: {Status}
                    TotalLogs: {TotalLogs}
                    InformationCount: {InformationCount}
                    WarningCount: {WarningCount}
                    ErrorCount: {ErrorCount}
                    FailureService: {FailureService}
                    ErrorCode: {ErrorCode}
                    ServicesInvolved: {ServicesInvolved}
                    """,
                    failureEvent.EventId,
                    report.TransactionId,
                    report.Status,
                    report.TotalLogs,
                    report.InformationCount,
                    report.WarningCount,
                    report.ErrorCount,
                    report.FailureServiceName,
                    report.ErrorCode,
                    string.Join(", ", report.ServicesInvolved));

                // 8. Commit only after successful investigation.
                consumer.Commit(result);

                logger.LogInformation(
                    "Failure event successfully investigated and committed. TransactionId: {TransactionId}, Offset: {Offset}",
                    failureEvent.TransactionId,
                    result.Offset);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation(
                    "Kafka consumer shutdown requested.");

                break;
            }
            catch (JsonException ex)
            {
                logger.LogError(
                    ex,
                    "Invalid JSON received from Kafka. Partition: {Partition}, Offset: {Offset}",
                    result?.Partition,
                    result?.Offset);

                // Poison message policy: skip malformed JSON.
                if (result is not null)
                    consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(
                    ex,
                    "Kafka consumption error.");
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failure investigation failed. Partition: {Partition}, Offset: {Offset}. Message will not be committed.",
                    result?.Partition,
                    result?.Offset);

                // Stop processing rather than accidentally committing
                // a later offset and losing this failed message.
                throw;
            }
        }

        logger.LogInformation("Kafka consumer is stopping.");
    }

    private static DateTimeOffset Max(
        DateTimeOffset first,
        DateTimeOffset second)
    {
        return first >= second ? first : second;
    }
}
