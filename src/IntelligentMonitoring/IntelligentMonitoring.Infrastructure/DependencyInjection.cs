using IntelligentMonitoring.Application.Abstractions.Logging;
using IntelligentMonitoring.Infrastructure.Logging.Loki;
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

        services.AddHttpClient<ILokiLogRepository, LokiLogRepository>(
            client =>
            {
                client.BaseAddress = new Uri(lokiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        return services;
    }
}