
using IntelligentMonitoring.Notification.Application.Abstractions;
using IntelligentMonitoring.Notification.Application.Models;
using IntelligentMonitoring.Notification.Application.Services;
using IntelligentMonitoring.Notification.Contracts.Events;
using IntelligentMonitoring.Tests.Notification.Fakes;
using NUnit.Framework;

namespace IntelligentMonitoring.Tests.Notification;

[TestFixture]
public class NotificationDispatcherTests
{
    private static NotificationDispatchRequest CreateRequest(
        NotificationChannel channel,
        string destination = "test-destination")
    {
        return new NotificationDispatchRequest
        {
            Event = new InvestigationCompletedEvent
            {
                EventId = Guid.NewGuid(),
                TransactionId = "TXN-10001",
                TraceId = "TRACE-10001",
                ServiceName = "WarehouseEventsApi",
                Environment = "QA",
                IntegrationType = "Kafka",
                InvestigationStatus = "Completed",
                ErrorCode = "INT-500",
                ErrorMessage = "Integration failed",
                AiSummary = "Investigation completed.",
                AiProbableRootCause = "Downstream service unavailable.",
                AiBusinessImpact = "Shipment processing delayed.",
                OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                CompletedAtUtc = DateTimeOffset.UtcNow
            },
            Channel = channel,
            Destination = destination
        };
    }

    [Test]
    public async Task DispatchAsync_WhenSlackProviderSucceeds_ReturnsSuccessfulResult()
    {
        // Arrange
        var provider = new FakeNotificationProvider(NotificationChannel.Slack);

        var dispatcher = new NotificationDispatcher([provider]);

        var request = CreateRequest(NotificationChannel.Slack);

        // Act
        var result = await dispatcher.DispatchAsync(request);

        // Assert
        Assert.That(result.IsSuccessful, Is.True);
        Assert.That(provider.CallCount, Is.EqualTo(1));
        Assert.That(provider.LastRequest, Is.SameAs(request));
    }

    [Test]
    public async Task DispatchAsync_WhenEmailProviderSucceeds_ReturnsSuccessfulResult()
    {
        // Arrange
        var provider = new FakeNotificationProvider(NotificationChannel.Email);

        var dispatcher = new NotificationDispatcher([provider]);

        var request = CreateRequest(NotificationChannel.Email);

        // Act
        var result = await dispatcher.DispatchAsync(request);

        // Assert
        Assert.That(result.IsSuccessful, Is.True);
        Assert.That(provider.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task DispatchAsync_WhenProviderIsNotRegistered_ReturnsFailure()
    {
        // Arrange
        var dispatcher = new NotificationDispatcher(
            Array.Empty<INotificationProvider>());

        var request = CreateRequest(NotificationChannel.Slack);

        // Act
        var result = await dispatcher.DispatchAsync(request);

        // Assert
        Assert.That(result.IsSuccessful, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("No provider is registered"));
    }

    [Test]
    public void Constructor_WhenDuplicateProvidersRegistered_ThrowsException()
    {
        // Arrange
        var providers = new[]
        {
            new FakeNotificationProvider(NotificationChannel.Slack),
            new FakeNotificationProvider(NotificationChannel.Slack)
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(
            () => new NotificationDispatcher(providers));
    }

    [Test]
    public async Task DispatchAsync_WhenProviderThrowsException_ReturnsFailure()
    {
        // Arrange
        var provider = new FakeNotificationProvider(NotificationChannel.Slack)
        {
            ShouldThrowException = true
        };

        var dispatcher = new NotificationDispatcher([provider]);

        var request = CreateRequest(NotificationChannel.Slack);

        // Act
        var result = await dispatcher.DispatchAsync(request);

        // Assert
        Assert.That(result.IsSuccessful, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("Simulated provider failure"));
    }

    [Test]
    public void DispatchAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var provider = new FakeNotificationProvider(NotificationChannel.Slack);
        var dispatcher = new NotificationDispatcher([provider]);

        var request = CreateRequest(NotificationChannel.Slack);

        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await dispatcher.DispatchAsync(
                request,
                cancellationTokenSource.Token));
    }

    [Test]
    public async Task DispatchAsync_WhenDestinationIsEmpty_ReturnsFailure()
    {
        // Arrange
        var provider = new FakeNotificationProvider(NotificationChannel.Slack);
        var dispatcher = new NotificationDispatcher([provider]);

        var request = CreateRequest(NotificationChannel.Slack, " ");

        // Act
        var result = await dispatcher.DispatchAsync(request);

        // Assert
        Assert.That(result.IsSuccessful, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("destination"));
        Assert.That(provider.CallCount, Is.EqualTo(0));
    }
}
