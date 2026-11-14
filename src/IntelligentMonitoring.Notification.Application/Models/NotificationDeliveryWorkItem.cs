
using IntelligentMonitoring.Notification.Contracts.Events;

namespace IntelligentMonitoring.Notification.Application.Models;

public sealed record NotificationDeliveryWorkItem
{
    public Guid Id { get; init; }

    public Guid EventId { get; init; }

    public required InvestigationCompletedEvent Event { get; init; }

    public required NotificationChannel Channel { get; init; }

    public required string Destination { get; init; }

    public int AttemptCount { get; init; }
}
