using System.Text.Json;
using Contracts.IntegrationEvents;
using FluentValidation;
using Incident.Application.Abstractions;
using Incident.Application.DTOs;
using Incident.Application.Services;
using Incident.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Incident.Infrastructure.Services;

public class IncidentService : IIncidentService
{
    private readonly IIncidentDbContext _db;
    private readonly IFileStorageService _fileStorage;
    private readonly IValidator<CreateIncidentRequest> _createValidator;
    private readonly IValidator<UpdateIncidentStatusRequest> _statusValidator;
    private readonly IValidator<ResolveIncidentRequest> _resolveValidator;
    private readonly ILogger<IncidentService> _logger;

    public IncidentService(
        IIncidentDbContext db,
        IFileStorageService fileStorage,
        IValidator<CreateIncidentRequest> createValidator,
        IValidator<UpdateIncidentStatusRequest> statusValidator,
        IValidator<ResolveIncidentRequest> resolveValidator,
        ILogger<IncidentService> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _createValidator = createValidator;
        _statusValidator = statusValidator;
        _resolveValidator = resolveValidator;
        _logger = logger;
    }

    public async Task<IncidentDto> CreateIncidentAsync(CreateIncidentRequest request, Guid reportedByUserId, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);

        var datePrefix = request.OccurredAt.ToString("yyyyMMdd");
        var countToday = await _db.Incidents
            .CountAsync(i => i.IncidentNo.StartsWith($"INC-{datePrefix}"), ct);
        var incidentNo = $"INC-{datePrefix}-{(countToday + 1):D4}";

        var incident = new Incident.Domain.Entities.Incident
        {
            Id = Guid.NewGuid(),
            IncidentNo = incidentNo,
            TripId = request.TripId,
            RouteLegId = request.RouteLegId,
            CheckpointId = request.CheckpointId ?? Guid.Empty,
            HorseId = request.HorseId,
            ReportedByUserId = reportedByUserId,
            Type = request.Type,
            Severity = request.Severity,
            Status = "OPEN",
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Title = request.Title,
            Description = request.Description,
            OccurredAt = request.OccurredAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            VersionNo = 1
        };

        _db.Incidents.Add(incident);

        // Audit Log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = "Incident",
            EntityId = incident.Id,
            Action = "CREATE",
            OldState = null,
            NewState = "OPEN",
            ActorUserId = reportedByUserId,
            Reason = "Incident reported",
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        // Outbox Message: Incident.IncidentCreated
        var correlationId = Guid.NewGuid();
        var eventData = new IncidentCreatedData(
            IncidentId: incident.Id,
            IncidentNo: incident.IncidentNo,
            TripId: incident.TripId,
            RouteLegId: incident.RouteLegId,
            CheckpointId: incident.CheckpointId == Guid.Empty ? null : incident.CheckpointId,
            HorseId: incident.HorseId,
            Type: incident.Type,
            Severity: incident.Severity,
            Title: incident.Title,
            Description: incident.Description,
            OccurredAt: incident.OccurredAt,
            ReportedByUserId: incident.ReportedByUserId,
            Latitude: incident.Latitude,
            Longitude: incident.Longitude);

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "Incident.IncidentCreated",
            AggregateType = "Incident",
            AggregateId = incident.Id,
            Payload = JsonSerializer.Serialize(eventData),
            CorrelationId = correlationId,
            OccurredAt = DateTime.UtcNow,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Created incident {IncidentNo} ({Id}) for Trip {TripId}", incident.IncidentNo, incident.Id, incident.TripId);

        return MapToDto(incident);
    }

    public async Task<PagedResult<IncidentSummaryDto>> GetIncidentsAsync(IncidentFilterDto filter, CancellationToken ct = default)
    {
        var query = _db.Incidents.AsNoTracking().AsQueryable();

        if (filter.TripId.HasValue)
            query = query.Where(i => i.TripId == filter.TripId.Value);

        if (filter.HorseId.HasValue)
            query = query.Where(i => i.HorseId == filter.HorseId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Type))
            query = query.Where(i => i.Type == filter.Type);

        if (!string.IsNullOrWhiteSpace(filter.Severity))
            query = query.Where(i => i.Severity == filter.Severity);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(i => i.Status == filter.Status);

        if (filter.FromDate.HasValue)
            query = query.Where(i => i.OccurredAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(i => i.OccurredAt <= filter.ToDate.Value);

        var totalCount = await query.CountAsync(ct);

        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(i => i.OccurredAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new IncidentSummaryDto(
                i.Id,
                i.IncidentNo,
                i.TripId,
                i.HorseId,
                i.Type,
                i.Severity,
                i.Status,
                i.Title,
                i.OccurredAt,
                i.ResolvedAt,
                i.Attachments.Count))
            .ToListAsync(ct);

        return new PagedResult<IncidentSummaryDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<IncidentDto?> GetIncidentByIdAsync(Guid id, CancellationToken ct = default)
    {
        var incident = await _db.Incidents
            .Include(i => i.Attachments)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        return incident == null ? null : MapToDto(incident);
    }

    public async Task<IReadOnlyList<IncidentSummaryDto>> GetIncidentsByTripIdAsync(Guid tripId, CancellationToken ct = default)
    {
        return await _db.Incidents
            .AsNoTracking()
            .Where(i => i.TripId == tripId)
            .OrderByDescending(i => i.OccurredAt)
            .Select(i => new IncidentSummaryDto(
                i.Id,
                i.IncidentNo,
                i.TripId,
                i.HorseId,
                i.Type,
                i.Severity,
                i.Status,
                i.Title,
                i.OccurredAt,
                i.ResolvedAt,
                i.Attachments.Count))
            .ToListAsync(ct);
    }

    public async Task<IncidentDto> UpdateStatusAsync(Guid id, UpdateIncidentStatusRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        await _statusValidator.ValidateAndThrowAsync(request, ct);

        var incident = await _db.Incidents
            .Include(i => i.Attachments)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (incident == null)
            throw new KeyNotFoundException($"Incident with ID {id} was not found.");

        ValidateStatusTransition(incident.Status, request.NewStatus);

        var oldStatus = incident.Status;
        incident.Status = request.NewStatus;
        incident.UpdatedAt = DateTime.UtcNow;
        incident.VersionNo++;

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = "Incident",
            EntityId = incident.Id,
            Action = "STATUS_CHANGE",
            OldState = oldStatus,
            NewState = request.NewStatus,
            ActorUserId = actorUserId,
            Reason = request.Reason,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated incident {IncidentNo} status: {OldStatus} -> {NewStatus}", incident.IncidentNo, oldStatus, incident.Status);

        return MapToDto(incident);
    }

    public async Task<IncidentDto> ResolveIncidentAsync(Guid id, ResolveIncidentRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        await _resolveValidator.ValidateAndThrowAsync(request, ct);

        var incident = await _db.Incidents
            .Include(i => i.Attachments)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (incident == null)
            throw new KeyNotFoundException($"Incident with ID {id} was not found.");

        if (incident.Status is "RESOLVED" or "CLOSED" or "CANCELLED")
            throw new InvalidOperationException($"Cannot resolve incident {incident.IncidentNo} currently in status {incident.Status}.");

        var oldStatus = incident.Status;
        incident.Status = "RESOLVED";
        incident.ResolvedAt = DateTime.UtcNow;
        incident.ResolutionNote = request.ResolutionNote;
        incident.UpdatedAt = DateTime.UtcNow;
        incident.VersionNo++;

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = "Incident",
            EntityId = incident.Id,
            Action = "RESOLVE",
            OldState = oldStatus,
            NewState = "RESOLVED",
            ActorUserId = actorUserId,
            Reason = request.ResolutionNote,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        var eventData = new IncidentResolvedData(
            IncidentId: incident.Id,
            IncidentNo: incident.IncidentNo,
            TripId: incident.TripId,
            ResolvedAt: incident.ResolvedAt.Value,
            ResolutionNote: incident.ResolutionNote,
            ResolvedByUserId: actorUserId);

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "Incident.IncidentResolved",
            AggregateType = "Incident",
            AggregateId = incident.Id,
            Payload = JsonSerializer.Serialize(eventData),
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Resolved incident {IncidentNo} ({Id})", incident.IncidentNo, incident.Id);

        return MapToDto(incident);
    }

    public async Task<IncidentAttachmentDto> AddAttachmentAsync(
        Guid incidentId,
        string fileName,
        string contentType,
        Stream fileStream,
        Guid uploadedByUserId,
        CancellationToken ct = default)
    {
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == incidentId, ct);
        if (incident == null)
            throw new KeyNotFoundException($"Incident with ID {incidentId} was not found.");

        var fileUrl = await _fileStorage.UploadFileAsync(fileStream, fileName, contentType, ct);

        var attachment = new IncidentAttachment
        {
            Id = Guid.NewGuid(),
            IncidentId = incidentId,
            FileName = fileName,
            FileUrl = fileUrl,
            MimeType = contentType,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = DateTime.UtcNow
        };

        _db.IncidentAttachments.Add(attachment);

        incident.UpdatedAt = DateTime.UtcNow;
        incident.VersionNo++;

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = "Incident",
            EntityId = incidentId,
            Action = "ATTACHMENT_ADDED",
            OldState = null,
            NewState = null,
            ActorUserId = uploadedByUserId,
            Reason = $"Uploaded attachment: {fileName}",
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Added attachment {FileName} to incident {IncidentId}", fileName, incidentId);

        return new IncidentAttachmentDto(
            attachment.Id,
            attachment.IncidentId,
            attachment.FileName,
            attachment.FileUrl,
            attachment.MimeType,
            attachment.UploadedByUserId,
            attachment.UploadedAt);
    }

    private static void ValidateStatusTransition(string currentStatus, string newStatus)
    {
        if (currentStatus == newStatus) return;

        var isValid = currentStatus switch
        {
            "OPEN" => newStatus is "INVESTIGATING" or "RESOLVED" or "CANCELLED",
            "INVESTIGATING" => newStatus is "RESOLVED" or "CANCELLED",
            "RESOLVED" => newStatus is "CLOSED",
            "CANCELLED" => false,
            "CLOSED" => false,
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidOperationException($"Invalid status transition from {currentStatus} to {newStatus}.");
        }
    }

    private static IncidentDto MapToDto(Incident.Domain.Entities.Incident incident) =>
        new(
            incident.Id,
            incident.IncidentNo,
            incident.TripId,
            incident.RouteLegId,
            incident.CheckpointId == Guid.Empty ? null : incident.CheckpointId,
            incident.HorseId,
            incident.ReportedByUserId,
            incident.Type,
            incident.Severity,
            incident.Status,
            incident.LocationId,
            incident.Latitude,
            incident.Longitude,
            incident.Title,
            incident.Description,
            incident.OccurredAt,
            incident.ResolvedAt,
            incident.ResolutionNote,
            incident.CreatedAt,
            incident.UpdatedAt,
            incident.VersionNo,
            incident.Attachments?.Select(a => new IncidentAttachmentDto(
                a.Id,
                a.IncidentId,
                a.FileName,
                a.FileUrl,
                a.MimeType,
                a.UploadedByUserId,
                a.UploadedAt)).ToList() ?? []);
}
