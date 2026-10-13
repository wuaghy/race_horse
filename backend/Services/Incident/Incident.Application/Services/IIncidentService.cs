using Incident.Application.DTOs;

namespace Incident.Application.Services;

public interface IIncidentService
{
    Task<IncidentDto> CreateIncidentAsync(CreateIncidentRequest request, Guid reportedByUserId, CancellationToken ct = default);
    Task<PagedResult<IncidentSummaryDto>> GetIncidentsAsync(IncidentFilterDto filter, CancellationToken ct = default);
    Task<IncidentDto?> GetIncidentByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<IncidentSummaryDto>> GetIncidentsByTripIdAsync(Guid tripId, CancellationToken ct = default);
    Task<IncidentDto> UpdateStatusAsync(Guid id, UpdateIncidentStatusRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<IncidentDto> ResolveIncidentAsync(Guid id, ResolveIncidentRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<IncidentAttachmentDto> AddAttachmentAsync(Guid incidentId, string fileName, string contentType, Stream fileStream, Guid uploadedByUserId, CancellationToken ct = default);
}
