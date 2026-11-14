
using IntelligentMonitoring.Notification.Application.Abstractions;
using IntelligentMonitoring.Notification.Application.Models;
using IntelligentMonitoring.Notification.Infrastructure.Configuration;
using IntelligentMonitoring.Notification.Infrastructure.Providers.Slack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntelligentMonitoring.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Slack configuration
        services.Configure<SlackOptions>(
            configuration.GetSection(SlackOptions.SectionName));

        // Slack HTTP client
        services.AddHttpClient<SlackNotificationProvider>(client =>
        {
            var baseUrl = configuration["Slack:ApiBaseUrl"]
                ?? "https://slack.com/api/";

            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register Slack as a notification provider.
        services.AddTransient<INotificationProvider>(
            serviceProvider =>
                serviceProvider.GetRequiredService<SlackNotificationProvider>());

        return services;
    }
}
