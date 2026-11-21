
using IntelligentMonitoring.AgentFramework.Learning.Models;
using Microsoft.Agents.AI.Workflows;

namespace IntelligentMonitoring.AgentFramework.Learning.Executors;

internal sealed class InvestigationPreparationExecutor()
    : Executor<InvestigationWorkflowContext, InvestigationPreparationResult>(
        "InvestigationPreparationExecutor")
{
    public override ValueTask<InvestigationPreparationResult> HandleAsync(
        InvestigationWorkflowContext input,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine("[InvestigationPreparationExecutor]");

        Console.WriteLine(
            $"Preparing investigation for {input.TransactionId}");

        var result = new InvestigationPreparationResult
        {
            EventId = input.EventId,
            TransactionId = input.TransactionId,
            TraceId = input.TraceId,
            ServiceName = input.ServiceName,
            Environment = input.Environment,
            Status = "Prepared",
            Message = $"Investigation prepared for {input.TransactionId}"
        };

        Console.WriteLine("Preparation completed.");

        return ValueTask.FromResult(result);
    }
}
