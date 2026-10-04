using System.Text.Json;
using Compliance.Application.Abstractions;
using Compliance.Application.DTOs.Documents;
using Compliance.Application.Services;
using Compliance.Domain.Entities;
using Contracts.IntegrationEvents;
using Microsoft.EntityFrameworkCore;

namespace Compliance.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private readonly IComplianceDbContext _db;

    public DocumentService(IComplianceDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(DocumentFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _db.Documents
            .Include(d => d.DocumentType)
            .Include(d => d.Reviews)
            .AsNoTracking()
            .AsQueryable();

        if (filter.HorseId.HasValue) query = query.Where(d => d.HorseId == filter.HorseId.Value);
        if (filter.RequestId.HasValue) query = query.Where(d => d.RequestId == filter.RequestId.Value);
        if (filter.TripId.HasValue) query = query.Where(d => d.TripId == filter.TripId.Value);
        if (filter.DocumentTypeId.HasValue) query = query.Where(d => d.DocumentTypeId == filter.DocumentTypeId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(d => d.Status == filter.Status.ToUpperInvariant());

        var list = await query.OrderByDescending(d => d.UploadedAt).ToListAsync(cancellationToken);
        return list.Select(MapDocumentToDto).ToList();
    }

    public async Task<DocumentDto?> GetDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _db.Documents
            .Include(d => d.DocumentType)
            .Include(d => d.Reviews)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        return item is null ? null : MapDocumentToDto(item);
    }

    public async Task<DocumentDto> UploadDocumentAsync(Guid uploadedByUserId, UploadDocumentRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.HorseId.HasValue && !request.RequestId.HasValue && !request.TripId.HasValue)
        {
            throw new ArgumentException("Document must be associated with at least one entity: HorseId, RequestId, or TripId.");
        }

        var docType = await _db.DocumentTypes.FirstOrDefaultAsync(dt => dt.Id == request.DocumentTypeId, cancellationToken);
        if (docType is null)
        {
            throw new KeyNotFoundException($"DocumentType with ID {request.DocumentTypeId} not found.");
        }

        // Generate unique DocumentNo: DOC-YYYYMMDD-XXXX
        var datePrefix = DateTime.UtcNow.ToString("yyyyMMdd");
        var randomSuffix = Random.Shared.Next(1000, 9999);
        var documentNo = $"DOC-{datePrefix}-{randomSuffix}";

        // Compute expiry date if not provided and default validity days are configured
        var issueDate = request.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var expiryDate = request.ExpiryDate;
        if (!expiryDate.HasValue && docType.DefaultValidityDays.HasValue)
        {
            expiryDate = issueDate.AddDays(docType.DefaultValidityDays.Value);
        }

        var document = new Document
        {
            Id = Guid.NewGuid(),
            DocumentNo = documentNo,
            DocumentTypeId = request.DocumentTypeId,
            HorseId = request.HorseId,
            RequestId = request.RequestId,
            TripId = request.TripId,
            UploadedByUserId = uploadedByUserId,
            FileName = request.FileName,
            FileUrl = request.FileUrl,
            FileSize = request.FileSize,
            MimeType = request.MimeType,
            IssueDate = issueDate,
            ExpiryDate = expiryDate,
            IssuingAuthority = request.IssuingAuthority,
            Status = DocumentStatus.Uploaded,
            UploadedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            VersionNo = 1
        };

        _db.Documents.Add(document);

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = "Document",
            EntityId = document.Id,
            Action = "UPLOADED",
            OldState = null,
            NewState = DocumentStatus.Uploaded,
            ActorUserId = uploadedByUserId,
            Reason = $"Document {documentNo} uploaded.",
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return (await GetDocumentByIdAsync(document.Id, cancellationToken))!;
    }

    public async Task<DocumentDto?> ReviewDocumentAsync(Guid documentId, Guid reviewerUserId, ReviewDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var targetStatus = request.Status.Trim().ToUpperInvariant();
        if (!DocumentStatus.All.Contains(targetStatus) || targetStatus == DocumentStatus.Uploaded)
        {
            throw new ArgumentException($"Invalid review status: '{request.Status}'. Expected APPROVED, REJECTED, or NEED_ADDITIONAL_INFO.");
        }

        var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (document is null) return null;

        var oldState = document.Status;
        document.Status = targetStatus;
        document.CurrentReviewNote = request.Comment;
        document.UpdatedAt = DateTime.UtcNow;
        document.VersionNo++;

        // Append to immutable review history
        _db.DocumentReviews.Add(new DocumentReview
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            ReviewerUserId = reviewerUserId,
            Status = targetStatus,
            Comment = request.Comment,
            ReviewedAt = DateTime.UtcNow
        });

        // Audit Log
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = "Document",
            EntityId = document.Id,
            Action = $"STATUS_CHANGED_{targetStatus}",
            OldState = oldState,
            NewState = targetStatus,
            ActorUserId = reviewerUserId,
            Reason = request.Comment,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });

        // Outbox event if approved
        if (targetStatus == DocumentStatus.Approved)
        {
            var eventData = new ComplianceDocumentApprovedData(
                document.Id,
                document.DocumentNo,
                document.HorseId,
                document.RequestId,
                document.TripId,
                DateTime.UtcNow
            );

            var integrationEvent = new ComplianceDocumentApprovedEvent
            {
                Data = eventData,
                CorrelationId = Guid.NewGuid()
            };

            _db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = integrationEvent.EventType,
                AggregateType = "Document",
                AggregateId = document.Id,
                Payload = JsonSerializer.Serialize(integrationEvent),
                CorrelationId = integrationEvent.CorrelationId,
                OccurredAt = integrationEvent.OccurredAt,
                Status = "PENDING"
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetDocumentByIdAsync(document.Id, cancellationToken))!;
    }

    private static DocumentDto MapDocumentToDto(Document d) =>
        new(
            d.Id,
            d.DocumentNo,
            d.DocumentTypeId,
            d.DocumentType?.Code,
            d.DocumentType?.Name,
            d.DocumentType?.Scope,
            d.HorseId,
            d.RequestId,
            d.TripId,
            d.UploadedByUserId,
            d.FileName,
            d.FileUrl,
            d.FileSize,
            d.MimeType,
            d.IssueDate,
            d.ExpiryDate,
            d.IssuingAuthority,
            d.Status,
            d.CurrentReviewNote,
            d.UploadedAt,
            d.UpdatedAt,
            d.VersionNo,
            d.Reviews.Select(r => new DocumentReviewDto(r.Id, r.DocumentId, r.ReviewerUserId, r.Status, r.Comment, r.ReviewedAt)).ToList()
        );
}
