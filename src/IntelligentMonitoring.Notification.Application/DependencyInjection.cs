
using IntelligentMonitoring.Notification.Application.Abstractions;
using IntelligentMonitoring.Notification.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntelligentMonitoring.Notification.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTransient<
            INotificationDispatcher,
            NotificationDispatcher>();

        services.Configure<Configuration.NotificationOptions>(
            configuration.GetSection(
                Configuration.NotificationOptions.SectionName));

        services.AddScoped<NotificationOutboxProcessor>();

        return services;
    }
}
