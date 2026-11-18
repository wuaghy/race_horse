using Compliance.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Readiness;

namespace Planning.Infrastructure.Clients;

public class ComplianceGrpcClient : IComplianceVerificationClient
{
    private readonly ComplianceRpc.ComplianceRpcClient _client;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger<ComplianceGrpcClient> _logger;

    public ComplianceGrpcClient(
        ComplianceRpc.ComplianceRpcClient client,
        ILogger<ComplianceGrpcClient> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _client = client;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<TripComplianceVerificationDto> VerifyTripComplianceAsync(
        ComplianceTripQuery query,
        CancellationToken cancellationToken = default)
    {
        var correlationId = _httpContextAccessor?.HttpContext?.Items["X-Correlation-ID"]?.ToString()
            ?? _httpContextAccessor?.HttpContext?.Request.Headers["X-Correlation-ID"].ToString()
            ?? Guid.NewGuid().ToString();

        _logger.LogInformation("[{CorrelationId}] Dispatching gRPC VerifyTripCompliance request for TripId {TripId} (Origin: {Origin}, Destination: {Dest}, Horses: {Count})",
            correlationId, query.TripId, query.OriginCountryId, query.DestinationCountryId, query.HorseIds.Count);

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

        var headers = new Metadata
        {
            { "x-correlation-id", correlationId }
        };

        var callOptions = new CallOptions(
            headers: headers,
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
