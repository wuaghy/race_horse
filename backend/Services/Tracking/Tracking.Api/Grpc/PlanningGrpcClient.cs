using Contracts.Grpc.Planning;
using Grpc.Core;

namespace Tracking.Api.Grpc;

public sealed class PlanningGrpcClient(PlanningQuery.PlanningQueryClient client)
{
    public async Task<TripSummaryReply?> GetTripSummaryAsync(Guid tripId, CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetTripSummaryAsync(
                new GetTripSummaryRequest { TripId = tripId.ToString() },
                cancellationToken: cancellationToken);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }
}
