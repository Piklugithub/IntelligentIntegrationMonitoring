
namespace IntelligentMonitoring.Notification.Infrastructure.Configuration;

public sealed class SlackOptions
{
    public const string SectionName = "Slack";

    public string BotToken { get; set; } = string.Empty;

    public string ApiBaseUrl { get; set; } =
        "https://slack.com/api/";
}
