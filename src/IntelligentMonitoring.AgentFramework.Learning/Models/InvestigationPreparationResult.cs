
namespace IntelligentMonitoring.AgentFramework.Learning.Models;

public sealed record InvestigationPreparationResult
{
    public Guid EventId { get; init; }

    public string TransactionId { get; init; } = string.Empty;

    public string TraceId { get; init; } = string.Empty;

    public string ServiceName { get; init; } = string.Empty;

    public string Environment { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
