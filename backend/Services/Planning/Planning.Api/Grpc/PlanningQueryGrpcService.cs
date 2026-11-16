using Contracts.Grpc.Planning;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure.Persistence;
using Planning.Application.Services;
using System.Security.Cryptography;
using System.Text;
using System.Data;
using System.Text.Json;

namespace Planning.Api.Grpc;

public sealed class PlanningQueryGrpcService(PlanningDbContext db, IConfiguration configuration, ILogger<PlanningQueryGrpcService> logger, ITripComplianceEvaluator complianceEvaluator)
    : PlanningQuery.PlanningQueryBase
{
    public override async Task<RouteTransportModesReply> GetRouteTransportModes(
        GetRouteTransportModesRequest request,
        ServerCallContext context)
    {
        RequireInternalServiceKey(context);
        if (!Guid.TryParse(request.OriginLocationId, out var originId) ||
            !Guid.TryParse(request.DestinationLocationId, out var destinationId) ||
            originId == destinationId)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Both location IDs must be valid and different."));

        var (configured, modes) = await RouteCapabilityEndpoints.GetCapabilitiesAsync(db, configuration, originId, destinationId, context.CancellationToken);
        var estimatedDurations = configured
            ? await RouteCapabilityEndpoints.GetEstimatedDurationsAsync(db, originId, destinationId, modes, context.CancellationToken)
            : [];
        var replyOriginCountryId = string.Empty;
        var replyDestinationCountryId = string.Empty;
        var locations = await db.Locations.AsNoTracking()
            .Where(location => location.Active && (location.Id == originId || location.Id == destinationId))
            .Select(location => new { location.Id, location.CountryId })
            .ToListAsync(context.CancellationToken);
        if (locations.Count == 2)
        {
            replyOriginCountryId = locations.Single(location => location.Id == originId).CountryId.ToString();
            replyDestinationCountryId = locations.Single(location => location.Id == destinationId).CountryId.ToString();
        }
        configured &= locations.Count == 2;
        if (!configured)
        {
            modes = [];
            estimatedDurations = [];
        }
        var reply = new RouteTransportModesReply
        {
            Configured = configured,
            OriginCountryId = replyOriginCountryId,
            DestinationCountryId = replyDestinationCountryId
        };
        reply.AllowedModes.AddRange(modes);
        reply.EstimatedDurations.AddRange(estimatedDurations.Select(item => new RouteModeDurationEstimate
        {
            Mode = item.Mode,
            MinDurationMinutes = item.MinDurationMinutes,
            MaxDurationMinutes = item.MaxDurationMinutes,
            MinExpressDurationMinutes = item.MinExpressDurationMinutes ?? 0,
            MaxExpressDurationMinutes = item.MaxExpressDurationMinutes ?? 0
        }));
        return reply;
    }

    public override async Task<OrderTripStatusReply> GetOrderTripStatus(
        GetOrderTripStatusRequest request,
        ServerCallContext context)
    {
        RequireInternalServiceKey(context);
        if (!Guid.TryParse(request.OrderId, out var orderId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "order_id must be a valid UUID."));
        }

        var linkedTripId = await db.TripOrders.AsNoTracking().Where(item => item.OrderId == orderId)
            .Select(item => (Guid?)item.TripId).SingleOrDefaultAsync(context.CancellationToken);
        var trip = linkedTripId.HasValue
            ? await db.TransportTrips.AsNoTracking().SingleOrDefaultAsync(item => item.Id == linkedTripId.Value, context.CancellationToken)
            : await db.TransportTrips.AsNoTracking().SingleOrDefaultAsync(item => item.OrderId == orderId, context.CancellationToken);
        return trip is null
            ? new OrderTripStatusReply { TripExists = false }
            : new OrderTripStatusReply { TripExists = true, TripId = trip.Id.ToString(), Status = trip.Status };
    }

    public override async Task<GetCustomerTripIdsReply> GetCustomerTripIds(
        GetCustomerTripIdsRequest request,
        ServerCallContext context)
    {
        RequireInternalServiceKey(context);
        if (!Guid.TryParse(request.IdentityUserId, out var identityUserId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "identity_user_id must be a valid UUID."));

        var tripIds = await db.TripOrders.AsNoTracking()
            .Where(order => order.CustomerUserId == identityUserId && order.TripId.HasValue)
            .Select(order => order.TripId!.Value)
            .Distinct()
            .ToListAsync(context.CancellationToken);
        var reply = new GetCustomerTripIdsReply();
        reply.TripIds.AddRange(tripIds.Select(id => id.ToString()));
        return reply;
    }

    public override async Task<ValidateTripDepartureReply> ValidateTripDeparture(
        ValidateTripDepartureRequest request,
        ServerCallContext context)
    {
        RequireInternalServiceKey(context);
        if (!Guid.TryParse(request.TripId, out var tripId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "trip_id must be a valid UUID."));
        var trip = await db.TransportTrips.SingleOrDefaultAsync(item => item.Id == tripId, context.CancellationToken);
        if (trip is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Trip was not found."));

        var evaluation = await complianceEvaluator.EvaluateAsync(tripId, context.CancellationToken);
        trip.ComplianceReady = evaluation.IsReady;
        trip.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(context.CancellationToken);

        var blockers = evaluation.Blockers.ToList();
        if (!string.Equals(trip.Status, Planning.Domain.PlanningConstants.TripStatus.Ready, StringComparison.OrdinalIgnoreCase))
            blockers.Insert(0, $"Trip status must be READY before departure (current status: {trip.Status}).");
        var reply = new ValidateTripDepartureReply
        {
            Allowed = blockers.Count == 0 && evaluation.IsReady,
            TripId = trip.Id.ToString(),
            Status = trip.Status,
            ComplianceReady = evaluation.IsReady
        };
        reply.Blockers.AddRange(blockers);
        return reply;
    }

    public override async Task<SetOrderCancellationPendingReply> SetOrderCancellationPending(
        SetOrderCancellationPendingRequest request,
        ServerCallContext context)
    {
        RequireInternalServiceKey(context);
        if (!Guid.TryParse(request.OrderId, out var orderId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "order_id must be a valid UUID."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, context.CancellationToken);
        var memberOrder = await db.TripOrders.SingleOrDefaultAsync(item => item.OrderId == orderId, context.CancellationToken);
        if (memberOrder is not null && memberOrder.TripId is null)
        {
            await transaction.CommitAsync(context.CancellationToken);
            return new SetOrderCancellationPendingReply { TripExists = false, CanCancel = true };
        }
        var trip = memberOrder?.TripId is Guid linkedTripId
            ? await db.TransportTrips.FromSqlInterpolated($"SELECT * FROM dbo.TransportTrips WITH (UPDLOCK, HOLDLOCK) WHERE Id = {linkedTripId}").SingleOrDefaultAsync(context.CancellationToken)
            : await db.TransportTrips.FromSqlInterpolated($"SELECT * FROM dbo.TransportTrips WITH (UPDLOCK, HOLDLOCK) WHERE OrderId = {orderId}").SingleOrDefaultAsync(context.CancellationToken);
        if (trip?.MergedIntoTripId is { } mergedIntoTripId)
            trip = await db.TransportTrips.FromSqlInterpolated($"SELECT * FROM dbo.TransportTrips WITH (UPDLOCK, HOLDLOCK) WHERE Id = {mergedIntoTripId}")
                .SingleOrDefaultAsync(context.CancellationToken);
        if (trip is null)
        {
            await transaction.CommitAsync(context.CancellationToken);
            return new SetOrderCancellationPendingReply { TripExists = false, CanCancel = true };
        }

        var canCancel = trip.Status == Planning.Domain.PlanningConstants.TripStatus.Planned;
        if (canCancel)
        {
            trip.CancellationPending = request.Pending;
            trip.UpdatedAt = DateTime.UtcNow;
            trip.VersionNo++;
            await db.SaveChangesAsync(context.CancellationToken);
        }
        await transaction.CommitAsync(context.CancellationToken);
        return new SetOrderCancellationPendingReply
        {
            TripExists = true,
            CanCancel = canCancel,
            TripId = trip.Id.ToString(),
            Status = trip.Status
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

    public override async Task<TripSummaryReply> GetTripSummary(
        GetTripSummaryRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.TripId, out var tripId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "trip_id must be a valid UUID."));
        }

        RequireInternalServiceKey(context);

        var trip = await db.TransportTrips.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == tripId, context.CancellationToken);
        if (trip is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Trip was not found."));
        }

        if (trip.MergedIntoTripId is Guid canonicalTripId)
            trip = await db.TransportTrips.AsNoTracking().SingleOrDefaultAsync(item => item.Id == canonicalTripId, context.CancellationToken) ?? trip;

        var orders = await db.TripOrders.AsNoTracking().Where(item => item.TripId == trip.Id).ToListAsync(context.CancellationToken);
        var owners = new List<(Guid UserId, Guid[] HorseIds)>();
        foreach (var order in orders.Where(item => item.CustomerUserId.HasValue))
        {
            Guid[] horseIds;
            try { horseIds = JsonSerializer.Deserialize<Guid[]>(order.HorseIdsJson) ?? []; }
            catch (JsonException) { horseIds = []; }
            owners.Add((order.CustomerUserId!.Value, horseIds));
        }
        var distinctOwners = owners.GroupBy(owner => owner.UserId)
            .Select(group => (UserId: group.Key, HorseIds: group.SelectMany(owner => owner.HorseIds).Distinct().ToArray()))
            .ToArray();

        logger.LogInformation("gRPC trip summary served for {TripId} with status {Status} and {OwnerCount} customer owners.", trip.Id, trip.Status, distinctOwners.Length);
        var reply = new TripSummaryReply
        {
            TripId = trip.Id.ToString(),
            TripNo = trip.TripNo,
            OrderId = trip.OrderId?.ToString() ?? string.Empty,
            Status = trip.Status,
            ComplianceReady = trip.ComplianceReady,
            PlannedDepartureAt = trip.PlannedDepartureAt?.ToString("O") ?? string.Empty,
            PlannedArrivalAt = trip.PlannedArrivalAt?.ToString("O") ?? string.Empty,
            DelayMinutes = trip.DelayMinutes,
            VersionNo = trip.VersionNo,
            CustomerUserId = distinctOwners.Length == 1 ? distinctOwners[0].UserId.ToString() : string.Empty
        };
        reply.HorseIds.AddRange(distinctOwners.SelectMany(owner => owner.HorseIds).Distinct().Select(id => id.ToString()));
        reply.CustomerOwners.AddRange(distinctOwners.Select(owner =>
        {
            var customer = new TripOwner { CustomerUserId = owner.UserId.ToString() };
            customer.HorseIds.AddRange(owner.HorseIds.Select(id => id.ToString()));
            return customer;
        }));
        return reply;
    }
}
