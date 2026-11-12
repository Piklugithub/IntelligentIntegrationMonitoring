
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
        Guid leaseId,
        CancellationToken cancellationToken = default)
    {
        var report = record.Investigation;

        // Only completed, evidence-backed investigations can be saved.
        if (!report.HasLogs)
        {
            throw new InvalidOperationException(
                $"Cannot complete investigation for EventId {record.EventId}. " +
                "No matching logs were found. Schedule a retry instead.");
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            var now = DateTimeOffset.UtcNow;

            // Validate ownership and lock the eligible event row.
            // Serializable isolation protects this validation and save
            // from a competing transaction taking over the event.
            var failure = await dbContext.IntegrationFailures
                .Include(x => x.Investigation)
                .FirstOrDefaultAsync(
                    x =>
                        x.EventId == record.EventId &&
                        x.InvestigationStatus == "Processing" &&
                        x.LeaseId == leaseId &&
                        x.ProcessingLeaseExpiresAtUtc != null &&
                        x.ProcessingLeaseExpiresAtUtc > now,
                    cancellationToken);

            if (failure is null)
            {
                throw new InvalidOperationException(
                    $"Cannot save investigation for EventId {record.EventId}. " +
                    "The lease is expired, invalid, or no longer owned by this worker.");
            }

            // Create an investigation if one does not exist.
            var investigation = failure.Investigation;

            if (investigation is null)
            {
                investigation = new FailureInvestigationEntity
                {
                    Id = Guid.NewGuid(),
                    EventId = record.EventId,
                    CreatedAtUtc = now
                };

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

            // Complete the event and release ownership.
            failure.InvestigationStatus = "Completed";
            failure.NextRetryAtUtc = null;
            failure.LastAttemptAtUtc = now;
            failure.LastError = null;
            failure.CompletedAtUtc = now;
            failure.ProcessingLeaseExpiresAtUtc = null;
            failure.LeaseId = null;

            await dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
