
using IntelligentMonitoring.Notification.Application.Models;

namespace IntelligentMonitoring.Notification.Application.Abstractions;

public interface INotificationProvider
{
    NotificationChannel Channel { get; }

    Task<NotificationDispatchResult> SendAsync(
        NotificationDispatchRequest request,
        CancellationToken cancellationToken = default);
}
