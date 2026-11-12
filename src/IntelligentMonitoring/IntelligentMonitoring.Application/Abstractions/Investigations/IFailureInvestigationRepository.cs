
using IntelligentMonitoring.Application.Models.Investigations;

namespace IntelligentMonitoring.Application.Abstractions.Investigations;

public interface IFailureInvestigationRepository
{
    Task SaveAsync(
        FailureInvestigationRecord record,
        Guid leaseId,
        CancellationToken cancellationToken = default);
}
