
using IntelligentMonitoring.Infrastructure;
using IntelligentMonitoring.Notification.Application;
using IntelligentMonitoring.Notification.Infrastructure;
using IntelligentMonitoring.Notification.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

// Existing SQL Server database and repositories.
builder.Services.AddMonitoringPersistence(
    builder.Configuration);

// Notification application services.
builder.Services.AddNotificationApplication(
    builder.Configuration);

// Slack and other notification providers.
builder.Services.AddNotificationInfrastructure(
    builder.Configuration);

// Background notification processing.
builder.Services.AddHostedService<NotificationWorker>();
builder.Services.AddHostedService<NotificationOutboxWorker>();

var host = builder.Build();

host.Run();
