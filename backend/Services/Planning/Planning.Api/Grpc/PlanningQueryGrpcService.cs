using Contracts.Grpc.Planning;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure.Persistence;

namespace Planning.Api.Grpc;

public sealed class PlanningQueryGrpcService(PlanningDbContext db, ILogger<PlanningQueryGrpcService> logger)
    : PlanningQuery.PlanningQueryBase
{
    public override async Task<TripSummaryReply> GetTripSummary(
        GetTripSummaryRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.TripId, out var tripId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "trip_id must be a valid UUID."));
        }

        var trip = await db.TransportTrips.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == tripId, context.CancellationToken);
        if (trip is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Trip was not found."));
        }

        logger.LogInformation("gRPC trip summary served for {TripId} with status {Status}.", trip.Id, trip.Status);
        return new TripSummaryReply
        {
            TripId = trip.Id.ToString(),
            TripNo = trip.TripNo,
            OrderId = trip.OrderId.ToString(),
            Status = trip.Status,
            ComplianceReady = trip.ComplianceReady,
            PlannedDepartureAt = trip.PlannedDepartureAt?.ToString("O") ?? string.Empty,
            PlannedArrivalAt = trip.PlannedArrivalAt?.ToString("O") ?? string.Empty,
            DelayMinutes = trip.DelayMinutes,
            VersionNo = trip.VersionNo
        };
    }
}
