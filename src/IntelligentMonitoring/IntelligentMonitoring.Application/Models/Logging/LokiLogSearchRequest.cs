
namespace IntelligentMonitoring.Application.Models.Logging;

public sealed class LokiLogSearchRequest
{
    public string? TransactionId { get; set; }

    public string? TraceId { get; set; }

    public string? ServiceName { get; set; }

    public string? Level { get; set; }

    public string? IntegrationType { get; set; }

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset EndTime { get; set; }

    public int Limit { get; set; } = 100;
}
