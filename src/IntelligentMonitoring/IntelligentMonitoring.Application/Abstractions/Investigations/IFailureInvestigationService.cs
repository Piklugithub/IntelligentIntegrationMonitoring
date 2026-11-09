
using IntelligentMonitoring.Application.Models.Investigations;

namespace IntelligentMonitoring.Application.Abstractions.Investigations;

public interface IFailureInvestigationService
{
    Task<TransactionInvestigationReport> InvestigateTransactionAsync(
        string transactionId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);
}
