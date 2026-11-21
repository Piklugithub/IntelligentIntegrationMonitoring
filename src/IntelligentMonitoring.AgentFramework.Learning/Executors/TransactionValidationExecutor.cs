
using IntelligentMonitoring.AgentFramework.Learning.Models;
using Microsoft.Agents.AI.Workflows;

namespace IntelligentMonitoring.AgentFramework.Learning.Executors;

internal sealed class TransactionValidationExecutor()
    : Executor<InvestigationWorkflowContext, InvestigationWorkflowContext>(
        "TransactionValidationExecutor")
{
    public override ValueTask<InvestigationWorkflowContext> HandleAsync(
        InvestigationWorkflowContext input,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine("[TransactionValidationExecutor]");

        if (input.EventId == Guid.Empty)
        {
            throw new ArgumentException("EventId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(input.TransactionId))
        {
            throw new ArgumentException("TransactionId is required.");
        }

        if (string.IsNullOrWhiteSpace(input.TraceId))
        {
            throw new ArgumentException("TraceId is required.");
        }

        var validatedContext = input with
        {
            TransactionId = input.TransactionId.Trim()
        };

        Console.WriteLine($"Event ID: {validatedContext.EventId}");
        Console.WriteLine($"Transaction ID: {validatedContext.TransactionId}");
        Console.WriteLine($"Trace ID: {validatedContext.TraceId}");
        Console.WriteLine("Validation successful.");

        return ValueTask.FromResult(validatedContext);
    }
}
