
using IntelligentMonitoring.AgentFramework.Learning.Middleware;
using IntelligentMonitoring.AgentFramework.Learning.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;

var endpoint = "http://localhost:11434";
var modelName = "qwen2.5:3b";

// 1. Create the original Ollama client.
var httpClient = new HttpClient
{
    BaseAddress = new Uri(endpoint),
    Timeout = TimeSpan.FromMinutes(5)
};

IChatClient ollamaClient = new OllamaApiClient(
    httpClient,
    modelName);

// 2. Add Chat Middleware around Ollama.
IChatClient chatClient = new ChatClientBuilder(ollamaClient)
    .Use(
        getResponseFunc: ChatLoggingMiddleware.InvokeAsync,
        getStreamingResponseFunc: ChatLoggingMiddleware.InvokeStreamingAsync)
    .Build();

// 3. Register the transaction tool.
var transactionTool =
    AIFunctionFactory.Create(
        TransactionTools.GetTransactionDetails);

// 4. Create the agent using the instrumented chat client.
AIAgent baseAgent = chatClient.AsAIAgent(
    name: "IntegrationInvestigationAgent",
    instructions: """
        You are an Integration Failure Investigation Specialist.

        Use the available transaction tool when investigating
        a specific transaction.

        Explain the probable root cause and business impact.
        Recommend practical troubleshooting steps.

        Never invent transaction information or log evidence.
        """,
    tools: [transactionTool]);

// 5. Register Agent and Function Middleware.
AIAgent agent = baseAgent
    .AsBuilder()
    .Use(
        runFunc: AgentLoggingMiddleware.InvokeAsync,
        runStreamingFunc: AgentLoggingMiddleware.InvokeStreamingAsync)
    .Use(FunctionLoggingMiddleware.InvokeAsync)
    .Build();

// 6. Execute the investigation.
var query = """
    Investigate transaction TXN-20003.

    Retrieve its transaction details using the available tool.

    Explain:
    1. What happened?
    2. What is the probable root cause?
    3. What is the potential business impact?
    4. What should the support team investigate?
    """;

Console.WriteLine("Starting investigation...");

var response = await agent.RunAsync(query);

Console.WriteLine();
Console.WriteLine("========== AGENT RESPONSE ==========");
Console.WriteLine(response);
Console.WriteLine("====================================");
