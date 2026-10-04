using Compliance.Application.DTOs.Clearance;

namespace Compliance.Application.Services;

public interface IClearanceService
{
    Task<IReadOnlyList<ClearanceCaseDto>> GetClearanceCasesByTripAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<ClearanceCaseDto?> GetClearanceCaseByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ClearanceCaseDto> CreateClearanceCaseAsync(Guid createdByUserId, CreateClearanceCaseRequest request, CancellationToken cancellationToken = default);
    Task<ClearanceCaseDto?> SubmitClearanceCaseAsync(Guid caseId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<ClearanceCaseDto?> ReviewClearanceCaseAsync(Guid caseId, Guid reviewerUserId, ReviewClearanceCaseRequest request, CancellationToken cancellationToken = default);
}
