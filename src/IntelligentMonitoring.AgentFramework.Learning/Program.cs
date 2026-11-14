
using IntelligentMonitoring.AgentFramework.Learning.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;

var endpoint = "http://localhost:11434";
var modelName = "qwen2.5:3b";

// 1. Create the model client.
IChatClient chatClient = new OllamaApiClient(
    new Uri(endpoint),
    modelName);

// 2. Register our C# method as an agent tool.
var transactionTool =
    AIFunctionFactory.Create(
        TransactionTools.GetTransactionDetails);

// 3. Create the agent and provide its tool.
AIAgent agent = chatClient.AsAIAgent(
    name: "IntegrationInvestigationAgent",
    instructions: """
        You are an Integration Failure Investigation Specialist.

        You can investigate transactions using the tools
        available to you.

        Rules:
        1. When the user asks about a specific transaction,
           retrieve its details using GetTransactionDetails.
        2. Base your analysis on the returned transaction data.
        3. Never invent transaction details or log evidence.
        4. Clearly explain the possible root cause.
        5. Recommend practical troubleshooting steps.
        6. If a transaction is not found, explain that clearly.
        """,
    tools: [transactionTool]);

// 4. Ask the agent to investigate a real transaction
// from the tool's demo dataset.
var response = await agent.RunAsync(
    """
    Please investigate transaction TXN-20003.

    Retrieve its transaction details first.
    Then explain the failure, probable root cause,
    business impact and recommended troubleshooting steps.
    """);

// 5. Display the result.
Console.WriteLine("========================================");
Console.WriteLine("INTEGRATION INVESTIGATION AGENT");
Console.WriteLine("========================================");

Console.WriteLine(response);
