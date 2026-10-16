using System.Security.Claims;
using BuildingBlocks.Api;
using BuildingBlocks.Exceptions;
using Handover.Application.Abstractions;
using Handover.Application.DTOs;
using Handover.Application.Services;
using Handover.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Handover.Api.Endpoints;

public static class HandoverEndpoints
{
    private const long MaxFileBytes = 10 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> SupportedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf"
    };

    public static IEndpointRouteBuilder MapHandoverEndpoints(this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup("/api/v1/handovers").WithTags("Handovers").RequireAuthorization("HandoverRead");

        read.MapGet("/", async (
            [FromQuery] Guid? tripId,
            [FromQuery] string? status,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ClaimsPrincipal user,
            IHandoverService service,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var receiverEmail = user.IsInRole("CUSTOMER") ? user.FindFirst("email")?.Value : null;
            var createdByUserId = user.IsInRole("DRIVER_ESCORT") ? actorId : null;
            var result = await service.GetHandoversAsync(new HandoverFilter(
                tripId, status, page ?? 0, pageSize ?? 20,
                CreatedByUserId: createdByUserId,
                ReceiverEmail: receiverEmail), ct);
            var pagination = new PaginationMeta
            {
                Page = result.Page,
                PageSize = result.PageSize,
                TotalItems = result.TotalItems,
                TotalPages = (int)Math.Ceiling(result.TotalItems / (double)result.PageSize)
            };
            return Results.Ok(ApiResponse<IReadOnlyList<HandoverDto>>.Success(result.Items, pagination));
        }).WithName("GetHandovers");

        read.MapGet("/by-trip/{tripId:guid}", async (Guid tripId, ClaimsPrincipal user, IHandoverService service, CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var handover = await service.GetByTripIdAsync(tripId, ct);
            if (handover is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover record not found for trip."));
            if (!CanAccessHandover(user, handover)) return Results.Forbid();
            return Results.Ok(ApiResponse<HandoverDto>.Success(handover));
        }).WithName("GetHandoverByTripId");

        read.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IHandoverService service, CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var handover = await service.GetByIdAsync(id, ct);
            if (handover is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover record not found."));
            if (!CanAccessHandover(user, handover)) return Results.Forbid();
            return Results.Ok(ApiResponse<HandoverDto>.Success(handover));
        }).WithName("GetHandoverById");

        read.MapGet("/{id:guid}/completion-check", async (Guid id, ClaimsPrincipal user, IHandoverService service, CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var handover = await service.GetByIdAsync(id, ct);
            if (handover is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover record not found."));
            if (!CanAccessHandover(user, handover)) return Results.Forbid();
            var check = await service.EvaluateCompletionAsync(id, ct);
            return Results.Ok(ApiResponse<HandoverCompletionCheckDto>.Success(check));
        }).WithName("GetHandoverCompletionCheck");

        read.MapGet("/{id:guid}/signature", async (
            Guid id,
            ClaimsPrincipal user,
            IHandoverService service,
            IHandoverFileStorage storage,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var handover = await service.GetByIdAsync(id, ct);
            if (handover is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover record not found."));
            if (!CanAccessHandover(user, handover)) return Results.Forbid();
            var storageKey = await service.GetSignatureStorageKeyAsync(id, ct);
            if (string.IsNullOrWhiteSpace(storageKey))
                return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover signature not found."));
            var stream = await storage.OpenReadAsync(storageKey, ct);
            if (stream is null)
                return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover signature file not found."));
            return Results.File(stream, GetMimeType(storageKey), storageKey);
        }).WithName("DownloadHandoverSignature");

        read.MapGet("/{id:guid}/horses/{horseId:guid}/evidence", async (
            Guid id,
            Guid horseId,
            ClaimsPrincipal user,
            IHandoverService service,
            IHandoverFileStorage storage,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var handover = await service.GetByIdAsync(id, ct);
            if (handover is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover record not found."));
            if (!CanAccessHandover(user, handover)) return Results.Forbid();
            var storageKey = await service.GetHorseEvidenceStorageKeyAsync(id, horseId, ct);
            if (string.IsNullOrWhiteSpace(storageKey))
                return Results.NotFound(ApiResponse<object>.Failure(40400, "Horse evidence file not found."));
            var stream = await storage.OpenReadAsync(storageKey, ct);
            if (stream is null)
                return Results.NotFound(ApiResponse<object>.Failure(40400, "Horse evidence binary not found."));
            return Results.File(stream, GetMimeType(storageKey), storageKey);
        }).WithName("DownloadHorseEvidence");

        var prepare = app.MapGroup("/api/v1/handovers").WithTags("Handovers").RequireAuthorization("HandoverPrepare");

        prepare.MapPost("/", async (
            [FromBody] CreateHandoverRequest request,
            ClaimsPrincipal user,
            HttpContext context,
            IHandoverService service,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var created = await service.CreateAsync(actorId.Value, request, GetCorrelationId(context), ct);
            return Results.Created($"/api/v1/handovers/{created.Id}", ApiResponse<HandoverDto>.Success(created, "Handover created successfully."));
        }).WithName("CreateHandover");

        prepare.MapPatch("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateHandoverRequest request,
            ClaimsPrincipal user,
            HttpContext context,
            IHandoverService service,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var handover = await service.GetByIdAsync(id, ct);
            if (handover is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover record not found."));
            if (!CanAccessHandover(user, handover)) return Results.Forbid();
            var updated = await service.UpdateDraftAsync(id, actorId.Value, request, GetCorrelationId(context), ct);
            return Results.Ok(ApiResponse<HandoverDto>.Success(updated, "Handover updated successfully."));
        }).WithName("UpdateHandover");

        prepare.MapPost("/{id:guid}/horses", async (
            Guid id,
            [FromBody] UpsertHandoverHorseRequest request,
            ClaimsPrincipal user,
            HttpContext context,
            IHandoverService service,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var handover = await service.GetByIdAsync(id, ct);
            if (handover is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover record not found."));
            if (!CanAccessHandover(user, handover)) return Results.Forbid();
            var horse = await service.UpsertHorseAsync(id, actorId.Value, request, GetCorrelationId(context), ct);
            return Results.Created($"/api/v1/handovers/{id}/horses/{horse.HorseId}", ApiResponse<HandoverHorseDto>.Success(horse, "Horse added or updated on handover."));
        }).WithName("UpsertHandoverHorse");

        prepare.MapPost("/{id:guid}/horses/{horseId:guid}/inspect", async (
            Guid id,
            Guid horseId,
            [FromBody] InspectHandoverHorseRequest request,
            ClaimsPrincipal user,
            HttpContext context,
            IHandoverService service,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var handover = await service.GetByIdAsync(id, ct);
            if (handover is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover record not found."));
            if (!CanAccessHandover(user, handover)) return Results.Forbid();
            var horse = await service.InspectHorseAsync(id, horseId, actorId.Value, request, GetCorrelationId(context), ct);
            return Results.Ok(ApiResponse<HandoverHorseDto>.Success(horse, "Horse inspection recorded successfully."));
        }).WithName("InspectHandoverHorse");

        prepare.MapPost("/{id:guid}/submit", async (
            Guid id,
            [FromBody] SubmitHandoverRequest? request,
            ClaimsPrincipal user,
            HttpContext context,
            IHandoverService service,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var handover = await service.GetByIdAsync(id, ct);
            if (handover is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover record not found."));
            if (!CanAccessHandover(user, handover)) return Results.Forbid();
            var result = await service.SubmitForAcceptanceAsync(id, actorId.Value, request ?? new SubmitHandoverRequest(), GetCorrelationId(context), ct);
            return Results.Ok(ApiResponse<HandoverDto>.Success(result, "Handover submitted for acceptance."));
        }).WithName("SubmitHandoverForAcceptance");

        var write = app.MapGroup("/api/v1/handovers").WithTags("Handovers").RequireAuthorization("HandoverWrite");

        write.MapPost("/{id:guid}/horses/{horseId:guid}/evidence", async (
            Guid id,
            Guid horseId,
            IFormFile file,
            [FromQuery] int? expectedVersionNo,
            ClaimsPrincipal user,
            HttpContext context,
            IHandoverService service,
            IHandoverFileStorage storage,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var handover = await service.GetByIdAsync(id, ct);
            if (handover is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover record not found."));
            if (!CanAccessHandover(user, handover)) return Results.Forbid();
            var validation = await ValidateFileAsync(file, ct);
            if (validation.Error is not null)
                return Results.BadRequest(ApiResponse<object>.Failure(40000, validation.Error));

            string? key = null;
            try
            {
                await using var stream = file.OpenReadStream();
                key = await storage.SaveAsync(stream, validation.Extension!, ct);
                var dto = await service.UploadHorseEvidenceAsync(
                    id,
                    horseId,
                    actorId.Value,
                    new StoredHandoverFile(key, Path.GetFileName(file.FileName), validation.ContentType!, file.Length),
                    expectedVersionNo,
                    GetCorrelationId(context),
                    ct);
                return Results.Created($"/api/v1/handovers/{id}/horses/{horseId}/evidence", ApiResponse<HandoverHorseDto>.Success(dto, "Horse evidence uploaded successfully."));
            }
            catch
            {
                if (key is not null) await storage.DeleteAsync(key, ct);
                throw;
            }
        }).DisableAntiforgery().WithName("UploadHorseEvidence");

        write.MapPost("/{id:guid}/signature", async (
            Guid id,
            IFormFile file,
            [FromQuery] int? expectedVersionNo,
            ClaimsPrincipal user,
            HttpContext context,
            IHandoverService service,
            IHandoverFileStorage storage,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var handover = await service.GetByIdAsync(id, ct);
            if (handover is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover record not found."));
            if (!CanAccessHandover(user, handover)) return Results.Forbid();
            var validation = await ValidateFileAsync(file, ct);
            if (validation.Error is not null)
                return Results.BadRequest(ApiResponse<object>.Failure(40000, validation.Error));

            string? key = null;
            try
            {
                await using var stream = file.OpenReadStream();
                key = await storage.SaveAsync(stream, validation.Extension!, ct);
                var dto = await service.UploadSignatureAsync(
                    id,
                    actorId.Value,
                    new StoredHandoverFile(key, Path.GetFileName(file.FileName), validation.ContentType!, file.Length),
                    expectedVersionNo,
                    GetCorrelationId(context),
                    ct);
                return Results.Created($"/api/v1/handovers/{id}/signature", ApiResponse<HandoverDto>.Success(dto, "Handover signature uploaded successfully."));
            }
            catch
            {
                if (key is not null) await storage.DeleteAsync(key, ct);
                throw;
            }
        }).DisableAntiforgery().WithName("UploadHandoverSignature");

        var accept = app.MapGroup("/api/v1/handovers").WithTags("Handovers").RequireAuthorization("HandoverAccept");

        accept.MapPost("/{id:guid}/horses/{horseId:guid}/accept", async (
            Guid id,
            Guid horseId,
            [FromBody] AcceptHandoverHorseRequest? request,
            ClaimsPrincipal user,
            HttpContext context,
            IHandoverService service,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var handover = await service.GetByIdAsync(id, ct);
            if (handover is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover record not found."));
            if (!CanAccessHandover(user, handover)) return Results.Forbid();
            var result = await service.AcceptHorseAsync(id, horseId, actorId.Value, request ?? new AcceptHandoverHorseRequest(), GetCorrelationId(context), ct);
            return Results.Ok(ApiResponse<HandoverDto>.Success(result, "Horse accepted for handover."));
        }).WithName("AcceptHandoverHorse");

        accept.MapPost("/{id:guid}/horses/{horseId:guid}/dispute", async (
            Guid id,
            Guid horseId,
            [FromBody] DisputeHandoverHorseRequest request,
            ClaimsPrincipal user,
            HttpContext context,
            IHandoverService service,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var handover = await service.GetByIdAsync(id, ct);
            if (handover is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover record not found."));
            if (!CanAccessHandover(user, handover)) return Results.Forbid();
            var result = await service.DisputeHorseAsync(id, horseId, actorId.Value, request, GetCorrelationId(context), ct);
            return Results.Ok(ApiResponse<HandoverDto>.Success(result, "Horse disputed on handover."));
        }).WithName("DisputeHandoverHorse");

        var manage = app.MapGroup("/api/v1/handovers").WithTags("Handovers").RequireAuthorization("HandoverManage");

        manage.MapPost("/{id:guid}/horses/{horseId:guid}/resolve-dispute", async (
            Guid id,
            Guid horseId,
            [FromBody] ResolveHandoverHorseDisputeRequest request,
            ClaimsPrincipal user,
            HttpContext context,
            IHandoverService service,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var result = await service.ResolveHorseDisputeAsync(id, horseId, actorId.Value, request, GetCorrelationId(context), ct);
            return Results.Ok(ApiResponse<HandoverDto>.Success(result, "Horse dispute formally resolved."));
        }).WithName("ResolveHandoverHorseDispute");

        var complete = app.MapGroup("/api/v1/handovers").WithTags("Handovers").RequireAuthorization("HandoverComplete");

        complete.MapPost("/{id:guid}/complete", async (
            Guid id,
            [FromBody] CompleteHandoverRequest? request,
            ClaimsPrincipal user,
            HttpContext context,
            IHandoverService service,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var handover = await service.GetByIdAsync(id, ct);
            if (handover is null) return Results.NotFound(ApiResponse<object>.Failure(40400, "Handover record not found."));
            if (!CanAccessHandover(user, handover)) return Results.Forbid();
            var result = await service.CompleteAsync(id, actorId.Value, request ?? new CompleteHandoverRequest(), GetCorrelationId(context), ct);
            return Results.Ok(ApiResponse<HandoverDto>.Success(result, "Handover completed successfully."));
        }).WithName("CompleteHandover");

        var operations = app.MapGroup("/api/v1/handovers").WithTags("Handover Finance & Audit").RequireAuthorization("HandoverManage");

        operations.MapPost("/trips/{tripId:guid}/costs", async (
            Guid tripId,
            CreateFinancialItemRequest request,
            ClaimsPrincipal user,
            IHandoverDbContext db,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var error = ValidateFinancialItem(request);
            if (error is not null) return Results.UnprocessableEntity(ApiResponse<object>.Failure(42200, error));
            var item = new TripCostItem
            {
                TripId = tripId,
                Category = request.Category.Trim().ToUpperInvariant(),
                Description = NormalizeOptional(request.Description),
                Amount = request.Amount,
                CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
                Status = "PENDING",
                IncurredAt = NormalizeUtc(request.OccurredAt),
                CreatedByUserId = actorId.Value
            };
            await db.TripCostItems.AddAsync(item, ct);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/handovers/trips/{tripId}/costs/{item.Id}", ApiResponse<FinancialItemDto>.Success(ToDto(item), "Trip cost recorded."));
        }).WithName("CreateTripCost");

        operations.MapPost("/trips/{tripId:guid}/revenue", async (
            Guid tripId,
            CreateFinancialItemRequest request,
            ClaimsPrincipal user,
            IHandoverDbContext db,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var error = ValidateFinancialItem(request);
            if (error is not null) return Results.UnprocessableEntity(ApiResponse<object>.Failure(42200, error));
            var item = new TripRevenueItem
            {
                TripId = tripId,
                Category = request.Category.Trim().ToUpperInvariant(),
                Description = NormalizeOptional(request.Description),
                Amount = request.Amount,
                CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
                Status = "PENDING",
                RecognizedAt = NormalizeUtc(request.OccurredAt),
                CreatedByUserId = actorId.Value
            };
            await db.TripRevenueItems.AddAsync(item, ct);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/handovers/trips/{tripId}/revenue/{item.Id}", ApiResponse<FinancialItemDto>.Success(ToDto(item), "Trip revenue recorded."));
        }).WithName("CreateTripRevenue");

        operations.MapPost("/trips/{tripId:guid}/costs/{itemId:guid}/confirm", async (
            Guid tripId,
            Guid itemId,
            ClaimsPrincipal user,
            IHandoverDbContext db,
            HttpContext context,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var item = await db.TripCostItems.SingleOrDefaultAsync(x => x.Id == itemId && x.TripId == tripId, ct)
                ?? throw new NotFoundException("TripCostItem", itemId);
            if (item.Status != "PENDING") throw new BusinessRuleException("Only pending trip costs can be confirmed.", "INVALID_FINANCIAL_TRANSITION", "status");
            item.Status = "CONFIRMED";
            db.AuditLogs.Add(new HandoverAuditLog
            {
                EntityType = "TripCostItem", EntityId = item.Id, Action = "COST_CONFIRMED",
                OldState = "PENDING", NewState = "CONFIRMED", ActorUserId = actorId.Value,
                CorrelationId = GetCorrelationId(context), OccurredAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            return Results.Ok(ApiResponse<FinancialItemDto>.Success(ToDto(item), "Trip cost confirmed."));
        }).WithName("ConfirmTripCost");

        operations.MapPost("/trips/{tripId:guid}/revenue/{itemId:guid}/recognize", async (
            Guid tripId,
            Guid itemId,
            ClaimsPrincipal user,
            IHandoverDbContext db,
            HttpContext context,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var item = await db.TripRevenueItems.SingleOrDefaultAsync(x => x.Id == itemId && x.TripId == tripId, ct)
                ?? throw new NotFoundException("TripRevenueItem", itemId);
            if (item.Status != "PENDING") throw new BusinessRuleException("Only pending trip revenue can be recognized.", "INVALID_FINANCIAL_TRANSITION", "status");
            item.Status = "RECOGNIZED";
            item.RecognizedAt ??= DateTime.UtcNow;
            db.AuditLogs.Add(new HandoverAuditLog
            {
                EntityType = "TripRevenueItem", EntityId = item.Id, Action = "REVENUE_RECOGNIZED",
                OldState = "PENDING", NewState = "RECOGNIZED", ActorUserId = actorId.Value,
                CorrelationId = GetCorrelationId(context), OccurredAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            return Results.Ok(ApiResponse<FinancialItemDto>.Success(ToDto(item), "Trip revenue recognized."));
        }).WithName("RecognizeTripRevenue");

        operations.MapGet("/dashboard", async (
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            IHandoverDbContext db,
            CancellationToken ct) =>
        {
            var end = NormalizeUtc(to) ?? DateTime.UtcNow;
            var start = NormalizeUtc(from) ?? end.AddDays(-30);
            if (start >= end)
                return Results.UnprocessableEntity(ApiResponse<object>.Failure(42200, "from must be earlier than to."));

            var costs = await db.TripCostItems.AsNoTracking()
                .Where(x => (x.IncurredAt ?? x.CreatedAt) >= start && (x.IncurredAt ?? x.CreatedAt) < end && x.Status == "CONFIRMED")
                .GroupBy(x => x.CurrencyCode)
                .Select(g => new FinancialTotalDto(g.Key, g.Sum(x => x.Amount)))
                .ToListAsync(ct);
            var revenue = await db.TripRevenueItems.AsNoTracking()
                .Where(x => (x.RecognizedAt ?? x.CreatedAt) >= start && (x.RecognizedAt ?? x.CreatedAt) < end && x.Status == "RECOGNIZED")
                .GroupBy(x => x.CurrencyCode)
                .Select(g => new FinancialTotalDto(g.Key, g.Sum(x => x.Amount)))
                .ToListAsync(ct);
            var completed = await db.HandoverRecords.AsNoTracking()
                .CountAsync(x => x.Status == HandoverStatuses.Completed && x.ActualAt >= start && x.ActualAt < end, ct);
            return Results.Ok(ApiResponse<FinancialDashboardDto>.Success(
                new FinancialDashboardDto(start, end, completed, costs, revenue)));
        }).WithName("GetHandoverFinancialDashboard");

        operations.MapGet("/{id:guid}/audit", async (
            Guid id,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            IHandoverDbContext db,
            CancellationToken ct) =>
        {
            var pageValue = page ?? 0;
            var size = pageSize ?? 20;
            if (pageValue < 0 || size is < 1 or > 100)
                return Results.UnprocessableEntity(ApiResponse<object>.Failure(42200, "page must be zero or greater and pageSize must be between 1 and 100."));
            var query = db.AuditLogs.AsNoTracking().Where(x => x.EntityId == id && x.EntityType == "HandoverRecord");
            var total = await query.LongCountAsync(ct);
            var events = await query.OrderByDescending(x => x.OccurredAt).Skip(pageValue * size).Take(size).ToListAsync(ct);
            return Results.Ok(ApiResponse<IReadOnlyList<HandoverAuditLog>>.Success(events, new PaginationMeta
            {
                Page = pageValue,
                PageSize = size,
                TotalItems = total,
                TotalPages = (int)Math.Ceiling(total / (double)size)
            }));
        }).WithName("GetHandoverAuditHistory");

        return app;
    }

    private static async Task<(string? Error, string? Extension, string? ContentType)> ValidateFileAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return ("A file is required.", null, null);
        if (file.Length > MaxFileBytes) return ("File exceeds the 10 MB limit.", null, null);
        var extension = Path.GetExtension(file.FileName);
        if (!SupportedTypes.TryGetValue(extension, out var contentType))
            return ("Only JPG, PNG, WebP, and PDF files are supported.", null, null);
        if (!string.Equals(file.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
            return ("File extension and content type do not match.", null, null);

        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(), cancellationToken);
        var validSignature = extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" => read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            ".pdf" => read >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            _ => false
        };
        if (!validSignature) return ("File content does not match its supported type.", null, null);
        return (null, extension.ToLowerInvariant(), contentType);
    }

    private static string GetMimeType(string storageKey)
    {
        var ext = Path.GetExtension(storageKey);
        return SupportedTypes.TryGetValue(ext, out var mime) ? mime : "application/octet-stream";
    }

    private static Guid? GetActorId(ClaimsPrincipal user)
    {
        var value = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static bool CanAccessHandover(ClaimsPrincipal user, HandoverDto handover)
    {
        var actorId = GetActorId(user);
        if (user.IsInRole("DRIVER_ESCORT")) return actorId == handover.CreatedByUserId;
        if (user.IsInRole("CUSTOMER"))
        {
            var email = user.FindFirst("email")?.Value;
            return actorId == handover.CreatedByUserId ||
                (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(handover.ReceiverEmail) &&
                 string.Equals(email.Trim(), handover.ReceiverEmail.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        return user.IsInRole("FLEET_ROUTE_COORDINATOR") || user.IsInRole("LOGISTICS_MANAGER") || user.IsInRole("ADMIN");
    }

    private static Guid? GetCorrelationId(HttpContext context) =>
        Guid.TryParse(context.Request.Headers["X-Correlation-ID"].FirstOrDefault(), out var value) ? value : null;

    private static string? ValidateFinancialItem(CreateFinancialItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Category) || request.Category.Trim().Length > 100)
            return "Category is required and must not exceed 100 characters.";
        if (request.Description?.Length > 1000) return "Description must not exceed 1000 characters.";
        if (request.Amount <= 0) return "Amount must be greater than zero.";
        if (string.IsNullOrWhiteSpace(request.CurrencyCode) || request.CurrencyCode.Trim().Length != 3)
            return "CurrencyCode must be a three-letter currency code.";
        return null;
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime? NormalizeUtc(DateTime? value) => value.HasValue
        ? value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        }
        : null;

    private static FinancialItemDto ToDto(TripCostItem item) => new(item.Id, item.TripId, item.Category, item.Description, item.Amount, item.CurrencyCode, item.Status, item.IncurredAt, item.CreatedByUserId, item.CreatedAt);

    private static FinancialItemDto ToDto(TripRevenueItem item) => new(item.Id, item.TripId, item.Category, item.Description, item.Amount, item.CurrencyCode, item.Status, item.RecognizedAt, item.CreatedByUserId, item.CreatedAt);
}
