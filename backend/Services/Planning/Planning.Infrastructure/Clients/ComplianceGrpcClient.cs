using Compliance.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Readiness;

namespace Planning.Infrastructure.Clients;

public class ComplianceGrpcClient : IComplianceVerificationClient
{
    private readonly ComplianceRpc.ComplianceRpcClient _client;
    private readonly ILogger<ComplianceGrpcClient> _logger;

    public ComplianceGrpcClient(
        ComplianceRpc.ComplianceRpcClient client,
        ILogger<ComplianceGrpcClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<TripComplianceVerificationDto> VerifyTripComplianceAsync(
        ComplianceTripQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Dispatching gRPC VerifyTripCompliance request for TripId {TripId} (Origin: {Origin}, Destination: {Dest}, Horses: {Count})",
            query.TripId, query.OriginCountryId, query.DestinationCountryId, query.HorseIds.Count);

        var request = new VerifyTripComplianceRequest
        {
            TripId = query.TripId.ToString(),
            OriginCountryId = query.OriginCountryId == Guid.Empty ? string.Empty : query.OriginCountryId.ToString(),
            DestinationCountryId = query.DestinationCountryId == Guid.Empty ? string.Empty : query.DestinationCountryId.ToString(),
            PlannedDepartureDate = query.PlannedDepartureDate?.ToString("yyyy-MM-dd") ?? string.Empty
        };

        foreach (var horseId in query.HorseIds)
        {
            request.HorseIds.Add(horseId.ToString());
        }

        var callOptions = new CallOptions(
            deadline: DateTime.UtcNow.AddSeconds(5),
            cancellationToken: cancellationToken
        );

        var response = await _client.VerifyTripComplianceAsync(request, callOptions);

        var checkItems = response.CheckItems.Select(item => new ComplianceCheckItemDto(
            item.DocumentTypeCode,
            item.DocumentTypeName,
            item.Scope,
            item.TargetId,
            item.Status,
            item.IsMet,
            item.Details
        )).ToList();

        var evaluatedAt = DateTime.TryParse(response.EvaluatedAt, out var dt) ? dt : DateTime.UtcNow;

        var dto = new TripComplianceVerificationDto(
            Guid.TryParse(response.TripId, out var parsedId) ? parsedId : query.TripId,
            response.IsCompliant,
            response.TotalRequired,
            response.TotalMet,
            response.TotalMissing,
            response.TotalExpired,
            response.BlockerMessages.ToList(),
            checkItems,
            evaluatedAt
        );

        _logger.LogInformation("gRPC VerifyTripCompliance response for TripId {TripId}: Compliant={IsCompliant}, Met={Met}/{Total}, Blockers={BlockerCount}",
            query.TripId, dto.IsCompliant, dto.TotalMet, dto.TotalRequired, dto.BlockerMessages.Count);

        return dto;
    }
}
