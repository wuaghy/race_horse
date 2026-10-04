using Compliance.Application.DTOs.Readiness;

namespace Compliance.Application.Services;

public interface IComplianceReadinessService
{
    Task<ComplianceReadinessResultDto> EvaluateTripReadinessAsync(EvaluateComplianceReadinessRequest request, CancellationToken cancellationToken = default);
}
