using Handover.Application.Abstractions;
using Handover.Application.DTOs;

namespace Handover.Application.Services;

public interface IHandoverService
{
    Task<HandoverListResult> GetHandoversAsync(HandoverFilter filter, CancellationToken cancellationToken = default);
    Task<HandoverDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<HandoverDto?> GetByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<HandoverCompletionCheckDto> EvaluateCompletionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<HandoverDto> CreateAsync(Guid actorUserId, CreateHandoverRequest request, Guid? correlationId, CancellationToken cancellationToken = default);
    Task<HandoverDto> EnsureFromTripDepartedAsync(Guid tripId, Guid actorUserId, IReadOnlyList<Guid>? horseIds, Guid? correlationId, CancellationToken cancellationToken = default);
    Task<HandoverDto> UpdateDraftAsync(Guid id, Guid actorUserId, UpdateHandoverRequest request, Guid? correlationId, CancellationToken cancellationToken = default);

    Task<HandoverHorseDto> UpsertHorseAsync(Guid handoverId, Guid actorUserId, UpsertHandoverHorseRequest request, Guid? correlationId, CancellationToken cancellationToken = default);
    Task<HandoverHorseDto> InspectHorseAsync(Guid handoverId, Guid horseId, Guid actorUserId, InspectHandoverHorseRequest request, Guid? correlationId, CancellationToken cancellationToken = default);
    Task<HandoverHorseDto> UploadHorseEvidenceAsync(Guid handoverId, Guid horseId, Guid actorUserId, StoredHandoverFile file, int? expectedVersionNo, Guid? correlationId, CancellationToken cancellationToken = default);
    Task<string?> GetHorseEvidenceStorageKeyAsync(Guid handoverId, Guid horseId, CancellationToken cancellationToken = default);

    Task<HandoverDto> SubmitForAcceptanceAsync(Guid id, Guid actorUserId, SubmitHandoverRequest request, Guid? correlationId, CancellationToken cancellationToken = default);
    Task<HandoverDto> AcceptHorseAsync(Guid handoverId, Guid horseId, Guid actorUserId, AcceptHandoverHorseRequest request, Guid? correlationId, CancellationToken cancellationToken = default);
    Task<HandoverDto> DisputeHorseAsync(Guid handoverId, Guid horseId, Guid actorUserId, DisputeHandoverHorseRequest request, Guid? correlationId, CancellationToken cancellationToken = default);
    Task<HandoverDto> ResolveHorseDisputeAsync(Guid handoverId, Guid horseId, Guid actorUserId, ResolveHandoverHorseDisputeRequest request, Guid? correlationId, CancellationToken cancellationToken = default);

    Task<HandoverDto> UploadSignatureAsync(Guid id, Guid actorUserId, StoredHandoverFile file, int? expectedVersionNo, Guid? correlationId, CancellationToken cancellationToken = default);
    Task<string?> GetSignatureStorageKeyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<HandoverDto> CompleteAsync(Guid id, Guid actorUserId, CompleteHandoverRequest request, Guid? correlationId, CancellationToken cancellationToken = default);
}
