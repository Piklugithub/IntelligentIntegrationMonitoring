
using Microsoft.Agents.AI.Workflows;

namespace IntelligentMonitoring.AgentFramework.Learning.Executors;

internal sealed class TransactionInputExecutor()
    : Executor<string, string>("TransactionInputExecutor")
{
    public override ValueTask<string> HandleAsync(
        string transactionId,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine("[TransactionInputExecutor]");
        Console.WriteLine($"Received: {transactionId}");

        var normalizedId = transactionId.Trim().ToUpperInvariant();

        if (!normalizedId.StartsWith("TXN-") ||
            normalizedId.Length <= 4)
        {
            throw new ArgumentException(
                "Invalid transaction ID. Expected format: TXN-20003.");
        }

        Console.WriteLine($"Validated: {normalizedId}");

        return ValueTask.FromResult(normalizedId);
    }
}
