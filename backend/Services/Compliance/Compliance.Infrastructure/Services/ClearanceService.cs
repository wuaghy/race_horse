using System.Text.Json;
using Compliance.Application.Abstractions;
using Compliance.Application.DTOs.Clearance;
using Compliance.Application.Services;
using Compliance.Domain.Entities;
using Contracts.IntegrationEvents;
using Microsoft.EntityFrameworkCore;

namespace Compliance.Infrastructure.Services;

public class ClearanceService : IClearanceService
{
    private readonly IComplianceDbContext _db;

    public ClearanceService(IComplianceDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ClearanceCaseDto>> GetClearanceCasesByTripAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        var list = await _db.ClearanceCases
            .Include(c => c.ClearanceCaseDocuments)
                .ThenInclude(ccd => ccd.Document)
                    .ThenInclude(d => d!.DocumentType)
            .AsNoTracking()
            .Where(c => c.TripId == tripId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return list.Select(MapClearanceCaseToDto).ToList();
    }

    public async Task<ClearanceCaseDto?> GetClearanceCaseByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _db.ClearanceCases
            .Include(c => c.ClearanceCaseDocuments)
                .ThenInclude(ccd => ccd.Document)
                    .ThenInclude(d => d!.DocumentType)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return item is null ? null : MapClearanceCaseToDto(item);
    }

    public async Task<ClearanceCaseDto> CreateClearanceCaseAsync(Guid createdByUserId, CreateClearanceCaseRequest request, CancellationToken cancellationToken = default)
    {
        var datePrefix = DateTime.UtcNow.ToString("yyyyMMdd");
        var randomSuffix = Random.Shared.Next(1000, 9999);
        var caseNo = $"CLR-{datePrefix}-{randomSuffix}";

        var clearanceCase = new ClearanceCase
        {
            Id = Guid.NewGuid(),
            CaseNo = caseNo,
            TripId = request.TripId,
            RouteLegId = request.RouteLegId,
            CountryId = request.CountryId,
            BorderPointId = request.BorderPointId,
            AuthorityName = request.AuthorityName.Trim(),
            ReferenceNumber = request.ReferenceNumber,
            Status = ClearanceStatus.Draft,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            VersionNo = 1
        };

        if (request.DocumentIds != null && request.DocumentIds.Count > 0)
        {
            foreach (var docId in request.DocumentIds.Distinct())
            {
                var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == docId, cancellationToken);
                if (doc != null)
                {
                    clearanceCase.ClearanceCaseDocuments.Add(new ClearanceCaseDocument
                    {
                        Id = Guid.NewGuid(),
                        ClearanceCaseId = clearanceCase.Id,
                        DocumentId = docId,
                        Required = true,
                        Status = doc.Status
                    });
                }
            }
        }

        _db.ClearanceCases.Add(clearanceCase);

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = "ClearanceCase",
            EntityId = clearanceCase.Id,
            Action = "CREATED",
            OldState = null,
            NewState = ClearanceStatus.Draft,
            ActorUserId = createdByUserId,
            Reason = $"Clearance case {caseNo} created.",
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return (await GetClearanceCaseByIdAsync(clearanceCase.Id, cancellationToken))!;
    }

    public async Task<ClearanceCaseDto?> SubmitClearanceCaseAsync(Guid caseId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var clearanceCase = await _db.ClearanceCases
            .Include(c => c.ClearanceCaseDocuments)
            .FirstOrDefaultAsync(c => c.Id == caseId, cancellationToken);

        if (clearanceCase is null) return null;

        if (clearanceCase.Status != ClearanceStatus.Draft && clearanceCase.Status != ClearanceStatus.NeedAdditionalInfo)
        {
            throw new InvalidOperationException($"Cannot submit clearance case in state '{clearanceCase.Status}'.");
        }

        var oldState = clearanceCase.Status;
        clearanceCase.Status = ClearanceStatus.Submitted;
        clearanceCase.SubmittedAt = DateTime.UtcNow;
        clearanceCase.UpdatedAt = DateTime.UtcNow;
        clearanceCase.VersionNo++;

        foreach (var ccd in clearanceCase.ClearanceCaseDocuments)
        {
            ccd.SubmittedAt = DateTime.UtcNow;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = "ClearanceCase",
            EntityId = clearanceCase.Id,
            Action = "SUBMITTED",
            OldState = oldState,
            NewState = ClearanceStatus.Submitted,
            ActorUserId = actorUserId,
            Reason = "Submitted clearance case to authority.",
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetClearanceCaseByIdAsync(clearanceCase.Id, cancellationToken))!;
    }

    public async Task<ClearanceCaseDto?> ReviewClearanceCaseAsync(Guid caseId, Guid reviewerUserId, ReviewClearanceCaseRequest request, CancellationToken cancellationToken = default)
    {
        var targetStatus = request.Status.Trim().ToUpperInvariant();
        if (!ClearanceStatus.All.Contains(targetStatus))
        {
            throw new ArgumentException($"Invalid clearance review status: '{request.Status}'.");
        }

        var clearanceCase = await _db.ClearanceCases.FirstOrDefaultAsync(c => c.Id == caseId, cancellationToken);
        if (clearanceCase is null) return null;

        var allowedTransitions = clearanceCase.Status switch
        {
            ClearanceStatus.Submitted => new[] { ClearanceStatus.UnderReview },
            ClearanceStatus.UnderReview => new[]
            {
                ClearanceStatus.NeedAdditionalInfo,
                ClearanceStatus.Approved,
                ClearanceStatus.Rejected
            },
            ClearanceStatus.Approved => new[] { ClearanceStatus.Completed },
            _ => Array.Empty<string>()
        };
        if (!allowedTransitions.Contains(targetStatus, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Cannot transition clearance case from '{clearanceCase.Status}' to '{targetStatus}'.");
        }

        var oldState = clearanceCase.Status;
        clearanceCase.Status = targetStatus;
        clearanceCase.ReviewedAt = DateTime.UtcNow;
        clearanceCase.UpdatedAt = DateTime.UtcNow;
        clearanceCase.VersionNo++;

        if (!string.IsNullOrWhiteSpace(request.ReferenceNumber))
        {
            clearanceCase.ReferenceNumber = request.ReferenceNumber;
        }
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            clearanceCase.Notes = request.Notes;
        }
        if (targetStatus == ClearanceStatus.Rejected)
        {
            clearanceCase.RejectionReason = request.Reason;
        }
        if (targetStatus == ClearanceStatus.Completed || targetStatus == ClearanceStatus.Approved)
        {
            clearanceCase.CompletedAt = DateTime.UtcNow;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = "ClearanceCase",
            EntityId = clearanceCase.Id,
            Action = $"STATUS_CHANGED_{targetStatus}",
            OldState = oldState,
            NewState = targetStatus,
            ActorUserId = reviewerUserId,
            Reason = request.Reason ?? request.Notes,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        // If Approved / Completed -> Publish Compliance.ClearanceApproved
        if (targetStatus == ClearanceStatus.Approved || targetStatus == ClearanceStatus.Completed)
        {
            var eventData = new ComplianceClearanceApprovedData(
                clearanceCase.Id,
                clearanceCase.TripId,
                clearanceCase.CountryId,
                clearanceCase.AuthorityName,
                DateTime.UtcNow
            );

            var integrationEvent = new ComplianceClearanceApprovedEvent
            {
                Data = eventData,
                CorrelationId = Guid.NewGuid()
            };

            _db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = integrationEvent.EventType,
                AggregateType = "ClearanceCase",
                AggregateId = clearanceCase.Id,
                Payload = JsonSerializer.Serialize(integrationEvent),
                CorrelationId = integrationEvent.CorrelationId,
                OccurredAt = integrationEvent.OccurredAt,
                Status = "PENDING"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetClearanceCaseByIdAsync(clearanceCase.Id, cancellationToken))!;
    }

    private static ClearanceCaseDto MapClearanceCaseToDto(ClearanceCase c) =>
        new(
            c.Id,
            c.CaseNo,
            c.TripId,
            c.RouteLegId,
            c.CountryId,
            c.BorderPointId,
            c.AuthorityName,
            c.ReferenceNumber,
            c.Status,
            c.SubmittedAt,
            c.ReviewedAt,
            c.CompletedAt,
            c.RejectionReason,
            c.Notes,
            c.CreatedAt,
            c.UpdatedAt,
            c.VersionNo,
            c.ClearanceCaseDocuments.Select(ccd => new ClearanceCaseDocumentDto(
                ccd.Id,
                ccd.ClearanceCaseId,
                ccd.DocumentId,
                ccd.Document?.DocumentNo,
                ccd.Document?.DocumentType?.Name,
                ccd.Required,
                ccd.SubmittedAt,
                ccd.Status
            )).ToList()
        );
}
