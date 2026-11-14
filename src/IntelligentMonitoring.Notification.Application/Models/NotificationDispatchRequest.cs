
using IntelligentMonitoring.Notification.Contracts.Events;

namespace IntelligentMonitoring.Notification.Application.Models;

public sealed record NotificationDispatchRequest
{
    public required InvestigationCompletedEvent Event { get; init; }

    public required NotificationChannel Channel { get; init; }

    // Destination identifier, such as a Slack channel ID
    // or an email address.
    public required string Destination { get; init; }

    public string IdempotencyKey =>
        $"{Event.IdempotencyKey}:{Channel}:{Destination}";
}
