
namespace IntelligentMonitoring.Notification.Application.Configuration;

public sealed class NotificationChannelOptions
{
    public string Channel { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public List<string> Destinations { get; set; } = [];
}
