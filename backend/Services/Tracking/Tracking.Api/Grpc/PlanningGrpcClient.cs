using Contracts.Grpc.Planning;
using Grpc.Core;
using Microsoft.Extensions.Configuration;

namespace Tracking.Api.Grpc;

public sealed class PlanningGrpcClient(PlanningQuery.PlanningQueryClient client, IConfiguration configuration)
{
    private readonly string _internalServiceKey = configuration["Jwt:SigningKey"] ?? string.Empty;

    public async Task<TripSummaryReply?> GetTripSummaryAsync(Guid tripId, CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetTripSummaryAsync(
                new GetTripSummaryRequest { TripId = tripId.ToString() },
                headers: CreateHeaders(),
                cancellationToken: cancellationToken);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Guid>> GetCustomerTripIdsAsync(Guid identityUserId, CancellationToken cancellationToken)
    {
        var response = await client.GetCustomerTripIdsAsync(
            new GetCustomerTripIdsRequest { IdentityUserId = identityUserId.ToString() },
            headers: CreateHeaders(),
            cancellationToken: cancellationToken);
        return response.TripIds.Select(value => Guid.TryParse(value, out var tripId) ? tripId : Guid.Empty)
            .Where(tripId => tripId != Guid.Empty).Distinct().ToArray();
    }

    public async Task<bool> CustomerCanAccessTripAsync(Guid tripId, Guid identityUserId, Guid? horseId, CancellationToken cancellationToken)
    {
        var summary = await GetTripSummaryAsync(tripId, cancellationToken);
        if (summary is null) return false;
        var owner = summary.CustomerOwners.FirstOrDefault(item => Guid.TryParse(item.CustomerUserId, out var userId) && userId == identityUserId);
        if (owner is null) return false;
        return !horseId.HasValue || owner.HorseIds.Any(value => Guid.TryParse(value, out var assignedHorseId) && assignedHorseId == horseId.Value);
    }

    public async Task<ValidateTripDepartureReply> ValidateTripDepartureAsync(Guid tripId, CancellationToken cancellationToken) =>
        await client.ValidateTripDepartureAsync(
            new ValidateTripDepartureRequest { TripId = tripId.ToString() },
            headers: CreateHeaders(),
            cancellationToken: cancellationToken);

    private Metadata CreateHeaders()
    {
        if (string.IsNullOrWhiteSpace(_internalServiceKey))
            throw new InvalidOperationException("Jwt:SigningKey is required for internal Planning gRPC calls.");
        return new Metadata { { "x-internal-service-key", _internalServiceKey } };
    }
}
