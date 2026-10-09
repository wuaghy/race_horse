using System.Text.Json;
using BuildingBlocks.Api;
using BuildingBlocks.Exceptions;
using Contracts.IntegrationEvents;
using Handover.Application.Abstractions;
using Handover.Application.DTOs;
using Handover.Application.Services;
using Handover.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Handover.Infrastructure.Services;

public sealed class HandoverService(IHandoverDbContext db) : IHandoverService
{
    public async Task<HandoverListResult> GetHandoversAsync(HandoverFilter filter, CancellationToken cancellationToken = default)
    {
        if (filter.Page < 0) throw new BusinessRuleException("Page must be zero or greater.", "INVALID_PAGE", "page");
        if (filter.PageSize is < 1 or > 100) throw new BusinessRuleException("Page size must be between 1 and 100.", "INVALID_PAGE_SIZE", "pageSize");
        if (filter.Status is not null && !HandoverStatuses.IsValid(filter.Status.Trim().ToUpperInvariant()))
            throw new BusinessRuleException("Unknown handover status.", "INVALID_HANDOVER_STATUS", "status");

        var query = db.HandoverRecords.AsNoTracking().AsQueryable();
        if (filter.TripId.HasValue) query = query.Where(x => x.TripId == filter.TripId.Value);
        if (filter.CreatedByUserId.HasValue) query = query.Where(x => x.CreatedByUserId == filter.CreatedByUserId.Value);
        if (filter.Status is not null)
        {
            var normalizedStatus = filter.Status.Trim().ToUpperInvariant();
            query = query.Where(x => x.Status == normalizedStatus);
        }

        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip(checked(filter.Page * filter.PageSize))
            .Take(filter.PageSize)
            .Include(x => x.Horses)
            .ToListAsync(cancellationToken);

        return new HandoverListResult(rows.Select(ToDto).ToList(), total, filter.Page, filter.PageSize);
    }

    public async Task<HandoverDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var handover = await db.HandoverRecords.AsNoTracking()
            .Include(x => x.Horses)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return handover is null ? null : ToDto(handover);
    }

    public async Task<HandoverDto?> GetByTripIdAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        var handover = await db.HandoverRecords.AsNoTracking()
            .Include(x => x.Horses)
            .SingleOrDefaultAsync(x => x.TripId == tripId, cancellationToken);
        return handover is null ? null : ToDto(handover);
    }

    public async Task<HandoverCompletionCheckDto> EvaluateCompletionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var handover = await db.HandoverRecords.AsNoTracking()
            .Include(x => x.Horses)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("HandoverRecord", id);

        var blockers = ComputeCompletionBlockers(handover);
        return new HandoverCompletionCheckDto(handover.Id, handover.TripId, blockers.Count == 0, blockers);
    }

    public async Task<HandoverDto> CreateAsync(Guid actorUserId, CreateHandoverRequest request, Guid? correlationId, CancellationToken cancellationToken = default)
    {
        ValidateCreate(request);

        await using var tx = await db.BeginTransactionAsync(cancellationToken);

        if (await db.HandoverRecords.AnyAsync(x => x.TripId == request.TripId, cancellationToken))
            throw new BusinessRuleException("A handover record already exists for this trip.", "HANDOVER_ALREADY_EXISTS", "tripId");

        var now = DateTime.UtcNow;
        var handover = new HandoverRecord
        {
            HandoverNo = $"HND-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..21],
            TripId = request.TripId,
            Status = HandoverStatuses.Draft,
            ReceiverName = NormalizeOptional(request.ReceiverName),
            ReceiverPhone = NormalizeOptional(request.ReceiverPhone),
            ReceiverEmail = NormalizeOptional(request.ReceiverEmail),
            HandoverLocationId = request.HandoverLocationId,
            ScheduledAt = NormalizeUtc(request.ScheduledAt),
            Notes = NormalizeOptional(request.Notes),
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            UpdatedAt = now,
            VersionNo = 1
        };

        if (request.Horses is { Count: > 0 })
        {
            foreach (var item in request.Horses)
            {
                handover.Horses.Add(new HandoverHorse
                {
                    HandoverId = handover.Id,
                    HorseId = item.HorseId,
                    Status = HandoverHorseStatuses.Pending,
                    Condition = NormalizeOptional(item.Condition),
                    IssueNote = NormalizeOptional(item.IssueNote)
                });
            }
        }

        await db.HandoverRecords.AddAsync(handover, cancellationToken);
        await AddAuditAsync(handover.Id, actorUserId, "CREATED", null, HandoverStatuses.Draft, null, correlationId, now, cancellationToken);
        await SaveAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);

        return ToDto(handover);
    }

    public async Task<HandoverDto> EnsureFromTripDepartedAsync(Guid tripId, Guid actorUserId, IReadOnlyList<Guid>? horseIds, Guid? correlationId, CancellationToken cancellationToken = default)
    {
        if (tripId == Guid.Empty)
            throw new BusinessRuleException("Trip ID is required.", "TRIP_ID_REQUIRED", "tripId");

        var existing = await db.HandoverRecords
            .Include(x => x.Horses)
            .SingleOrDefaultAsync(x => x.TripId == tripId, cancellationToken);
        if (existing is not null)
            return ToDto(existing);

        var horseItems = horseIds?
            .Where(h => h != Guid.Empty)
            .Distinct()
            .Select(h => new CreateHandoverHorseItem(h))
            .ToList();

        return await CreateAsync(
            actorUserId,
            new CreateHandoverRequest(tripId, Horses: horseItems),
            correlationId,
            cancellationToken);
    }

    public async Task<HandoverDto> UpdateDraftAsync(Guid id, Guid actorUserId, UpdateHandoverRequest request, Guid? correlationId, CancellationToken cancellationToken = default)
    {
        ValidateExpectedVersion(request.ExpectedVersionNo);
        ValidateReceiverAndNotes(request.ReceiverName, request.ReceiverPhone, request.ReceiverEmail, request.Notes);

        var handover = await LoadForUpdateAsync(id, request.ExpectedVersionNo, cancellationToken);
        EnsureNotCompleted(handover);
        if (handover.Status is not (HandoverStatuses.Draft or HandoverStatuses.PendingAcceptance))
            throw new BusinessRuleException($"Handover metadata cannot be edited while in status {handover.Status}.", "HANDOVER_NOT_EDITABLE", "status");

        var now = DateTime.UtcNow;
        handover.ReceiverName = NormalizeOptional(request.ReceiverName);
        handover.ReceiverPhone = NormalizeOptional(request.ReceiverPhone);
        handover.ReceiverEmail = NormalizeOptional(request.ReceiverEmail);
        handover.HandoverLocationId = request.HandoverLocationId;
        handover.ScheduledAt = NormalizeUtc(request.ScheduledAt);
        handover.Notes = NormalizeOptional(request.Notes);
        handover.UpdatedAt = now;
        handover.VersionNo++;

        await AddAuditAsync(handover.Id, actorUserId, "UPDATED", handover.Status, handover.Status, null, correlationId, now, cancellationToken);
        await SaveAsync(cancellationToken);
        return ToDto(handover);
    }

    public async Task<HandoverHorseDto> UpsertHorseAsync(Guid handoverId, Guid actorUserId, UpsertHandoverHorseRequest request, Guid? correlationId, CancellationToken cancellationToken = default)
    {
        ValidateExpectedVersion(request.ExpectedVersionNo);
        if (request.HorseId == Guid.Empty)
            throw new BusinessRuleException("Horse ID is required.", "HORSE_ID_REQUIRED", "horseId");
        ValidateHorseFields(request.Condition, request.IssueNote);

        var handover = await LoadForUpdateAsync(handoverId, request.ExpectedVersionNo, cancellationToken);
        EnsureNotCompleted(handover);

        var now = DateTime.UtcNow;
        var horse = handover.Horses.SingleOrDefault(x => x.HorseId == request.HorseId);
        if (horse is null)
        {
            horse = new HandoverHorse
            {
                HandoverId = handover.Id,
                HorseId = request.HorseId,
                Status = HandoverHorseStatuses.Pending,
                Condition = NormalizeOptional(request.Condition),
                IssueNote = NormalizeOptional(request.IssueNote)
            };
            handover.Horses.Add(horse);
            await db.HandoverHorses.AddAsync(horse, cancellationToken);
        }
        else
        {
            horse.Condition = NormalizeOptional(request.Condition) ?? horse.Condition;
            horse.IssueNote = NormalizeOptional(request.IssueNote) ?? horse.IssueNote;
        }

        var oldState = handover.Status;
        RecalculateHandoverStatusAfterHorseChange(handover);
        handover.UpdatedAt = now;
        handover.VersionNo++;

        await AddAuditAsync(handover.Id, actorUserId, "HORSE_UPSERTED", oldState, handover.Status, $"HorseId={request.HorseId}", correlationId, now, cancellationToken);
        await SaveAsync(cancellationToken);
        return ToDto(horse);
    }

    public async Task<HandoverHorseDto> InspectHorseAsync(Guid handoverId, Guid horseId, Guid actorUserId, InspectHandoverHorseRequest request, Guid? correlationId, CancellationToken cancellationToken = default)
    {
        ValidateExpectedVersion(request.ExpectedVersionNo);
        if (horseId == Guid.Empty)
            throw new BusinessRuleException("Horse ID is required.", "HORSE_ID_REQUIRED", "horseId");
        if (string.IsNullOrWhiteSpace(request.Condition))
            throw new BusinessRuleException("Horse condition is required for inspection.", "CONDITION_REQUIRED", "condition");
        ValidateHorseFields(request.Condition, request.IssueNote);

        var handover = await LoadForUpdateAsync(handoverId, request.ExpectedVersionNo, cancellationToken);
        EnsureNotCompleted(handover);

        var now = DateTime.UtcNow;
        var horse = handover.Horses.SingleOrDefault(x => x.HorseId == horseId);
        if (horse is null)
        {
            horse = new HandoverHorse
            {
                HandoverId = handover.Id,
                HorseId = horseId,
                Status = HandoverHorseStatuses.Pending,
                Condition = request.Condition.Trim(),
                IssueNote = NormalizeOptional(request.IssueNote)
            };
            handover.Horses.Add(horse);
            await db.HandoverHorses.AddAsync(horse, cancellationToken);
        }
        else
        {
            horse.Condition = request.Condition.Trim();
            horse.IssueNote = NormalizeOptional(request.IssueNote);
        }

        handover.UpdatedAt = now;
        handover.VersionNo++;

        await AddAuditAsync(handover.Id, actorUserId, "HORSE_INSPECTED", handover.Status, handover.Status, $"HorseId={horseId}: {horse.Condition}", correlationId, now, cancellationToken);
        await SaveAsync(cancellationToken);
        return ToDto(horse);
    }

    public async Task<HandoverHorseDto> UploadHorseEvidenceAsync(Guid handoverId, Guid horseId, Guid actorUserId, StoredHandoverFile file, int? expectedVersionNo, Guid? correlationId, CancellationToken cancellationToken = default)
    {
        ValidateExpectedVersion(expectedVersionNo);
        ValidateStoredFile(file);

        var handover = await LoadForUpdateAsync(handoverId, expectedVersionNo, cancellationToken);
        EnsureNotCompleted(handover);

        var horse = handover.Horses.SingleOrDefault(x => x.HorseId == horseId)
            ?? throw new NotFoundException("HandoverHorse", horseId);

        var now = DateTime.UtcNow;
        horse.EvidenceImageUrl = file.StorageKey;
        handover.UpdatedAt = now;
        handover.VersionNo++;

        await AddAuditAsync(handover.Id, actorUserId, "HORSE_EVIDENCE_UPLOADED", handover.Status, handover.Status, $"HorseId={horseId}", correlationId, now, cancellationToken);
        await SaveAsync(cancellationToken);
        return ToDto(horse);
    }

    public async Task<string?> GetHorseEvidenceStorageKeyAsync(Guid handoverId, Guid horseId, CancellationToken cancellationToken = default)
    {
        var horse = await db.HandoverHorses.AsNoTracking()
            .SingleOrDefaultAsync(x => x.HandoverId == handoverId && x.HorseId == horseId, cancellationToken);
        return horse?.EvidenceImageUrl;
    }

    public async Task<HandoverDto> SubmitForAcceptanceAsync(Guid id, Guid actorUserId, SubmitHandoverRequest request, Guid? correlationId, CancellationToken cancellationToken = default)
    {
        ValidateExpectedVersion(request.ExpectedVersionNo);
        if (request.Notes?.Trim().Length > 3000)
            throw new BusinessRuleException("Notes cannot exceed 3000 characters.", "FIELD_TOO_LONG", "notes");

        var handover = await LoadForUpdateAsync(id, request.ExpectedVersionNo, cancellationToken);
        if (handover.Status != HandoverStatuses.Draft)
            throw new BusinessRuleException($"Only a DRAFT handover can be submitted for acceptance. Current status: {handover.Status}.", "INVALID_HANDOVER_TRANSITION", "status");

        if (handover.Horses.Count == 0)
            throw new BusinessRuleException("Handover must contain at least one horse before submitting for acceptance.", "NO_HORSES_IN_HANDOVER", "horses");

        if (string.IsNullOrWhiteSpace(handover.ReceiverName))
            throw new BusinessRuleException("Receiver name is required before submitting for acceptance.", "RECEIVER_NAME_REQUIRED", "receiverName");

        var now = DateTime.UtcNow;
        var oldState = handover.Status;
        if (!string.IsNullOrWhiteSpace(request.Notes))
            handover.Notes = request.Notes.Trim();

        handover.Status = HandoverStatuses.PendingAcceptance;
        handover.UpdatedAt = now;
        handover.VersionNo++;

        await AddAuditAsync(handover.Id, actorUserId, "SUBMITTED_FOR_ACCEPTANCE", oldState, handover.Status, NormalizeOptional(request.Notes), correlationId, now, cancellationToken);
        await SaveAsync(cancellationToken);
        return ToDto(handover);
    }

    public async Task<HandoverDto> AcceptHorseAsync(Guid handoverId, Guid horseId, Guid actorUserId, AcceptHandoverHorseRequest request, Guid? correlationId, CancellationToken cancellationToken = default)
    {
        ValidateExpectedVersion(request.ExpectedVersionNo);
        ValidateHorseFields(request.Condition, request.Note);

        var handover = await LoadForUpdateAsync(handoverId, request.ExpectedVersionNo, cancellationToken);
        EnsureCanReviewHorses(handover);

        var horse = handover.Horses.SingleOrDefault(x => x.HorseId == horseId)
            ?? throw new NotFoundException("HandoverHorse", horseId);

        var now = DateTime.UtcNow;
        var oldState = handover.Status;
        horse.Status = HandoverHorseStatuses.Accepted;
        horse.AcceptedAt = now;
        if (!string.IsNullOrWhiteSpace(request.Condition))
            horse.Condition = request.Condition.Trim();
        if (!string.IsNullOrWhiteSpace(request.Note))
            horse.IssueNote = request.Note.Trim();

        RecalculateHandoverStatusAfterHorseChange(handover);
        handover.UpdatedAt = now;
        handover.VersionNo++;

        await AddAuditAsync(handover.Id, actorUserId, "HORSE_ACCEPTED", oldState, handover.Status, NormalizeOptional(request.Note) ?? $"Accepted HorseId={horseId}", correlationId, now, cancellationToken);
        await SaveAsync(cancellationToken);
        return ToDto(handover);
    }

    public async Task<HandoverDto> DisputeHorseAsync(Guid handoverId, Guid horseId, Guid actorUserId, DisputeHandoverHorseRequest request, Guid? correlationId, CancellationToken cancellationToken = default)
    {
        ValidateExpectedVersion(request.ExpectedVersionNo);
        if (string.IsNullOrWhiteSpace(request.IssueNote))
            throw new BusinessRuleException("Issue note is required when disputing a horse.", "ISSUE_NOTE_REQUIRED", "issueNote");
        ValidateHorseFields(request.Condition, request.IssueNote);

        var handover = await LoadForUpdateAsync(handoverId, request.ExpectedVersionNo, cancellationToken);
        EnsureCanReviewHorses(handover);

        var horse = handover.Horses.SingleOrDefault(x => x.HorseId == horseId)
            ?? throw new NotFoundException("HandoverHorse", horseId);

        var now = DateTime.UtcNow;
        var oldState = handover.Status;
        horse.Status = HandoverHorseStatuses.Disputed;
        horse.IssueNote = request.IssueNote.Trim();
        horse.AcceptedAt = null;
        if (!string.IsNullOrWhiteSpace(request.Condition))
            horse.Condition = request.Condition.Trim();

        handover.Status = HandoverStatuses.Disputed;
        handover.UpdatedAt = now;
        handover.VersionNo++;

        await AddAuditAsync(handover.Id, actorUserId, "HORSE_DISPUTED", oldState, handover.Status, request.IssueNote.Trim(), correlationId, now, cancellationToken);
        await SaveAsync(cancellationToken);
        return ToDto(handover);
    }

    public async Task<HandoverDto> ResolveHorseDisputeAsync(Guid handoverId, Guid horseId, Guid actorUserId, ResolveHandoverHorseDisputeRequest request, Guid? correlationId, CancellationToken cancellationToken = default)
    {
        ValidateExpectedVersion(request.ExpectedVersionNo);
        if (string.IsNullOrWhiteSpace(request.ResolutionNote))
            throw new BusinessRuleException("Resolution note is required to resolve a disputed horse.", "RESOLUTION_NOTE_REQUIRED", "resolutionNote");
        ValidateHorseFields(request.Condition, request.ResolutionNote);

        var handover = await LoadForUpdateAsync(handoverId, request.ExpectedVersionNo, cancellationToken);
        EnsureNotCompleted(handover);

        var horse = handover.Horses.SingleOrDefault(x => x.HorseId == horseId)
            ?? throw new NotFoundException("HandoverHorse", horseId);

        if (horse.Status != HandoverHorseStatuses.Disputed)
            throw new BusinessRuleException("Only a DISPUTED horse can be formally resolved.", "HORSE_NOT_DISPUTED", "status");

        var now = DateTime.UtcNow;
        var oldState = handover.Status;
        horse.Status = HandoverHorseStatuses.Accepted;
        horse.AcceptedAt = now;
        if (!string.IsNullOrWhiteSpace(request.Condition))
            horse.Condition = request.Condition.Trim();

        var resolutionText = $"[Resolved]: {request.ResolutionNote.Trim()}";
        horse.IssueNote = string.IsNullOrWhiteSpace(horse.IssueNote)
            ? resolutionText
            : Truncate($"{horse.IssueNote} | {resolutionText}", 2000);

        RecalculateHandoverStatusAfterHorseChange(handover);
        handover.UpdatedAt = now;
        handover.VersionNo++;

        await AddAuditAsync(handover.Id, actorUserId, "DISPUTE_RESOLVED", oldState, handover.Status, request.ResolutionNote.Trim(), correlationId, now, cancellationToken);
        await SaveAsync(cancellationToken);
        return ToDto(handover);
    }

    public async Task<HandoverDto> UploadSignatureAsync(Guid id, Guid actorUserId, StoredHandoverFile file, int? expectedVersionNo, Guid? correlationId, CancellationToken cancellationToken = default)
    {
        ValidateExpectedVersion(expectedVersionNo);
        ValidateStoredFile(file);

        var handover = await LoadForUpdateAsync(id, expectedVersionNo, cancellationToken);
        EnsureNotCompleted(handover);

        var now = DateTime.UtcNow;
        handover.SignatureUrl = file.StorageKey;
        handover.UpdatedAt = now;
        handover.VersionNo++;

        await AddAuditAsync(handover.Id, actorUserId, "SIGNATURE_UPLOADED", handover.Status, handover.Status, file.OriginalFileName, correlationId, now, cancellationToken);
        await SaveAsync(cancellationToken);
        return ToDto(handover);
    }

    public async Task<string?> GetSignatureStorageKeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var handover = await db.HandoverRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return handover?.SignatureUrl;
    }

    public async Task<HandoverDto> CompleteAsync(Guid id, Guid actorUserId, CompleteHandoverRequest request, Guid? correlationId, CancellationToken cancellationToken = default)
    {
        ValidateExpectedVersion(request.ExpectedVersionNo);
        if (request.Notes?.Trim().Length > 3000)
            throw new BusinessRuleException("Notes cannot exceed 3000 characters.", "FIELD_TOO_LONG", "notes");

        await using var tx = await db.BeginTransactionAsync(cancellationToken);

        var handover = await LoadForUpdateAsync(id, request.ExpectedVersionNo, cancellationToken);
        if (handover.Status == HandoverStatuses.Completed)
            throw new BusinessRuleException("Handover is already completed.", "HANDOVER_ALREADY_COMPLETED", "status");

        var blockers = ComputeCompletionBlockers(handover);
        if (blockers.Count > 0)
        {
            var errors = blockers
                .Select(b => new ApiError { Field = "blockers", Reason = "HANDOVER_COMPLETION_BLOCKED", Message = b })
                .ToList();

            throw new AppException(
                "Handover cannot be completed because required prerequisites are not satisfied.",
                statusCode: 422,
                errorCode: 42200,
                errors: errors);
        }

        var now = DateTime.UtcNow;
        var actualAt = NormalizeUtc(request.ActualAt) ?? now;
        var oldState = handover.Status;

        if (!string.IsNullOrWhiteSpace(request.Notes))
            handover.Notes = request.Notes.Trim();

        handover.Status = HandoverStatuses.Completed;
        handover.ActualAt = actualAt;
        handover.CompletedByUserId = actorUserId;
        handover.UpdatedAt = now;
        handover.VersionNo++;

        var corrId = correlationId ?? Guid.NewGuid();
        await AddAuditAsync(handover.Id, actorUserId, "COMPLETED", oldState, HandoverStatuses.Completed, NormalizeOptional(request.Notes), corrId, now, cancellationToken);

        var acceptedHorseIds = handover.Horses
            .Where(h => h.Status == HandoverHorseStatuses.Accepted)
            .Select(h => h.HorseId)
            .OrderBy(h => h)
            .ToList();

        var eventData = new HandoverCompletedData(
            handover.Id,
            handover.HandoverNo,
            handover.TripId,
            handover.HandoverLocationId,
            actualAt,
            actorUserId,
            acceptedHorseIds);

        await db.OutboxMessages.AddAsync(new HandoverOutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "Handover.HandoverCompleted",
            AggregateType = "HandoverRecord",
            AggregateId = handover.Id,
            Payload = JsonSerializer.Serialize(eventData, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CorrelationId = corrId,
            OccurredAt = now,
            Status = "PENDING",
            CreatedAt = now
        }, cancellationToken);

        await SaveAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);

        return ToDto(handover);
    }

    private async Task<HandoverRecord> LoadForUpdateAsync(Guid id, int? expectedVersionNo, CancellationToken cancellationToken)
    {
        var handover = await db.HandoverRecords
            .Include(x => x.Horses)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("HandoverRecord", id);

        if (expectedVersionNo.HasValue && expectedVersionNo.Value != handover.VersionNo)
            throw new ConcurrencyException();

        return handover;
    }

    private static List<string> ComputeCompletionBlockers(HandoverRecord handover)
    {
        var blockers = new List<string>();

        if (handover.Status == HandoverStatuses.Completed)
        {
            blockers.Add("Handover is already completed.");
            return blockers;
        }

        if (handover.Status == HandoverStatuses.Draft)
            blockers.Add("Handover must be submitted for acceptance before completion.");

        if (handover.Horses.Count == 0)
        {
            blockers.Add("Handover must contain at least one horse.");
        }
        else
        {
            foreach (var horse in handover.Horses.OrderBy(x => x.HorseId))
            {
                if (horse.Status == HandoverHorseStatuses.Pending)
                    blockers.Add($"Horse {horse.HorseId} is still PENDING acceptance.");
                else if (horse.Status == HandoverHorseStatuses.Disputed)
                    blockers.Add($"Horse {horse.HorseId} is DISPUTED and must be resolved before completion.");
            }
        }

        if (string.IsNullOrWhiteSpace(handover.SignatureUrl))
            blockers.Add("Receiver signature is required before completing handover.");

        return blockers;
    }

    private static void RecalculateHandoverStatusAfterHorseChange(HandoverRecord handover)
    {
        if (handover.Status == HandoverStatuses.Draft)
            return;

        if (handover.Horses.Any(x => x.Status == HandoverHorseStatuses.Disputed))
        {
            handover.Status = HandoverStatuses.Disputed;
        }
        else if (handover.Horses.Count > 0 && handover.Horses.All(x => x.Status == HandoverHorseStatuses.Accepted))
        {
            handover.Status = HandoverStatuses.Accepted;
        }
        else
        {
            handover.Status = HandoverStatuses.PendingAcceptance;
        }
    }

    private static void EnsureNotCompleted(HandoverRecord handover)
    {
        if (handover.Status == HandoverStatuses.Completed)
            throw new BusinessRuleException("Completed handover records cannot be modified.", "HANDOVER_ALREADY_COMPLETED", "status");
    }

    private static void EnsureCanReviewHorses(HandoverRecord handover)
    {
        EnsureNotCompleted(handover);
        if (handover.Status == HandoverStatuses.Draft)
            throw new BusinessRuleException("Handover must be submitted for acceptance before horses can be accepted or disputed.", "HANDOVER_NOT_SUBMITTED", "status");
    }

    private static void ValidateCreate(CreateHandoverRequest request)
    {
        if (request.TripId == Guid.Empty)
            throw new BusinessRuleException("Trip ID is required.", "TRIP_ID_REQUIRED", "tripId");

        ValidateReceiverAndNotes(request.ReceiverName, request.ReceiverPhone, request.ReceiverEmail, request.Notes);

        if (request.Horses is { Count: > 0 })
        {
            var seen = new HashSet<Guid>();
            foreach (var h in request.Horses)
            {
                if (h.HorseId == Guid.Empty)
                    throw new BusinessRuleException("Horse ID cannot be empty.", "HORSE_ID_REQUIRED", "horses");
                if (!seen.Add(h.HorseId))
                    throw new BusinessRuleException($"Duplicate horse ID '{h.HorseId}' in handover.", "DUPLICATE_HORSE_ID", "horses");
                ValidateHorseFields(h.Condition, h.IssueNote);
            }
        }
    }

    private static void ValidateReceiverAndNotes(string? receiverName, string? receiverPhone, string? receiverEmail, string? notes)
    {
        if (receiverName?.Trim().Length > 200)
            throw new BusinessRuleException("Receiver name cannot exceed 200 characters.", "FIELD_TOO_LONG", "receiverName");
        if (receiverPhone?.Trim().Length > 50)
            throw new BusinessRuleException("Receiver phone cannot exceed 50 characters.", "FIELD_TOO_LONG", "receiverPhone");
        if (receiverEmail?.Trim().Length > 255)
            throw new BusinessRuleException("Receiver email cannot exceed 255 characters.", "FIELD_TOO_LONG", "receiverEmail");
        if (notes?.Trim().Length > 3000)
            throw new BusinessRuleException("Notes cannot exceed 3000 characters.", "FIELD_TOO_LONG", "notes");
    }

    private static void ValidateHorseFields(string? condition, string? issueNote)
    {
        if (condition?.Trim().Length > 2000)
            throw new BusinessRuleException("Condition cannot exceed 2000 characters.", "FIELD_TOO_LONG", "condition");
        if (issueNote?.Trim().Length > 2000)
            throw new BusinessRuleException("Issue note cannot exceed 2000 characters.", "FIELD_TOO_LONG", "issueNote");
    }

    private static void ValidateExpectedVersion(int? expectedVersionNo)
    {
        if (expectedVersionNo is < 1)
            throw new BusinessRuleException("Expected version must be greater than zero.", "INVALID_VERSION", "expectedVersionNo");
    }

    private static void ValidateStoredFile(StoredHandoverFile file)
    {
        if (string.IsNullOrWhiteSpace(file.StorageKey) || file.StorageKey.Length > 1000)
            throw new BusinessRuleException("Invalid file storage key.", "INVALID_FILE", "file");
    }

    private async Task AddAuditAsync(Guid entityId, Guid actorUserId, string action, string? oldState, string? newState, string? reason, Guid? correlationId, DateTime now, CancellationToken cancellationToken) =>
        await db.AuditLogs.AddAsync(new HandoverAuditLog
        {
            EntityType = "HandoverRecord",
            EntityId = entityId,
            Action = action,
            OldState = oldState,
            NewState = newState,
            ActorUserId = actorUserId,
            Reason = Truncate(reason, 2000),
            CorrelationId = correlationId,
            OccurredAt = now,
            CreatedAt = now
        }, cancellationToken);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException();
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime? NormalizeUtc(DateTime? value) =>
        value is null ? null : (value.Value.Kind == DateTimeKind.Utc ? value.Value : value.Value.ToUniversalTime());

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];

    private static HandoverDto ToDto(HandoverRecord handover)
    {
        var blockers = ComputeCompletionBlockers(handover);
        return new HandoverDto(
            handover.Id,
            handover.HandoverNo,
            handover.TripId,
            handover.Status,
            handover.ReceiverName,
            handover.ReceiverPhone,
            handover.ReceiverEmail,
            handover.HandoverLocationId,
            handover.ScheduledAt,
            handover.ActualAt,
            handover.Notes,
            handover.SignatureUrl,
            handover.CreatedByUserId,
            handover.CompletedByUserId,
            handover.VersionNo,
            handover.CreatedAt,
            handover.UpdatedAt,
            blockers.Count == 0,
            blockers,
            handover.Horses.OrderBy(x => x.HorseId).Select(ToDto).ToList());
    }

    private static HandoverHorseDto ToDto(HandoverHorse horse) => new(
        horse.Id,
        horse.HandoverId,
        horse.HorseId,
        horse.Status,
        horse.Condition,
        horse.IssueNote,
        horse.EvidenceImageUrl,
        horse.AcceptedAt);
}
