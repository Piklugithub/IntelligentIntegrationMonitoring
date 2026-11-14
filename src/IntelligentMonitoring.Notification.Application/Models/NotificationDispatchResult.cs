
namespace IntelligentMonitoring.Notification.Application.Models;

public sealed record NotificationDispatchResult
{
    public required bool IsSuccessful { get; init; }

    public string? ExternalMessageId { get; init; }

    public string? ErrorMessage { get; init; }

    public DateTimeOffset CompletedAtUtc { get; init; }
}
