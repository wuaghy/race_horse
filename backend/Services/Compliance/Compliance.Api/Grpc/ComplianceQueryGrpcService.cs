using Compliance.Domain.Entities;
using Compliance.Application.DTOs.Documents;
using Compliance.Application.DTOs.Readiness;
using Compliance.Application.Services;
using Contracts.Grpc.Compliance;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Compliance.Infrastructure.Persistence;
using System.Security.Cryptography;
using System.Text;

namespace Compliance.Api.Grpc;

public sealed class ComplianceQueryGrpcService(ComplianceDbContext db, IDocumentService documentsService, IComplianceReadinessService readinessService, IConfiguration configuration) : ComplianceQuery.ComplianceQueryBase
{
    private static readonly (string Code, string Name)[] BaselineDocumentTypes =
    [
        ("HORSE_PASSPORT", "Horse passport"),
        ("VACCINATION_CERT", "Vaccination certificate")
    ];

    public override async Task<HorseDocumentChecklistReply> GetHorseDocumentChecklist(
        GetHorseDocumentChecklistRequest request,
        ServerCallContext context)
    {
        RequireInternalServiceKey(context);
        if (!Guid.TryParse(request.CustomerUserId, out var customerUserId) ||
            !Guid.TryParse(request.RequestId, out var requestId) ||
            !Guid.TryParse(request.OriginCountryId, out var originCountryId) ||
            !Guid.TryParse(request.DestinationCountryId, out var destinationCountryId) ||
            !DateOnly.TryParse(request.DepartureDate, out var departureDate))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Customer, request, route country IDs, and departure date are required."));

        var horseIds = request.HorseIds.Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty).ToArray();
        if (horseIds.Length == 0 || horseIds.Any(id => id == Guid.Empty))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "At least one valid horse ID is required."));

        var activeRules = await db.RegulationRules
            .Include(rule => rule.Requirements).ThenInclude(requirement => requirement.DocumentType)
            .AsNoTracking()
            .Where(rule => rule.Active && rule.OriginCountryId == originCountryId && rule.DestinationCountryId == destinationCountryId &&
                           rule.EffectiveFrom <= departureDate && (!rule.EffectiveTo.HasValue || rule.EffectiveTo.Value >= departureDate))
            .ToListAsync(context.CancellationToken);

        var requirements = activeRules
            .SelectMany(rule => rule.Requirements)
            .Where(requirement => requirement.Required && requirement.DocumentType != null && requirement.DocumentType.Active && requirement.DocumentType.Scope == DocumentScope.Horse)
            .GroupBy(requirement => requirement.DocumentTypeId)
            .Select(group => new ChecklistRequirement(
                group.Key,
                group.First().DocumentType!.Code,
                group.First().DocumentType!.Name,
                group.Max(requirement => requirement.MinValidityDays ?? 0)))
            .ToList();

        if (requirements.Count == 0)
        {
            var baselineCodes = BaselineDocumentTypes.Select(item => item.Code).ToArray();
            var baselineTypes = await db.DocumentTypes.AsNoTracking()
                .Where(type => type.Active && baselineCodes.Contains(type.Code))
                .ToListAsync(context.CancellationToken);
            requirements = BaselineDocumentTypes.Select(baseline =>
            {
                var type = baselineTypes.FirstOrDefault(item => item.Code == baseline.Code);
                return new ChecklistRequirement(type?.Id ?? Guid.Empty, baseline.Code, type?.Name ?? baseline.Name, 0, type?.DefaultValidityDays);
            }).ToList();
        }

        var documents = await db.Documents.Include(document => document.DocumentType)
            .AsNoTracking()
            .Where(document => document.UploadedByUserId == customerUserId &&
                               document.HorseId.HasValue && horseIds.Contains(document.HorseId.Value) &&
                               (document.RequestId == null || document.RequestId == requestId) &&
                               document.TripId == null)
            .ToListAsync(context.CancellationToken);

        var reply = new HorseDocumentChecklistReply();
        foreach (var horseId in horseIds)
        {
            foreach (var requirement in requirements)
            {
                List<Document> matches = requirement.DocumentTypeId == Guid.Empty
                    ? []
                    : documents.Where(document => document.HorseId == horseId && document.DocumentTypeId == requirement.DocumentTypeId)
                        .OrderByDescending(document => document.UpdatedAt).ToList();
                var latest = matches.FirstOrDefault();
                var validApproved = latest is not null && latest.Status == DocumentStatus.Approved &&
                    IsValidThrough(latest, requirement, departureDate) ? latest : null;
                var status = validApproved is not null
                    ? DocumentStatus.Approved
                    : latest is null
                        ? "MISSING"
                        : latest.Status == DocumentStatus.Approved ? "EXPIRED" : latest.Status;

                reply.Items.Add(new HorseDocumentChecklistItem
                {
                    HorseId = horseId.ToString(),
                    DocumentTypeCode = requirement.Code,
                    DocumentTypeName = requirement.Name,
                    Met = validApproved is not null,
                    Status = status,
                    DocumentNo = latest?.DocumentNo ?? string.Empty,
                    ExpiryDate = latest?.ExpiryDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                    Uploaded = latest is not null && latest.Status != DocumentStatus.Rejected
                });
            }
        }

        return reply;
    }

    public override async Task<EvaluateTripReadinessReply> EvaluateTripReadiness(
        EvaluateTripReadinessRequest request,
        ServerCallContext context)
    {
        RequireInternalServiceKey(context);
        if (!Guid.TryParse(request.TripId, out var tripId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "trip_id must be a valid UUID."));
        var requestIds = ParseIds(request.RequestIds, "request_ids");
        var horseIds = ParseIds(request.HorseIds, "horse_ids");
        var routeLegs = new List<ComplianceRouteLegContext>();
        foreach (var routeLeg in request.RouteLegs)
        {
            if (!Guid.TryParse(routeLeg.RouteLegId, out var routeLegId) ||
                !Guid.TryParse(routeLeg.OriginCountryId, out var originCountryId) ||
                !Guid.TryParse(routeLeg.DestinationCountryId, out var destinationCountryId) ||
                !DateTimeOffset.TryParse(routeLeg.PlannedDepartureAt, out var departureAt))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Each route leg requires valid IDs and an RFC 3339 departure timestamp."));
            DateTime? arrivalAt = DateTimeOffset.TryParse(routeLeg.PlannedArrivalAt, out var arrival) ? arrival.UtcDateTime : null;
            routeLegs.Add(new ComplianceRouteLegContext(routeLegId, originCountryId, destinationCountryId,
                departureAt.UtcDateTime, arrivalAt));
        }

        var result = await readinessService.EvaluateDispatchTripReadinessAsync(
            new EvaluateDispatchTripReadinessRequest(tripId, requestIds, horseIds, routeLegs), context.CancellationToken);
        var reply = new EvaluateTripReadinessReply
        {
            TripId = result.TripId.ToString(),
            IsReady = result.IsReady,
            EvaluatedAt = result.EvaluatedAt.ToUniversalTime().ToString("O")
        };
        reply.Blockers.AddRange(result.Blockers);
        reply.Requirements.AddRange(result.Requirements.Select(item => new ComplianceRequirementResult
        {
            RouteLegId = item.RouteLegId.ToString(),
            RuleCode = item.RuleCode,
            DocumentTypeCode = item.DocumentTypeCode,
            TargetScope = item.TargetScope,
            TargetEntityId = item.TargetEntityId?.ToString() ?? string.Empty,
            IsMet = item.IsMet,
            Status = item.Status,
            Notes = item.Notes ?? string.Empty
        }));
        reply.Clearances.AddRange(result.Clearances.Select(item => new ClearanceRequirementResult
        {
            RouteLegId = item.RouteLegId.ToString(),
            CountryId = item.CountryId.ToString(),
            ClearanceType = item.ClearanceType,
            CaseId = item.CaseId?.ToString() ?? string.Empty,
            IsRequired = item.IsRequired,
            IsMet = item.IsMet,
            Status = item.Status,
            Notes = item.Notes ?? string.Empty
        }));
        return reply;
    }

    public override async Task<ReviewTransportRequestDocumentReply> ReviewTransportRequestDocument(
        ReviewTransportRequestDocumentRequest request,
        ServerCallContext context)
    {
        RequireInternalServiceKey(context);
        if (!Guid.TryParse(request.RequestId, out var requestId) ||
            !Guid.TryParse(request.HorseId, out var horseId) ||
            !Guid.TryParse(request.DocumentId, out var documentId) ||
            !Guid.TryParse(request.ReviewerUserId, out var reviewerUserId) ||
            !Guid.TryParse(request.CustomerUserId, out var customerUserId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Request, horse, document, customer, and reviewer IDs are required."));

        var isAttachedToRequestHorse = await db.Documents.AsNoTracking().AnyAsync(
            document => document.Id == documentId &&
                        (document.RequestId == null || document.RequestId == requestId) &&
                        document.HorseId == horseId && document.UploadedByUserId == customerUserId,
            context.CancellationToken);
        if (!isAttachedToRequestHorse)
            throw new RpcException(new Status(StatusCode.NotFound, "The document is not owned by this customer or attached to this request horse."));

        var reviewed = await documentsService.ReviewDocumentAsync(
            documentId,
            reviewerUserId,
            new ReviewDocumentRequest(DocumentStatus.Approved, request.Comment),
            context.CancellationToken);
        if (reviewed is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Document not found."));

        return new ReviewTransportRequestDocumentReply
        {
            DocumentId = reviewed.Id.ToString(),
            Status = reviewed.Status
        };
    }

    private void RequireInternalServiceKey(ServerCallContext context)
    {
        var expected = configuration["Jwt:SigningKey"];
        var provided = context.RequestHeaders.FirstOrDefault(header => header.Key == "x-internal-service-key")?.Value;
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(provided) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided)))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "An internal service credential is required."));
    }

    private static IReadOnlyList<Guid> ParseIds(IEnumerable<string> values, string field)
    {
        var ids = new List<Guid>();
        foreach (var value in values)
        {
            if (!Guid.TryParse(value, out var id))
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"{field} contains an invalid UUID."));
            ids.Add(id);
        }
        return ids.Distinct().ToArray();
    }

    private static bool IsValidThrough(Document document, ChecklistRequirement requirement, DateOnly departureDate)
    {
        var effectiveExpiry = document.ExpiryDate;
        var validityDays = requirement.DefaultValidityDays ?? document.DocumentType?.DefaultValidityDays;
        if (effectiveExpiry is null && validityDays is int days)
        {
            var validityStart = document.IssueDate ?? DateOnly.FromDateTime(document.UploadedAt);
            effectiveExpiry = validityStart.AddDays(days);
        }
        return effectiveExpiry is null || effectiveExpiry.Value >= departureDate.AddDays(requirement.MinimumValidityDays);
    }

    private sealed record ChecklistRequirement(Guid DocumentTypeId, string Code, string Name, int MinimumValidityDays, int? DefaultValidityDays = null);
}
