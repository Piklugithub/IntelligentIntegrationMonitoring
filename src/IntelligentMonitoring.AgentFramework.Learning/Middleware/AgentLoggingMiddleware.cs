
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace IntelligentMonitoring.AgentFramework.Learning.Middleware;

public static class AgentLoggingMiddleware
{
    // Handles normal, non-streaming agent execution.
    public static async Task<AgentResponse> InvokeAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        var messageCount = messages.Count();
        var stopwatch = Stopwatch.StartNew();

        Console.WriteLine();
        Console.WriteLine("========== AGENT MIDDLEWARE ==========");
        Console.WriteLine("Agent execution started.");
        Console.WriteLine($"Input message count: {messageCount}");
        Console.WriteLine($"Session available: {session is not null}");

        try
        {
            var response = await innerAgent.RunAsync(
                messages,
                session,
                options,
                cancellationToken);

            stopwatch.Stop();

            Console.WriteLine("Agent execution completed.");
            Console.WriteLine(
                $"Response message count: {response.Messages.Count}");

            Console.WriteLine(
                $"Execution duration: {stopwatch.ElapsedMilliseconds} ms");

            Console.WriteLine("======================================");

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            Console.WriteLine("Agent execution failed.");
            Console.WriteLine($"Exception: {ex.GetType().Name}");
            Console.WriteLine(
                $"Execution duration: {stopwatch.ElapsedMilliseconds} ms");

            Console.WriteLine("======================================");

            throw;
        }
    }

    // Handles streaming agent execution.
    public static async IAsyncEnumerable<AgentResponseUpdate> InvokeStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messageCount = messages.Count();
        var stopwatch = Stopwatch.StartNew();

        Console.WriteLine();
        Console.WriteLine("========== AGENT STREAMING MIDDLEWARE ==========");
        Console.WriteLine("Streaming execution started.");
        Console.WriteLine($"Input message count: {messageCount}");

        try
        {
            await foreach (var update in innerAgent.RunStreamingAsync(
                messages,
                session,
                options,
                cancellationToken))
            {
                yield return update;
            }

            stopwatch.Stop();

            Console.WriteLine("Streaming execution completed.");
            Console.WriteLine(
                $"Execution duration: {stopwatch.ElapsedMilliseconds} ms");

            Console.WriteLine("===============================================");
        }
        finally
        {
            stopwatch.Stop();
        }
    }
}
