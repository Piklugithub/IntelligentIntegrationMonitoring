using IntelligentMonitoring.Application.Models.Investigations;

namespace IntelligentMonitoring.Application.Abstractions.Investigations;

public interface IFailureAnalysisService
{
    Task<FailureAnalysisResult> AnalyzeAsync(
        TransactionInvestigationReport investigation,
        CancellationToken cancellationToken = default);
}