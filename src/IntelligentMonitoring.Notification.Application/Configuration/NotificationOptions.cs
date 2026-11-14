
namespace IntelligentMonitoring.Notification.Application.Configuration;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public bool Enabled { get; set; } = true;

    public int PollingIntervalSeconds { get; set; } = 10;

    public int BatchSize { get; set; } = 10;

    public int ProcessingLeaseMinutes { get; set; } = 5;

    // Number of retries after the initial attempt.
    public int MaxRetries { get; set; } = 5;

    public List<int> RetryDelaysSeconds { get; set; } =
        [30, 60, 120, 300, 600];

    public List<NotificationChannelOptions> Channels { get; set; } = [];
}
