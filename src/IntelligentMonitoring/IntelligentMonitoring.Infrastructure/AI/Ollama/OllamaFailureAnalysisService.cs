using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IntelligentMonitoring.Application.Abstractions.Investigations;
using IntelligentMonitoring.Application.Models.Investigations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IntelligentMonitoring.Infrastructure.AI.Ollama;

public sealed class OllamaFailureAnalysisService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<OllamaFailureAnalysisService> logger)
    : IFailureAnalysisService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<FailureAnalysisResult> AnalyzeAsync(
        TransactionInvestigationReport investigation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(investigation);

        if (!investigation.HasLogs || investigation.Timeline.Count == 0)
        {
            return new FailureAnalysisResult
            {
                AnalysisStatus = "InsufficientEvidence",
                Summary = "No transaction logs were available for AI analysis.",
                ProbableRootCause = "Cannot determine from available evidence.",
                RootCauseConfidence = "Low",
                InformationGaps =
                [
                    "No matching transaction logs were returned by Loki."
                ],
                AnalyzedAtUtc = DateTimeOffset.UtcNow,
                ModelName = configuration["Ollama:Model"] ?? "unknown"
            };
        }

        var model = configuration["Ollama:Model"]
            ?? throw new InvalidOperationException(
                "Ollama:Model configuration is missing.");

        var prompt = BuildPrompt(investigation);

        var request = new OllamaChatRequest
        {
            Model = model,
            Stream = false,
            Format = "json",
            Messages =
            [
                new OllamaChatMessage
                {
                    Role = "system",
                    Content = """
                        You are an experienced enterprise integration support engineer.

                        Analyze only the supplied transaction evidence.

                        Rules:
                        1. Never invent log events, services, errors or timestamps.
                        2. Distinguish confirmed facts from hypotheses.
                        3. Do not claim that a downstream system failed unless logs support it.
                        4. If evidence is insufficient, explain what is missing.
                        5. Recommend diagnostic actions before destructive remediation.
                        6. Return valid JSON matching the requested structure.
                        """
                },
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = prompt
                }
            ]
        };

        using var response = await httpClient.PostAsJsonAsync(
            "/api/chat",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var ollamaResponse =
            await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                JsonOptions,
                cancellationToken);

        var content = ollamaResponse?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(
                "Ollama returned an empty analysis response.");
        }

        FailureAnalysisResult? result;

        try
        {
            result = JsonSerializer.Deserialize<FailureAnalysisResult>(
                content,
                JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(
                ex,
                "Could not deserialize Ollama analysis response.");

            throw new InvalidOperationException(
                "Ollama returned invalid analysis JSON.",
                ex);
        }

        if (result is null ||
            string.IsNullOrWhiteSpace(result.Summary))
        {
            throw new InvalidOperationException(
                "Ollama returned an incomplete analysis result.");
        }

        result.ModelName = model;
        result.AnalyzedAtUtc = DateTimeOffset.UtcNow;
        result.AnalysisStatus = "Completed";

        return result;
    }

    private static string BuildPrompt(
        TransactionInvestigationReport investigation)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine("Investigate this integration transaction.");
        prompt.AppendLine();
        prompt.AppendLine($"TransactionId: {investigation.TransactionId}");
        prompt.AppendLine($"Status: {investigation.Status}");
        prompt.AppendLine($"TotalLogs: {investigation.TotalLogs}");
        prompt.AppendLine($"ErrorCount: {investigation.ErrorCount}");
        prompt.AppendLine($"WarningCount: {investigation.WarningCount}");
        prompt.AppendLine(
            $"ServicesInvolved: {string.Join(", ", investigation.ServicesInvolved)}");

        prompt.AppendLine();
        prompt.AppendLine("Transaction timeline:");

        foreach (var entry in investigation.Timeline)
        {
            prompt.AppendLine(
                $"{entry.Timestamp:O} | " +
                $"{entry.ServiceName} | " +
                $"{entry.Level} | " +
                $"{entry.EventType} | " +
                $"{entry.Status} | " +
                $"{entry.ErrorCode} | " +
                $"{entry.Message}");
        }

        prompt.AppendLine();
        prompt.AppendLine("""
            Return a JSON object with these properties:

            {
              "summary": "Short investigation summary",
              "failureStage": "Identified stage or Unknown",
              "probableRootCause": "Evidence-based explanation",
              "rootCauseConfidence": "High, Medium or Low",
              "businessImpact": "Known impact or Unknown",
              "supportingEvidence": ["Evidence from the timeline"],
              "recommendedActions": ["Diagnostic or resolution steps"],
              "informationGaps": ["Missing evidence or unanswered questions"]
            }
            """);

        return prompt.ToString();
    }

    private sealed class OllamaChatRequest
    {
        public string Model { get; init; } = string.Empty;

        public bool Stream { get; init; }

        public string Format { get; init; } = "json";

        public List<OllamaChatMessage> Messages { get; init; } = [];
    }

    private sealed class OllamaChatMessage
    {
        public string Role { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;
    }

    private sealed class OllamaChatResponse
    {
        public OllamaChatMessage? Message { get; init; }
    }
}