using Compliance.Application.DTOs.Readiness;
using Compliance.Application.Services;
using Compliance.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Compliance.Api.Services;

public class ComplianceRpcService : ComplianceRpc.ComplianceRpcBase
{
    private readonly IComplianceReadinessService _readinessService;
    private readonly ILogger<ComplianceRpcService> _logger;

    public ComplianceRpcService(IComplianceReadinessService readinessService, ILogger<ComplianceRpcService> logger)
    {
        _readinessService = readinessService;
        _logger = logger;
    }

    public override async Task<VerifyTripComplianceResponse> VerifyTripCompliance(
        VerifyTripComplianceRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("gRPC VerifyTripCompliance invoked for TripId={TripId}, Origin={Origin}, Destination={Dest}, Horses={HorseCount}",
            request.TripId, request.OriginCountryId, request.DestinationCountryId, request.HorseIds.Count);

        _ = Guid.TryParse(request.TripId, out var tripId);
        _ = Guid.TryParse(request.OriginCountryId, out var originCountryId);
        _ = Guid.TryParse(request.DestinationCountryId, out var destinationCountryId);

        var horseIds = request.HorseIds
            .Select(h => Guid.TryParse(h, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();

        DateOnly? departureDate = DateOnly.TryParse(request.PlannedDepartureDate, out var dt) ? dt : null;

        var evaluateRequest = new EvaluateComplianceReadinessRequest(
            tripId,
            originCountryId,
            destinationCountryId,
            horseIds,
            departureDate
        );

        var cancellationToken = context?.CancellationToken ?? CancellationToken.None;
        var result = await _readinessService.EvaluateTripReadinessAsync(evaluateRequest, cancellationToken);

        var response = new VerifyTripComplianceResponse
        {
            TripId = result.TripId.ToString(),
            IsCompliant = result.IsReady,
            TotalRequired = result.TotalRequirements,
            TotalMet = result.MetRequirements,
            TotalMissing = result.MissingRequirements,
            TotalExpired = result.ExpiredRequirements,
            EvaluatedAt = result.EvaluatedAt.ToString("o")
        };

        foreach (var item in result.Items)
        {
            response.CheckItems.Add(new ComplianceCheckItemModel
            {
                DocumentTypeCode = item.DocumentTypeCode ?? string.Empty,
                DocumentTypeName = item.DocumentTypeName ?? string.Empty,
                Scope = item.TargetScope ?? string.Empty,
                TargetId = item.TargetEntityId?.ToString() ?? string.Empty,
                Status = item.Status ?? string.Empty,
                IsMet = item.IsMet,
                Details = item.Notes ?? string.Empty
            });

            if (!item.IsMet)
            {
                var msg = !string.IsNullOrWhiteSpace(item.Notes)
                    ? item.Notes
                    : $"{item.DocumentTypeName} ({item.DocumentTypeCode}) is not met.";
                response.BlockerMessages.Add(msg);
            }
        }

        _logger.LogInformation("gRPC VerifyTripCompliance finished for TripId={TripId}: Compliant={IsCompliant}, Met={Met}/{Total}, Blockers={Blockers}",
            response.TripId, response.IsCompliant, response.TotalMet, response.TotalRequired, response.BlockerMessages.Count);

        return response;
    }
}
