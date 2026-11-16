
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Diagnostics;

namespace IntelligentMonitoring.AgentFramework.Learning.Middleware;

public static class FunctionLoggingMiddleware
{
    public static async ValueTask<object?> InvokeAsync(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken,
            ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        var functionName = context.Function.Name;
        var stopwatch = Stopwatch.StartNew();

        Console.WriteLine();
        Console.WriteLine("========== FUNCTION MIDDLEWARE ==========");
        Console.WriteLine($"Function started: {functionName}");

        try
        {
            // Continue to the actual C# function.
            var result = await next(context, cancellationToken);

            stopwatch.Stop();

            Console.WriteLine($"Function completed: {functionName}");
            Console.WriteLine(
                $"Execution duration: {stopwatch.ElapsedMilliseconds} ms");

            Console.WriteLine("=========================================");

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            Console.WriteLine($"Function failed: {functionName}");
            Console.WriteLine($"Exception: {ex.GetType().Name}");
            Console.WriteLine(
                $"Execution duration: {stopwatch.ElapsedMilliseconds} ms");

            Console.WriteLine("=========================================");

            throw;
        }
    }
}
