
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
        var report = record.Investigation;

        // This method is for completed, evidence-backed investigations.
        // NoLogsFound must be handled by the retry workflow.
        if (!report.HasLogs)
        {
            throw new InvalidOperationException(
                $"Cannot complete investigation for EventId {record.EventId}. " +
                "No matching logs were found. Schedule a retry instead.");
        }

        // Find the existing event and its investigation.
        var failure = await dbContext.IntegrationFailures
            .Include(x => x.Investigation)
            .FirstOrDefaultAsync(
                x => x.EventId == record.EventId,
                cancellationToken);

        // Support events that were not previously registered.
        // Normally, the Kafka consumer will have already inserted them.
        if (failure is null)
        {
            failure = new IntegrationFailureEntity
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

            dbContext.IntegrationFailures.Add(failure);
        }

        // Create an investigation if one does not exist.
        var investigation = failure.Investigation;

        if (investigation is null)
        {
            investigation = new FailureInvestigationEntity
            {
                Id = Guid.NewGuid(),
                EventId = record.EventId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            // Explicitly mark the new investigation as Added.
            dbContext.FailureInvestigations.Add(investigation);

            failure.Investigation = investigation;
        }

        var ai = record.AiAnalysis;

        // Update deterministic investigation results.
        investigation.Status = report.Status;
        investigation.HasLogs = report.HasLogs;
        investigation.HasFailures = report.HasFailures;
        investigation.TotalLogs = report.TotalLogs;
        investigation.InformationCount = report.InformationCount;
        investigation.WarningCount = report.WarningCount;
        investigation.ErrorCount = report.ErrorCount;
        investigation.FirstLogAt = report.FirstLogAt;
        investigation.LastLogAt = report.LastLogAt;
        investigation.FailureTimestamp = report.FailureTimestamp;
        investigation.FailureServiceName = report.FailureServiceName;
        investigation.ErrorCode = report.ErrorCode;
        investigation.FailureMessage = report.FailureMessage;

        investigation.ServicesInvolvedJson =
            JsonSerializer.Serialize(report.ServicesInvolved);

        investigation.TimelineJson =
            JsonSerializer.Serialize(report.Timeline);

        // Update AI analysis.
        investigation.AiSummary = ai?.Summary;
        investigation.AiFailureStage = ai?.FailureStage;
        investigation.AiProbableRootCause = ai?.ProbableRootCause;
        investigation.AiRootCauseConfidence = ai?.RootCauseConfidence;
        investigation.AiBusinessImpact = ai?.BusinessImpact;

        investigation.AiSupportingEvidenceJson =
            JsonSerializer.Serialize(ai?.SupportingEvidence ?? []);

        investigation.AiRecommendedActionsJson =
            JsonSerializer.Serialize(ai?.RecommendedActions ?? []);

        investigation.AiInformationGapsJson =
            JsonSerializer.Serialize(ai?.InformationGaps ?? []);

        investigation.AiAnalysisStatus = ai?.AnalysisStatus;
        investigation.AiModelName = ai?.ModelName;
        investigation.AiAnalyzedAtUtc = ai?.AnalyzedAtUtc;

        // Mark the event lifecycle as completed.
        var now = DateTimeOffset.UtcNow;

        failure.InvestigationStatus = "Completed";
        failure.NextRetryAtUtc = null;
        failure.LastAttemptAtUtc = now;
        failure.LastError = null;
        failure.CompletedAtUtc = now;

        // Save event and investigation changes together.
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
