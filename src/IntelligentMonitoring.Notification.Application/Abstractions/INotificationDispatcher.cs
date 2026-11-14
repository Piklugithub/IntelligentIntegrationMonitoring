
using IntelligentMonitoring.Notification.Application.Models;

namespace IntelligentMonitoring.Notification.Application.Abstractions;

public interface INotificationDispatcher
{
    Task<NotificationDispatchResult> DispatchAsync(
        NotificationDispatchRequest request,
        CancellationToken cancellationToken = default);
}
