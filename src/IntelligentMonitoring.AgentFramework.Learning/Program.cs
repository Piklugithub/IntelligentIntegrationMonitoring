
using IntelligentMonitoring.AgentFramework.Learning.Executors;
using IntelligentMonitoring.AgentFramework.Learning.Models;
using Microsoft.Agents.AI.Workflows;

// 1. Create executors.
var validationExecutor = new TransactionValidationExecutor();

var preparationExecutor = new InvestigationPreparationExecutor();

// 2. Configure the workflow.
WorkflowBuilder builder = new(validationExecutor);

builder.AddEdge(validationExecutor, preparationExecutor);

builder.WithOutputFrom(preparationExecutor);

var workflow = builder
    .WithName("TypedTransactionInvestigationWorkflow")
    .WithDescription(
        "Validates investigation context and prepares an investigation.")
    .Build();

// 3. Prepare workflow input.
var input = new InvestigationWorkflowContext
{
    EventId = Guid.NewGuid(),
    TransactionId = "TXN-20003",
    TraceId = "TRACE-20003",
    ServiceName = "warehouse-events-api",
    Environment = "QA"
};

Console.WriteLine("========================================");
Console.WriteLine("TYPED INVESTIGATION WORKFLOW");
Console.WriteLine("========================================");

Console.WriteLine($"Transaction: {input.TransactionId}");

// 4. Execute workflow.
try
{
    await using var run = await InProcessExecution.RunAsync(
        workflow,
        input);

    foreach (var workflowEvent in run.NewEvents)
    {
        if (workflowEvent is ExecutorCompletedEvent completed)
        {
            Console.WriteLine(
                $"Executor completed: {completed.ExecutorId}");
        }

        if (workflowEvent is WorkflowOutputEvent output)
        {
            if (output.Data is InvestigationPreparationResult result)
            {
                Console.WriteLine();
                Console.WriteLine("========== FINAL RESULT ==========");
                Console.WriteLine($"Event ID: {result.EventId}");
                Console.WriteLine($"Transaction ID: {result.TransactionId}");
                Console.WriteLine($"Trace ID: {result.TraceId}");
                Console.WriteLine($"Service: {result.ServiceName}");
                Console.WriteLine($"Environment: {result.Environment}");
                Console.WriteLine($"Status: {result.Status}");
                Console.WriteLine($"Message: {result.Message}");
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine("Workflow execution failed.");
    Console.WriteLine(ex.Message);
}

Console.WriteLine();
Console.WriteLine("WORKFLOW FINISHED");
