
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
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
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
                    {
                        consumer.Commit(result);
                    }

                    continue;
                }

                // 2. Deserialize the failure event.
                var failureEvent =
                    JsonSerializer.Deserialize<IntegrationFailureEvent>(
                        result.Message.Value,
                        _jsonOptions);

                if (failureEvent is null)
                {
                    logger.LogWarning(
                        "Deserialized failure event is null. Partition: {Partition}, Offset: {Offset}",
                        result.Partition,
                        result.Offset);

                    consumer.Commit(result);
                    continue;
                }

                // 3. Validate the event.
                ValidateFailureEvent(failureEvent);

                logger.LogInformation(
                    """
                    Failure event received.
                    EventId: {EventId}
                    TransactionId: {TransactionId}
                    TraceId: {TraceId}
                    ServiceName: {ServiceName}
                    ErrorCode: {ErrorCode}
                    """,
                    failureEvent.EventId,
                    failureEvent.TransactionId,
                    failureEvent.TraceId,
                    failureEvent.ServiceName,
                    failureEvent.ErrorCode);

                // 4. Create an asynchronous DI scope.
                await using var scope =
                    scopeFactory.CreateAsyncScope();

                var eventRepository =
                    scope.ServiceProvider
                        .GetRequiredService<IFailureEventRepository>();

                // 5. Persist the event before committing Kafka offset.
                var inserted =
                    await eventRepository.SaveIfNotExistsAsync(
                        failureEvent,
                        stoppingToken);

                if (inserted)
                {
                    logger.LogInformation(
                        "Failure event successfully persisted with Pending status. EventId: {EventId}, TransactionId: {TransactionId}",
                        failureEvent.EventId,
                        failureEvent.TransactionId);
                }
                else
                {
                    logger.LogInformation(
                        "Duplicate failure event detected. Existing EventId will be retained. EventId: {EventId}",
                        failureEvent.EventId);
                }

                // 6. Commit only after the event is durably persisted
                // or its existing EventId has been confirmed.
                consumer.Commit(result);

                logger.LogInformation(
                    "Kafka offset committed. EventId: {EventId}, Partition: {Partition}, Offset: {Offset}",
                    failureEvent.EventId,
                    result.Partition,
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

                // Development policy: skip malformed messages.
                // Production: publish to a dead-letter topic first.
                if (result is not null)
                {
                    consumer.Commit(result);
                }
            }
            catch (ConsumeException ex)
            {
                logger.LogError(
                    ex,
                    "Kafka consumption error.");
            }
            catch (Exception ex)
            {
                logger.LogCritical(
                    ex,
                    "Failure event processing failed. Partition: {Partition}, Offset: {Offset}. Message will not be committed.",
                    result?.Partition,
                    result?.Offset);

                // Preserve the message for redelivery.
                throw;
            }
        }

        logger.LogInformation(
            "Kafka consumer is stopping.");
    }

    private static void ValidateFailureEvent(
        IntegrationFailureEvent failureEvent)
    {
        if (failureEvent.EventId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Failure event does not contain a valid EventId.");
        }

        if (string.IsNullOrWhiteSpace(failureEvent.TransactionId))
        {
            throw new InvalidOperationException(
                "Failure event does not contain a TransactionId.");
        }

        if (failureEvent.OccurredAtUtc == default)
        {
            throw new InvalidOperationException(
                "Failure event does not contain a valid OccurredAtUtc.");
        }
    }
}
