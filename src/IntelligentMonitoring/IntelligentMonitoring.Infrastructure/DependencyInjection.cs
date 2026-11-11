using IntelligentMonitoring.Application.Abstractions.Investigations;
using IntelligentMonitoring.Application.Abstractions.Logging;
using IntelligentMonitoring.Infrastructure.Logging.Loki;
using IntelligentMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IntelligentMonitoring.Infrastructure.AI.Ollama;

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
        var ollamaBaseUrl = configuration["Ollama:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Ollama:BaseUrl configuration is missing.");

        var ollamaTimeout = configuration.GetValue<int>(
            "Ollama:TimeoutSeconds",
            120);

        services.AddHttpClient<IFailureAnalysisService, OllamaFailureAnalysisService>(
            client =>
            {
                client.BaseAddress = new Uri(ollamaBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(ollamaTimeout);
            });

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

        services.AddScoped<
            IFailureEventRepository,
            FailureEventRepository>();

        return services;
    }
}