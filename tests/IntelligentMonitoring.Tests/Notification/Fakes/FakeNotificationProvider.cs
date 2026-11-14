
using IntelligentMonitoring.Notification.Application.Abstractions;
using IntelligentMonitoring.Notification.Application.Models;

namespace IntelligentMonitoring.Tests.Notification.Fakes;

public sealed class FakeNotificationProvider : INotificationProvider
{
    public NotificationChannel Channel { get; }

    public bool ShouldThrowException { get; set; }

    public bool IsSuccessful { get; set; } = true;

    public int CallCount { get; private set; }

    public NotificationDispatchRequest? LastRequest { get; private set; }

    public FakeNotificationProvider(NotificationChannel channel)
    {
        Channel = channel;
    }

    public Task<NotificationDispatchResult> SendAsync(
        NotificationDispatchRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CallCount++;
        LastRequest = request;

        if (ShouldThrowException)
        {
            throw new InvalidOperationException("Simulated provider failure.");
        }

        return Task.FromResult(new NotificationDispatchResult
        {
            IsSuccessful = IsSuccessful,
            ExternalMessageId = IsSuccessful ? "FAKE-MESSAGE-001" : null,
            ErrorMessage = IsSuccessful ? null : "Simulated delivery failure.",
            CompletedAtUtc = DateTimeOffset.UtcNow
        });
    }
}
