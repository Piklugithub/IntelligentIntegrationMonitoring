
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace IntelligentMonitoring.AgentFramework.Learning.Middleware;

public static class ChatLoggingMiddleware
{
    // Intercepts normal, non-streaming model requests.
    public static async Task<ChatResponse> InvokeAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerClient,
        CancellationToken cancellationToken)
    {
        var messageCount = messages.Count();
        var stopwatch = Stopwatch.StartNew();

        Console.WriteLine();
        Console.WriteLine("========== CHAT MIDDLEWARE ==========");
        Console.WriteLine("Model request started.");
        Console.WriteLine($"Input messages: {messageCount}");
        Console.WriteLine($"Requested model: {options?.ModelId ?? "Default"}");

        try
        {
            var response = await innerClient.GetResponseAsync(
                messages,
                options,
                cancellationToken);

            stopwatch.Stop();

            Console.WriteLine("Model request completed.");
            Console.WriteLine(
                $"Response messages: {response.Messages.Count}");

            Console.WriteLine(
                $"Model execution duration: {stopwatch.ElapsedMilliseconds} ms");

            Console.WriteLine("=====================================");

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            Console.WriteLine("Model request failed.");
            Console.WriteLine($"Exception: {ex.GetType().Name}");
            Console.WriteLine(
                $"Execution duration: {stopwatch.ElapsedMilliseconds} ms");

            Console.WriteLine("=====================================");

            throw;
        }
    }

    // Intercepts streaming model requests.

    public static async IAsyncEnumerable<ChatResponseUpdate> InvokeStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerClient,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messageCount = messages.Count();
        var stopwatch = Stopwatch.StartNew();
        var updateCount = 0;

        Console.WriteLine();
        Console.WriteLine("========== CHAT STREAMING MIDDLEWARE ==========");
        Console.WriteLine("Streaming model request started.");
        Console.WriteLine($"Input messages: {messageCount}");

        // Obtain the stream separately so exceptions during
        // stream creation can be handled normally.
        IAsyncEnumerable<ChatResponseUpdate> stream;

        try
        {
            stream = innerClient.GetStreamingResponseAsync(
                messages,
                options,
                cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            Console.WriteLine("Streaming model request failed.");
            Console.WriteLine($"Exception: {ex.GetType().Name}");
            Console.WriteLine(
                $"Execution duration: {stopwatch.ElapsedMilliseconds} ms");

            throw;
        }

        // Iterator execution cannot use yield return inside
        // a try block that has a catch clause.
        try
        {
            await foreach (var update in stream.WithCancellation(
                cancellationToken))
            {
                updateCount++;
                yield return update;
            }
        }
        finally
        {
            stopwatch.Stop();

            Console.WriteLine(
                $"Streaming response updates: {updateCount}");

            Console.WriteLine(
                $"Execution duration: {stopwatch.ElapsedMilliseconds} ms");

            Console.WriteLine("===============================================");
        }
    }

}
