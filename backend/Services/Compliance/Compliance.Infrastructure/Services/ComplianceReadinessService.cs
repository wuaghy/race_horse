using System.Text.Json;
using Compliance.Application.Abstractions;
using Compliance.Application.DTOs.Readiness;
using Compliance.Application.Services;
using Compliance.Domain.Entities;
using Contracts.IntegrationEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Compliance.Infrastructure.Services;

public class ComplianceReadinessService : IComplianceReadinessService
{
    private readonly IComplianceDbContext _db;
    private readonly ILogger<ComplianceReadinessService> _logger;

    public ComplianceReadinessService(IComplianceDbContext db, ILogger<ComplianceReadinessService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ComplianceReadinessResultDto> EvaluateTripReadinessAsync(EvaluateComplianceReadinessRequest request, CancellationToken cancellationToken = default)
    {
        var targetDate = request.PlannedDepartureDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Fetch relevant regulation rules
        var rules = await _db.RegulationRules
            .Include(r => r.Requirements)
                .ThenInclude(req => req.DocumentType)
            .AsNoTracking()
            .Where(r => r.Active &&
                        r.OriginCountryId == request.OriginCountryId &&
                        r.DestinationCountryId == request.DestinationCountryId &&
                        r.EffectiveFrom <= targetDate &&
                        (!r.EffectiveTo.HasValue || r.EffectiveTo.Value >= targetDate))
            .ToListAsync(cancellationToken);

        // 2. Fetch all documents related to this trip or the given horses
        var tripDocuments = await _db.Documents
            .Include(d => d.DocumentType)
            .AsNoTracking()
            .Where(d => d.TripId == request.TripId)
            .ToListAsync(cancellationToken);

        var horseDocuments = request.HorseIds.Count > 0
            ? await _db.Documents
                .Include(d => d.DocumentType)
                .AsNoTracking()
                .Where(d => d.HorseId.HasValue && request.HorseIds.Contains(d.HorseId.Value))
                .ToListAsync(cancellationToken)
            : new List<Document>();

        var checkItems = new List<ComplianceReadinessCheckItem>();

        foreach (var rule in rules)
        {
            foreach (var req in rule.Requirements.Where(r => r.Required))
            {
                var docType = req.DocumentType;
                if (docType == null) continue;

                if (docType.Scope == DocumentScope.Horse)
                {
                    // Evaluate for each horse
                    foreach (var horseId in request.HorseIds)
                    {
                        var matchingDocs = horseDocuments
                            .Where(d => d.HorseId == horseId && d.DocumentTypeId == req.DocumentTypeId)
                            .OrderByDescending(d => d.UploadedAt)
                            .ToList();

                        var latestDoc = matchingDocs.FirstOrDefault();
                        var check = EvaluateItem(rule, req, docType, DocumentScope.Horse, horseId, latestDoc, targetDate);
                        checkItems.Add(check);
                    }
                }
                else
                {
                    // Scope is TRIP or REQUEST
                    var matchingDocs = tripDocuments
                        .Where(d => d.DocumentTypeId == req.DocumentTypeId)
                        .OrderByDescending(d => d.UploadedAt)
                        .ToList();

                    var latestDoc = matchingDocs.FirstOrDefault();
                    var check = EvaluateItem(rule, req, docType, docType.Scope, request.TripId, latestDoc, targetDate);
                    checkItems.Add(check);
                }
            }
        }

        var total = checkItems.Count;
        var met = checkItems.Count(i => i.IsMet);
        var missing = checkItems.Count(i => i.Status == "MISSING");
        var expired = checkItems.Count(i => i.Status == "EXPIRED");
        var pending = checkItems.Count(i => i.Status == DocumentStatus.Uploaded || i.Status == DocumentStatus.UnderReview);

        // Absence of applicable configuration is not proof that a trip is compliant.
        var isReady = total > 0 && met == total;

        var result = new ComplianceReadinessResultDto(
            request.TripId,
            request.OriginCountryId,
            request.DestinationCountryId,
            isReady,
            total,
            met,
            missing,
            expired,
            pending,
            checkItems,
            DateTime.UtcNow
        );

        if (isReady)
        {
            var readyEventAlreadyQueued = await _db.OutboxMessages.AnyAsync(
                message => message.EventType == "Compliance.ComplianceReady" &&
                           message.AggregateType == "TripReadiness" &&
                           message.AggregateId == request.TripId,
                cancellationToken);

            if (!readyEventAlreadyQueued)
            {
                _logger.LogInformation("Trip {TripId} evaluated as Compliance READY ({Met}/{Total} requirements met). Creating Outbox Compliance.ComplianceReady.",
                    request.TripId, met, total);

                var readyEvent = new ComplianceComplianceReadyEvent
                {
                    Data = new ComplianceComplianceReadyData(
                        request.TripId,
                        null,
                        request.OriginCountryId,
                        request.DestinationCountryId,
                        DateTime.UtcNow,
                        $"All {total} compliance requirements satisfied."),
                    CorrelationId = Guid.NewGuid()
                };

                _db.OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = readyEvent.EventType,
                    AggregateType = "TripReadiness",
                    AggregateId = request.TripId,
                    Payload = JsonSerializer.Serialize(readyEvent),
                    CorrelationId = readyEvent.CorrelationId,
                    OccurredAt = readyEvent.OccurredAt,
                    Status = "PENDING"
                });

                _db.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    EntityType = "TripReadiness",
                    EntityId = request.TripId,
                    Action = "COMPLIANCE_READY_EVALUATED",
                    OldState = null,
                    NewState = "READY",
                    ActorUserId = Guid.Empty, // System
                    Reason = $"Evaluated readiness: {met}/{total} rules met.",
                    OccurredAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            _logger.LogWarning("Trip {TripId} evaluated as NOT ready. Missing: {Missing}, Expired: {Expired}, Pending: {Pending}.",
                request.TripId, missing, expired, pending);
        }

        return result;
    }

    private static ComplianceReadinessCheckItem EvaluateItem(
        RegulationRule rule,
        RegulationDocumentRequirement req,
        DocumentType docType,
        string targetScope,
        Guid? targetEntityId,
        Document? doc,
        DateOnly targetDate)
    {
        if (doc is null)
        {
            return new ComplianceReadinessCheckItem(
                rule.RuleCode,
                rule.Title,
                docType.Code,
                docType.Name,
                targetScope,
                targetEntityId,
                IsMet: false,
                Status: "MISSING",
                DocumentNo: null,
                ExpiryDate: null,
                Notes: $"Missing required document '{docType.Name}'."
            );
        }

        if (doc.Status != DocumentStatus.Approved)
        {
            return new ComplianceReadinessCheckItem(
                rule.RuleCode,
                rule.Title,
                docType.Code,
                docType.Name,
                targetScope,
                targetEntityId,
                IsMet: false,
                Status: doc.Status,
                DocumentNo: doc.DocumentNo,
                ExpiryDate: doc.ExpiryDate,
                Notes: $"Document is in status '{doc.Status}', requires APPROVED."
            );
        }

        if (doc.ExpiryDate.HasValue)
        {
            var requiredMinDate = req.MinValidityDays.HasValue
                ? targetDate.AddDays(req.MinValidityDays.Value)
                : targetDate;

            if (doc.ExpiryDate.Value < requiredMinDate)
            {
                return new ComplianceReadinessCheckItem(
                    rule.RuleCode,
                    rule.Title,
                    docType.Code,
                    docType.Name,
                    targetScope,
                    targetEntityId,
                    IsMet: false,
                    Status: "EXPIRED",
                    DocumentNo: doc.DocumentNo,
                    ExpiryDate: doc.ExpiryDate,
                    Notes: $"Document expired or expires before requirement threshold (Expires: {doc.ExpiryDate:yyyy-MM-dd}, Needed: {requiredMinDate:yyyy-MM-dd})."
                );
            }
        }

        return new ComplianceReadinessCheckItem(
            rule.RuleCode,
            rule.Title,
            docType.Code,
            docType.Name,
            targetScope,
            targetEntityId,
            IsMet: true,
            Status: DocumentStatus.Approved,
            DocumentNo: doc.DocumentNo,
            ExpiryDate: doc.ExpiryDate,
            Notes: "Document approved and valid."
        );
    }
}
