
namespace IntelligentMonitoring.AgentFramework.Learning.Models;

public sealed record InvestigationWorkflowContext
{
    public Guid EventId { get; init; }

    public string TransactionId { get; init; } = string.Empty;

    public string TraceId { get; init; } = string.Empty;

    public string ServiceName { get; init; } = string.Empty;

    public string Environment { get; init; } = string.Empty;
}
