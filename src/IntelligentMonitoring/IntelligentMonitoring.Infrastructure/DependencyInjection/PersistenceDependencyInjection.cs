
using IntelligentMonitoring.Application.Abstractions.Investigations;
using IntelligentMonitoring.Infrastructure.Persistence;
using IntelligentMonitoring.Notification.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntelligentMonitoring.Infrastructure;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddMonitoringPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("MonitoringDb")
            ?? throw new InvalidOperationException(
                "MonitoringDb connection string is missing.");

        services.AddDbContext<MonitoringDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<
            IFailureInvestigationRepository,
            FailureInvestigationRepository>();

        services.AddScoped<
            IFailureEventRepository,
            FailureEventRepository>();

        services.AddScoped<
            INotificationDeliveryRepository,
            NotificationDeliveryRepository>();

        services.AddScoped<
            INotificationOutboxRepository,
            NotificationOutboxRepository>();

        return services;
    }
}
