using IntelligentMonitoring.Application.Abstractions.Investigations;
using IntelligentMonitoring.Application.Abstractions.Logging;
using IntelligentMonitoring.Application.Services.Investigations;
using IntelligentMonitoring.Application.Services.Logging;
using IntelligentMonitoring.Infrastructure;
using IntelligentMonitoring.Worker.Investigations;
using IntelligentMonitoring.Worker.Messaging.Kafka;

var builder = Host.CreateApplicationBuilder(args);

// Infrastructure dependencies:
// Loki, SQL Server, persistence repository and Ollama.
builder.Services.AddInfrastructure(builder.Configuration);

// Application services.
builder.Services.AddScoped<
    ILogSearchService,
    LogSearchService>();

builder.Services.AddScoped<
    IFailureInvestigationService,
    FailureInvestigationService>();

// Background workers.
builder.Services.AddHostedService<KafkaFailureConsumer>();

builder.Services.AddHostedService<FailureInvestigationWorker>();

var host = builder.Build();

await host.RunAsync();