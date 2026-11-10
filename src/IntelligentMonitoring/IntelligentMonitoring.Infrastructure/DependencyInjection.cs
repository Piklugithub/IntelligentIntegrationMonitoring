using IntelligentMonitoring.Application.Abstractions.Investigations;
using IntelligentMonitoring.Application.Abstractions.Logging;
using IntelligentMonitoring.Infrastructure.Logging.Loki;
using IntelligentMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IntelligentMonitoring.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var lokiBaseUrl = configuration["Loki:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Loki:BaseUrl configuration is missing.");

        var connectionString =
            configuration.GetConnectionString("MonitoringDb")
            ?? throw new InvalidOperationException(
                "MonitoringDb connection string is missing.");

        // Loki
        services.AddHttpClient<ILokiLogRepository, LokiLogRepository>(
            client =>
            {
                client.BaseAddress = new Uri(lokiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        // SQL Server
        services.AddDbContext<MonitoringDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Persistence repository
        services.AddScoped<
            IFailureInvestigationRepository,
            FailureInvestigationRepository>();

        return services;
    }
}