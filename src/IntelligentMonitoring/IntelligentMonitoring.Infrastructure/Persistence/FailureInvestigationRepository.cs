
using System.Text.Json;
using IntelligentMonitoring.Application.Abstractions.Investigations;
using IntelligentMonitoring.Application.Models.Investigations;
using IntelligentMonitoring.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace IntelligentMonitoring.Infrastructure.Persistence;

public sealed class FailureInvestigationRepository(
    MonitoringDbContext dbContext)
    : IFailureInvestigationRepository
{
    public async Task SaveAsync(
        FailureInvestigationRecord record,
        CancellationToken cancellationToken = default)
    {
        // Idempotency: Kafka may deliver the same event more than once.
        var alreadyExists = await dbContext.IntegrationFailures
            .AnyAsync(
                x => x.EventId == record.EventId,
                cancellationToken);

        if (alreadyExists)
        {
            return;
        }

        var failure = new IntegrationFailureEntity
        {
            EventId = record.EventId,
            EventVersion = record.EventVersion,
            OccurredAtUtc = record.OccurredAtUtc,
            ServiceName = record.ServiceName,
            Environment = record.Environment,
            IntegrationType = record.IntegrationType,
            TransactionId = record.TransactionId,
            TraceId = record.TraceId,
            ErrorCode = record.ErrorCode,
            ErrorMessage = record.ErrorMessage,
            ProcessedAtUtc = DateTimeOffset.UtcNow
        };

        var report = record.Investigation;
        var ai = record.AiAnalysis;

        var investigation = new FailureInvestigationEntity
        {
            Id = Guid.NewGuid(),
            EventId = record.EventId,

            // Deterministic investigation
            Status = report.Status,
            HasLogs = report.HasLogs,
            HasFailures = report.HasFailures,
            TotalLogs = report.TotalLogs,
            InformationCount = report.InformationCount,
            WarningCount = report.WarningCount,
            ErrorCount = report.ErrorCount,
            FirstLogAt = report.FirstLogAt,
            LastLogAt = report.LastLogAt,
            FailureTimestamp = report.FailureTimestamp,
            FailureServiceName = report.FailureServiceName,
            ErrorCode = report.ErrorCode,
            FailureMessage = report.FailureMessage,

            ServicesInvolvedJson =
                JsonSerializer.Serialize(report.ServicesInvolved),

            TimelineJson =
                JsonSerializer.Serialize(report.Timeline),

            // AI investigation
            AiSummary = ai?.Summary,
            AiFailureStage = ai?.FailureStage,
            AiProbableRootCause = ai?.ProbableRootCause,
            AiRootCauseConfidence = ai?.RootCauseConfidence,
            AiBusinessImpact = ai?.BusinessImpact,

            AiSupportingEvidenceJson =
                JsonSerializer.Serialize(ai?.SupportingEvidence ?? []),

            AiRecommendedActionsJson =
                JsonSerializer.Serialize(ai?.RecommendedActions ?? []),

            AiInformationGapsJson =
                JsonSerializer.Serialize(ai?.InformationGaps ?? []),

            AiAnalysisStatus = ai?.AnalysisStatus,
            AiModelName = ai?.ModelName,
            AiAnalyzedAtUtc = ai?.AnalyzedAtUtc,

            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        failure.Investigation = investigation;

        dbContext.IntegrationFailures.Add(failure);

        // Both records are saved in one EF Core transaction.
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
