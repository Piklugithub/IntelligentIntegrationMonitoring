
using IntelligentMonitoring.Application.Abstractions.Investigations;
using IntelligentMonitoring.Application.Abstractions.Logging;
using IntelligentMonitoring.Infrastructure.AI.Ollama;
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

        var ollamaBaseUrl = configuration["Ollama:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Ollama:BaseUrl configuration is missing.");

        var ollamaTimeout = configuration.GetValue<int>(
            "Ollama:TimeoutSeconds",
            120);

        // SQL Server and repositories
        services.AddMonitoringPersistence(configuration);

        // Ollama AI
        services.AddHttpClient<
            IFailureAnalysisService,
            OllamaFailureAnalysisService>(
            client =>
            {
                client.BaseAddress = new Uri(ollamaBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(ollamaTimeout);
            });

        // Loki
        services.AddHttpClient<
            ILokiLogRepository,
            LokiLogRepository>(
            client =>
            {
                client.BaseAddress = new Uri(lokiBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });

        return services;
    }
}
