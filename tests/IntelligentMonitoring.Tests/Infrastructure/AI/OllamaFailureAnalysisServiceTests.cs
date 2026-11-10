
using IntelligentMonitoring.Application.Models.Investigations;
using IntelligentMonitoring.Infrastructure.AI.Ollama;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace IntelligentMonitoring.Tests.Infrastructure.AI;

[TestFixture]
[Category("Integration")]
public class OllamaFailureAnalysisServiceTests
{
    [Test]
    public async Task AnalyzeAsync_ShouldReturnStructuredFailureAnalysis()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ollama:BaseUrl"] = "http://localhost:11434",
                ["Ollama:Model"] = "qwen2.5:3b",
                ["Ollama:TimeoutSeconds"] = "120"
            })
            .Build();

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromSeconds(120)
        };

        var service = new OllamaFailureAnalysisService(
            httpClient,
            configuration,
            NullLogger<OllamaFailureAnalysisService>.Instance);

        var investigation = new TransactionInvestigationReport
        {
            TransactionId = "TXN-20003",
            Status = "Failed",
            HasLogs = true,
            HasFailures = true,
            TotalLogs = 2,
            InformationCount = 0,
            WarningCount = 1,
            ErrorCount = 1,
            FailureServiceName = "kafka-consumer",
            ErrorCode = "DOWNSTREAM_TIMEOUT",
            FailureMessage = "Downstream shipment request timed out.",
            ServicesInvolved =
            [
                "warehouse-events-api",
                "kafka-consumer"
            ],
            Timeline =
            [
                new InvestigationTimelineEntry
                {
                    Timestamp = DateTimeOffset.UtcNow.AddSeconds(-10),
                    ServiceName = "warehouse-events-api",
                    Level = "Warning",
                    EventType = "RetryAttempt",
                    Status = "Retrying",
                    Message = "Retrying downstream shipment request after timeout.",
                    DurationMs = 1000
                },
                new InvestigationTimelineEntry
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    ServiceName = "kafka-consumer",
                    Level = "Error",
                    EventType = "ProcessingFailed",
                    Status = "Failed",
                    ErrorCode = "DOWNSTREAM_TIMEOUT",
                    Message = "Downstream shipment request timed out.",
                    DurationMs = 5000
                }
            ]
        };

        // Act
        var result = await service.AnalyzeAsync(investigation);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Summary, Is.Not.Empty);
        Assert.That(result.ModelName, Is.EqualTo("qwen2.5:3b"));
        Assert.That(result.AnalysisStatus, Is.EqualTo("Completed"));
        Assert.That(result.AnalyzedAtUtc, Is.Not.EqualTo(default(DateTimeOffset)));

        TestContext.WriteLine("AI Investigation Result");
        TestContext.WriteLine("-----------------------");
        TestContext.WriteLine($"Summary: {result.Summary}");
        TestContext.WriteLine($"Failure Stage: {result.FailureStage}");
        TestContext.WriteLine($"Probable Root Cause: {result.ProbableRootCause}");
        TestContext.WriteLine($"Confidence: {result.RootCauseConfidence}");
        TestContext.WriteLine($"Business Impact: {result.BusinessImpact}");

        TestContext.WriteLine("Supporting Evidence:");
        foreach (var evidence in result.SupportingEvidence)
        {
            TestContext.WriteLine($"- {evidence}");
        }

        TestContext.WriteLine("Recommended Actions:");
        foreach (var action in result.RecommendedActions)
        {
            TestContext.WriteLine($"- {action}");
        }

        TestContext.WriteLine("Information Gaps:");
        foreach (var gap in result.InformationGaps)
        {
            TestContext.WriteLine($"- {gap}");
        }
    }
}
