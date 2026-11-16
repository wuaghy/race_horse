using System.Text.Json;
using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Common;
using Planning.Application.DTOs.Planning;
using Planning.Domain;

namespace Planning.Application.Services;

public class TripPlanningService(IPlanningDbContext db) : ITripPlanningService
{
    // ==========================================
    // Trips
    // ==========================================
    public async Task<TripDto> CreateTripAsync(CreateTripInput input, CancellationToken ct = default)
    {
        var existing = await db.TransportTrips.FirstOrDefaultAsync(t => t.OrderId == input.OrderId, ct);
        if (existing is not null)
        {
            ApplyRequestRoute(existing, input);
            await EnsureTripOrderAsync(existing, input, ct);
            await db.SaveChangesAsync(ct);
            return MapTrip(existing);
        }

        var count = await db.TransportTrips.CountAsync(ct);
        var tripNo = $"TRP-{DateTime.UtcNow:yyyyMMdd}-{count + 1:000}";

        var trip = new TransportTrip
        {
            TripNo = tripNo,
            OrderId = input.OrderId,
            RequestId = input.RequestId,
            OriginLocationId = input.OriginLocationId,
            DestinationLocationId = input.DestinationLocationId,
            CustomPickupLatitude = input.CustomPickupLatitude,
            CustomPickupLongitude = input.CustomPickupLongitude,
            CustomPickupLabel = input.CustomPickupLabel,
            RequestedDepartureAt = input.RequestedDepartureAt,
            RequestedArrivalAt = input.RequestedArrivalAt,
            PreferredTransportMode = NormalizeRequestedMode(input.PreferredTransportMode),
            PlannedDepartureAt = input.RequestedDepartureAt,
            PlannedArrivalAt = input.RequestedArrivalAt,
            Status = PlanningConstants.TripStatus.Planned,
            ComplianceReady = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            VersionNo = 1
        };

        db.TransportTrips.Add(trip);
        await EnsureTripOrderAsync(trip, input, ct);

        var payload = JsonSerializer.Serialize(new
        {
            tripId = trip.Id,
            orderId = trip.OrderId,
            tripNo = trip.TripNo
        });

        db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = "Planning.TripCreated",
            AggregateType = "TransportTrip",
            AggregateId = trip.Id,
            Payload = payload,
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Status = "PENDING"
        });

        try
        {
            await db.SaveChangesAsync(ct);
            return MapTrip(trip);
        }
        catch (DbUpdateException exception) when (IsDuplicateOrderTrip(exception))
        {
            // Booking.OrderCreated can create the Trip concurrently with this request.
            // The unique OrderId constraint is the final arbiter; return the winner instead
            // of surfacing a 500 when both paths race after the initial lookup.
            var concurrentlyCreated = await db.TransportTrips.AsNoTracking()
                .FirstOrDefaultAsync(item => item.OrderId == input.OrderId, ct);
            if (concurrentlyCreated is null) throw;
            return MapTrip(concurrentlyCreated);
        }
    }

    private static bool IsDuplicateOrderTrip(DbUpdateException exception)
    {
        var inner = exception.InnerException;
        if (inner is not System.Data.Common.DbException || inner.GetType().Name != "SqlException") return false;
        var number = inner.GetType().GetProperty("Number")?.GetValue(inner);
        return number is int value && value is 2601 or 2627;
    }

    private static void ApplyRequestRoute(TransportTrip trip, CreateTripInput input)
    {
        if (input.RequestId.HasValue) trip.RequestId = input.RequestId;
        if (input.OriginLocationId.HasValue) trip.OriginLocationId = input.OriginLocationId;
        if (input.DestinationLocationId.HasValue) trip.DestinationLocationId = input.DestinationLocationId;
        if (input.CustomPickupLatitude.HasValue && input.CustomPickupLongitude.HasValue)
        {
            trip.CustomPickupLatitude = input.CustomPickupLatitude;
            trip.CustomPickupLongitude = input.CustomPickupLongitude;
            trip.CustomPickupLabel = input.CustomPickupLabel;
        }
        if (input.RequestedDepartureAt.HasValue) trip.RequestedDepartureAt = input.RequestedDepartureAt;
        if (input.RequestedArrivalAt.HasValue) trip.RequestedArrivalAt = input.RequestedArrivalAt;
        if (NormalizeRequestedMode(input.PreferredTransportMode) is { } mode) trip.PreferredTransportMode = mode;
        if (input.RequestedDepartureAt.HasValue) trip.PlannedDepartureAt = input.RequestedDepartureAt;
        if (input.RequestedArrivalAt.HasValue) trip.PlannedArrivalAt = input.RequestedArrivalAt;
        trip.UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeRequestedMode(string? mode) => mode?.Trim().ToUpperInvariant() switch
    {
        PlanningConstants.TransportMode.Road => PlanningConstants.TransportMode.Road,
        PlanningConstants.TransportMode.Air => PlanningConstants.TransportMode.Air,
        "MIXED" => PlanningConstants.TransportMode.Road,
        _ => null
    };

    private static double DistanceKm(double latitudeA, double longitudeA, double latitudeB, double longitudeB)
    {
        const double earthRadiusKm = 6371.0088;
        var latA = latitudeA * Math.PI / 180;
        var latB = latitudeB * Math.PI / 180;
        var latDelta = latB - latA;
        var lonDelta = (longitudeB - longitudeA) * Math.PI / 180;
        var a = Math.Pow(Math.Sin(latDelta / 2), 2) + Math.Cos(latA) * Math.Cos(latB) * Math.Pow(Math.Sin(lonDelta / 2), 2);
        var clamped = Math.Clamp(a, 0d, 1d);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(clamped), Math.Sqrt(1 - clamped));
    }

    private async Task EnsureTripOrderAsync(TransportTrip trip, CreateTripInput input, CancellationToken ct)
    {
        var membership = await db.TripOrders.FirstOrDefaultAsync(order => order.OrderId == input.OrderId, ct);
        if (membership is null)
        {
            membership = new TripOrder { OrderId = input.OrderId };
            db.TripOrders.Add(membership);
        }
        membership.TripId = trip.Id;
        membership.OrderNo = input.OrderNo ?? membership.OrderNo;
        membership.RequestNo = input.RequestNo ?? membership.RequestNo;
        membership.RequestId = input.RequestId ?? trip.RequestId;
        membership.CustomerId = input.CustomerId ?? membership.CustomerId;
        membership.CustomerUserId = input.CustomerUserId ?? membership.CustomerUserId;
        membership.CustomerName = input.CustomerName ?? membership.CustomerName;
        membership.OriginLocationId = input.OriginLocationId ?? trip.OriginLocationId;
        membership.DestinationLocationId = input.DestinationLocationId ?? trip.DestinationLocationId;
        membership.CustomPickupLatitude = input.CustomPickupLatitude ?? membership.CustomPickupLatitude;
        membership.CustomPickupLongitude = input.CustomPickupLongitude ?? membership.CustomPickupLongitude;
        membership.CustomPickupLabel = input.CustomPickupLabel ?? membership.CustomPickupLabel;
        membership.RequestedDepartureAt = input.RequestedDepartureAt ?? trip.RequestedDepartureAt;
        membership.RequestedArrivalAt = input.RequestedArrivalAt ?? trip.RequestedArrivalAt;
        membership.PreferredTransportMode = NormalizeRequestedMode(input.PreferredTransportMode) ?? trip.PreferredTransportMode;
        if (input.HorseIds is { Count: > 0 })
            membership.HorseIdsJson = JsonSerializer.Serialize(input.HorseIds.Distinct().ToArray());
        if (input.HorseNames is { Count: > 0 })
            membership.HorseNamesJson = JsonSerializer.Serialize(input.HorseNames);
        membership.ReceiverName = input.ReceiverName ?? membership.ReceiverName;
        membership.ReceiverPhone = input.ReceiverPhone ?? membership.ReceiverPhone;
        membership.ReceiverEmail = input.ReceiverEmail ?? membership.ReceiverEmail;
    }

    public async Task RegisterOrderForDispatchAsync(CreateTripInput input, CancellationToken ct = default)
    {
        var order = await db.TripOrders.SingleOrDefaultAsync(item => item.OrderId == input.OrderId, ct);
        if (order is null)
        {
            order = new TripOrder { OrderId = input.OrderId, HorseIdsJson = "[]" };
            db.TripOrders.Add(order);
        }
        order.RequestId = input.RequestId ?? order.RequestId;
        order.OrderNo = input.OrderNo ?? order.OrderNo;
        order.RequestNo = input.RequestNo ?? order.RequestNo;
        order.CustomerId = input.CustomerId ?? order.CustomerId;
        order.CustomerUserId = input.CustomerUserId ?? order.CustomerUserId;
        order.CustomerName = input.CustomerName ?? order.CustomerName;
        order.OriginLocationId = input.OriginLocationId ?? order.OriginLocationId;
        order.DestinationLocationId = input.DestinationLocationId ?? order.DestinationLocationId;
        order.CustomPickupLatitude = input.CustomPickupLatitude ?? order.CustomPickupLatitude;
        order.CustomPickupLongitude = input.CustomPickupLongitude ?? order.CustomPickupLongitude;
        order.CustomPickupLabel = input.CustomPickupLabel ?? order.CustomPickupLabel;
        order.RequestedDepartureAt = input.RequestedDepartureAt ?? order.RequestedDepartureAt;
        order.RequestedArrivalAt = input.RequestedArrivalAt ?? order.RequestedArrivalAt;
        order.PreferredTransportMode = NormalizeRequestedMode(input.PreferredTransportMode) ?? order.PreferredTransportMode;
        if (input.HorseIds is { Count: > 0 }) order.HorseIdsJson = JsonSerializer.Serialize(input.HorseIds.Distinct().ToArray());
        if (input.HorseNames is { Count: > 0 }) order.HorseNamesJson = JsonSerializer.Serialize(input.HorseNames);
        order.ReceiverName = input.ReceiverName ?? order.ReceiverName;
        order.ReceiverPhone = input.ReceiverPhone ?? order.ReceiverPhone;
        order.ReceiverEmail = input.ReceiverEmail ?? order.ReceiverEmail;
        order.PaymentStatus = string.IsNullOrWhiteSpace(input.PaymentStatus) ? order.PaymentStatus : input.PaymentStatus.Trim().ToUpperInvariant();
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DispatchCalendarDayDto>> GetDispatchCalendarAsync(int year, int month, CancellationToken ct = default)
    {
        if (month is < 1 or > 12) throw new BusinessRuleException("Month must be between 1 and 12.", "INVALID_CALENDAR_MONTH");
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        var orders = await db.TripOrders.AsNoTracking()
            .Where(order => order.RequestedDepartureAt >= start && order.RequestedDepartureAt < end)
            .Select(order => new { order.RequestedDepartureAt, order.TripId })
            .ToListAsync(ct);
        var trips = await db.TransportTrips.AsNoTracking()
            .Where(trip => trip.MergedIntoTripId == null && trip.Status != PlanningConstants.TripStatus.Consolidated && trip.PlannedDepartureAt >= start && trip.PlannedDepartureAt < end)
            .Select(trip => trip.PlannedDepartureAt)
            .ToListAsync(ct);
        var ordersByDate = orders.GroupBy(order => DateOnly.FromDateTime(order.RequestedDepartureAt!.Value)).ToDictionary(group => group.Key, group => group.ToList());
        var tripsByDate = trips.GroupBy(departure => DateOnly.FromDateTime(departure!.Value)).ToDictionary(group => group.Key, group => group.Count());
        return ordersByDate.Keys.Union(tripsByDate.Keys).OrderBy(date => date)
            .Select(date => new DispatchCalendarDayDto(date, ordersByDate.GetValueOrDefault(date)?.Count ?? 0, tripsByDate.GetValueOrDefault(date), ordersByDate.GetValueOrDefault(date)?.Count(order => !order.TripId.HasValue) ?? 0))
            .ToList();
    }

    public async Task<IReadOnlyList<TripOrderDto>> GetDispatchOrdersAsync(DateOnly date, CancellationToken ct = default)
    {
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = start.AddDays(1);
        var orders = await db.TripOrders.AsNoTracking()
            .Where(order => order.TripId == null && order.RequestedDepartureAt >= start && order.RequestedDepartureAt < end)
            .OrderBy(order => order.RequestedDepartureAt).ToListAsync(ct);
        return orders.Select(MapTripOrder).ToList();
    }

    public async Task<IReadOnlyList<TripOrderDto>> SearchDispatchOrdersAsync(string search, CancellationToken ct = default)
    {
        search = search?.Trim() ?? string.Empty;
        if (search.Length == 0) return [];
        var id = Guid.TryParse(search, out var parsedId) ? parsedId : Guid.Empty;
        var orders = await db.TripOrders.AsNoTracking()
            .Where(order => order.TripId == null && order.RequestedDepartureAt.HasValue &&
                (order.OrderId == id || order.RequestId == id ||
                 (order.OrderNo != null && order.OrderNo.Contains(search)) ||
                 (order.RequestNo != null && order.RequestNo.Contains(search)) ||
                 (order.CustomerName != null && order.CustomerName.Contains(search)) || order.HorseNamesJson.Contains(search)))
            .OrderByDescending(order => order.RequestedDepartureAt).ThenBy(order => order.OrderId)
            .Take(1).ToListAsync(ct);
        return orders.Select(MapTripOrder).ToList();
    }

    public async Task<DispatchTripDto> CreateDispatchTripAsync(CreateDispatchTripInput input, CancellationToken ct = default)
    {
        var ids = input.OrderIds.Distinct().ToArray();
        if (ids.Length == 0) throw new BusinessRuleException("Select at least one order for the dispatch trip.", "DISPATCH_TRIP_ORDERS_REQUIRED");
        await using var transaction = await db.BeginTransactionAsync(ct);
        var orders = await db.TripOrders.Where(order => ids.Contains(order.OrderId)).OrderBy(order => order.RequestedDepartureAt).ToListAsync(ct);
        if (orders.Count != ids.Length || orders.Any(order => order.TripId.HasValue))
            throw new BusinessRuleException("One or more orders are missing or already assigned to a trip.", "DISPATCH_ORDER_ALREADY_ASSIGNED");
        if (orders.Any(order => !string.Equals(order.PaymentStatus, "PAID", StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleException("Customer payment is required before an order can be assigned to a trip.", "DISPATCH_PAYMENT_REQUIRED");
        var anchor = orders[0];
        if (orders.Any(order => order.RequestedDepartureAt?.Date != anchor.RequestedDepartureAt?.Date ||
            order.OriginLocationId != anchor.OriginLocationId || order.DestinationLocationId != anchor.DestinationLocationId ||
            order.CustomPickupLatitude != anchor.CustomPickupLatitude || order.CustomPickupLongitude != anchor.CustomPickupLongitude ||
            NormalizeRequestedMode(order.PreferredTransportMode) != NormalizeRequestedMode(anchor.PreferredTransportMode) ||
            order.CustomerId != anchor.CustomerId || !string.Equals(order.ReceiverEmail, anchor.ReceiverEmail, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleException("Orders must share the departure date, pickup, destination, mode and handover recipient.", "DISPATCH_ORDERS_NOT_COMPATIBLE");

        var sequence = await db.TransportTrips.CountAsync(ct) + 1;
        var trip = new TransportTrip
        {
            TripNo = $"TRP-{DateTime.UtcNow:yyyyMMdd}-{sequence:000}",
            OrderId = null,
            RequestId = null,
            OriginLocationId = anchor.OriginLocationId,
            DestinationLocationId = anchor.DestinationLocationId,
            CustomPickupLatitude = anchor.CustomPickupLatitude,
            CustomPickupLongitude = anchor.CustomPickupLongitude,
            CustomPickupLabel = anchor.CustomPickupLabel,
            RequestedDepartureAt = anchor.RequestedDepartureAt,
            RequestedArrivalAt = orders.Max(order => order.RequestedArrivalAt),
            PreferredTransportMode = anchor.PreferredTransportMode,
            PlannedDepartureAt = anchor.RequestedDepartureAt,
            PlannedArrivalAt = orders.Max(order => order.RequestedArrivalAt),
            Status = PlanningConstants.TripStatus.Planned,
            ComplianceReady = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            VersionNo = 1
        };
        db.TransportTrips.Add(trip);
        foreach (var order in orders) order.TripId = trip.Id;
        var orderIds = orders.Select(order => order.OrderId).ToArray();
        db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = "Planning.TripCreated",
            AggregateType = "TransportTrip",
            AggregateId = trip.Id,
            Payload = JsonSerializer.Serialize(new { tripId = trip.Id, tripNo = trip.TripNo, orderIds }),
            CorrelationId = Guid.NewGuid(), OccurredAt = DateTime.UtcNow, Status = "PENDING", CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new DispatchTripDto(MapTrip(trip, orders.Count), orders.Select(MapTripOrder).ToList());
    }

    public async Task<TripDto> GetTripByIdAsync(Guid id, CancellationToken ct = default)
    {
        var trip = await db.TransportTrips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Trip", id);
        if (trip.MergedIntoTripId.HasValue)
            trip = await db.TransportTrips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trip.MergedIntoTripId.Value, ct) ?? trip;
        var orderCount = await db.TripOrders.CountAsync(order => order.TripId == trip.Id, ct);
        return MapTrip(trip, Math.Max(orderCount, 1));
    }

    public async Task<RoutePlanDto> GetRoutePlanByTripAsync(Guid tripId, CancellationToken ct = default)
    {
        var plan = await db.RoutePlans.AsNoTracking().FirstOrDefaultAsync(item => item.TripId == tripId, ct)
            ?? throw new NotFoundException("Route plan for trip", tripId);
        return MapPlan(plan);
    }

    public async Task<PagedResult<TripDto>> GetTripsAsync(PaginationFilter filter, string? status = null, string? sortBy = null, string? sortDirection = null, CancellationToken ct = default, DateOnly? date = null)
    {
        var query = db.TransportTrips.AsNoTracking().Where(t => t.MergedIntoTripId == null && t.Status != PlanningConstants.TripStatus.Consolidated);
        if (date.HasValue)
        {
            var dayStart = date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(t => t.PlannedDepartureAt >= dayStart && t.PlannedDepartureAt < dayEnd);
        }
        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            if (Guid.TryParse(search, out var tripId))
            {
                query = query.Where(t => t.Id == tripId || t.TripNo.Contains(search) ||
                    db.TripOrders.Any(order => order.TripId == t.Id && (order.OrderId == tripId || order.RequestId == tripId)) ||
                    db.TripOrders.Any(order => order.TripId == t.Id &&
                        ((order.OrderNo != null && order.OrderNo.Contains(search)) || (order.RequestNo != null && order.RequestNo.Contains(search)) ||
                         (order.CustomerName != null && order.CustomerName.Contains(search)) || order.HorseNamesJson.Contains(search))));
            }
            else
            {
                query = query.Where(t => t.TripNo.Contains(search) ||
                    db.TripOrders.Any(order => order.TripId == t.Id &&
                        ((order.OrderNo != null && order.OrderNo.Contains(search)) || (order.RequestNo != null && order.RequestNo.Contains(search)) ||
                         (order.CustomerName != null && order.CustomerName.Contains(search)) || order.HorseNamesJson.Contains(search))));
            }
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            query = query.Where(t => t.Status.ToUpper() == normalizedStatus);
        }

        var total = await query.LongCountAsync(ct);
        var page = filter.NormalizedPage;
        var size = filter.NormalizedPageSize;
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        var normalizedSort = sortBy?.Trim().ToLowerInvariant();
        IOrderedQueryable<TransportTrip> ordered = normalizedSort switch
        {
            "tripno" or "trip" => descending
                ? query.OrderByDescending(t => t.TripNo).ThenByDescending(t => t.Id)
                : query.OrderBy(t => t.TripNo).ThenBy(t => t.Id),
            "status" => descending
                ? query.OrderByDescending(t => t.Status).ThenByDescending(t => t.PlannedDepartureAt ?? t.CreatedAt)
                : query.OrderBy(t => t.Status).ThenBy(t => t.PlannedDepartureAt ?? t.CreatedAt),
            "createdat" or "created" => descending
                ? query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
                : query.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id),
            _ => descending
                ? query.OrderByDescending(t => t.PlannedDepartureAt ?? t.CreatedAt).ThenByDescending(t => t.CreatedAt)
                : query.OrderBy(t => t.PlannedDepartureAt ?? t.CreatedAt).ThenBy(t => t.CreatedAt)
        };
        var items = await ordered
            .Skip(page * size)
            .Take(size)
            .ToListAsync(ct);

        var ids = items.Select(item => item.Id).ToArray();
        var orderCounts = ids.Length == 0
            ? new Dictionary<Guid, int>()
            : await db.TripOrders.AsNoTracking().Where(order => order.TripId.HasValue && ids.Contains(order.TripId.Value))
                .GroupBy(order => order.TripId!.Value).Select(group => new { TripId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(group => group.TripId, group => group.Count, ct);
        var orderSummaries = ids.Length == 0 ? [] : await db.TripOrders.AsNoTracking()
            .Where(order => order.TripId.HasValue && ids.Contains(order.TripId.Value))
            .Select(order => new { order.TripId, order.CustomerName, order.HorseNamesJson })
            .ToListAsync(ct);
        var summariesByTrip = orderSummaries.GroupBy(order => order.TripId!.Value).ToDictionary(group => group.Key, group =>
        {
            var customers = group.Select(order => order.CustomerName).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var horses = group.SelectMany(order => ParseHorseNames(order.HorseNamesJson).Values).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return (Customers: (IReadOnlyList<string>)customers, Horses: (IReadOnlyList<string>)horses);
        });
        return new PagedResult<TripDto>(items.Select(item =>
        {
            var summary = summariesByTrip.GetValueOrDefault(item.Id);
            return MapTrip(item, Math.Max(orderCounts.GetValueOrDefault(item.Id), 1), summary.Customers, summary.Horses);
        }).ToList(), page, size, total);
    }

    public async Task<IReadOnlyList<TripOrderDto>> GetTripOrdersAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await db.TransportTrips.AsNoTracking().FirstOrDefaultAsync(item => item.Id == tripId, ct)
            ?? throw new NotFoundException("Trip", tripId);
        var canonicalTripId = trip.MergedIntoTripId ?? trip.Id;
        var orders = await db.TripOrders.AsNoTracking().Where(order => order.TripId == canonicalTripId).OrderBy(order => order.OrderId).ToListAsync(ct);
        if (orders.Count == 0)
        {
            var primary = await db.TransportTrips.AsNoTracking().FirstOrDefaultAsync(item => item.Id == canonicalTripId, ct);
            return primary?.OrderId is not Guid primaryOrderId ? [] : [new TripOrderDto(primaryOrderId, primary.RequestId, null, primary.OriginLocationId,
                primary.DestinationLocationId, primary.RequestedDepartureAt, primary.RequestedArrivalAt, primary.PreferredTransportMode, [], null, null, null, null)];
        }
        return orders.Select(MapTripOrder).ToList();
    }

    public async Task<IReadOnlyList<TripOrderDto>> GetAssignedTripOrdersAsync(Guid tripId, Guid userId, CancellationToken ct = default)
    {
        var isAssigned = await db.StaffAssignments.AsNoTracking().AnyAsync(assignment =>
            assignment.TripId == tripId && assignment.UserId == userId && assignment.ReleasedAt == null, ct);
        if (!isAssigned)
            throw new BusinessRuleException("This trip is not assigned to your account.", "TRIP_NOT_ASSIGNED", "tripId");

        return await GetTripOrdersAsync(tripId, ct);
    }

    public async Task<PagedResult<TripDto>> GetAssignedTripsAsync(Guid userId, PaginationFilter filter, string? status = null, string? sortBy = null, string? sortDirection = null, CancellationToken ct = default)
    {
        var query = db.TransportTrips.AsNoTracking()
            .Where(trip => db.StaffAssignments.Any(assignment =>
                assignment.TripId == trip.Id && assignment.UserId == userId && assignment.ReleasedAt == null));

        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = Guid.TryParse(search, out var tripId)
                ? query.Where(trip => trip.Id == tripId || trip.TripNo.Contains(search))
                : query.Where(trip => trip.TripNo.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            query = query.Where(trip => trip.Status.ToUpper() == normalizedStatus);
        }

        var total = await query.LongCountAsync(ct);
        var page = filter.NormalizedPage;
        var size = filter.NormalizedPageSize;
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        var normalizedSort = sortBy?.Trim().ToLowerInvariant();
        IOrderedQueryable<TransportTrip> ordered = normalizedSort switch
        {
            "tripno" or "trip" => descending
                ? query.OrderByDescending(trip => trip.TripNo).ThenByDescending(trip => trip.Id)
                : query.OrderBy(trip => trip.TripNo).ThenBy(trip => trip.Id),
            "status" => descending
                ? query.OrderByDescending(trip => trip.Status).ThenByDescending(trip => trip.PlannedDepartureAt ?? trip.CreatedAt)
                : query.OrderBy(trip => trip.Status).ThenBy(trip => trip.PlannedDepartureAt ?? trip.CreatedAt),
            "createdat" or "created" => descending
                ? query.OrderByDescending(trip => trip.CreatedAt).ThenByDescending(trip => trip.Id)
                : query.OrderBy(trip => trip.CreatedAt).ThenBy(trip => trip.Id),
            _ => descending
                ? query.OrderByDescending(trip => trip.PlannedDepartureAt ?? trip.CreatedAt).ThenByDescending(trip => trip.CreatedAt)
                : query.OrderBy(trip => trip.PlannedDepartureAt ?? trip.CreatedAt).ThenBy(trip => trip.CreatedAt)
        };
        var trips = await ordered.Skip(page * size).Take(size).ToListAsync(ct);

        return new PagedResult<TripDto>(trips.Select(item => MapTrip(item)).ToList(), page, size, total);
    }

    // ==========================================
    // Route Plans & Versions
    // ==========================================
    public async Task<RoutePlanDto> CreateRoutePlanAsync(Guid tripId, Guid actorUserId, CancellationToken ct = default)
    {
        var trip = await db.TransportTrips.FirstOrDefaultAsync(t => t.Id == tripId, ct)
            ?? throw new NotFoundException("Trip", tripId);

        var plan = await db.RoutePlans.FirstOrDefaultAsync(p => p.TripId == tripId, ct);
        if (plan is not null && await db.RoutePlanVersions.AnyAsync(v => v.RoutePlanId == plan.Id, ct))
            throw new BusinessRuleException("Trip already has a route plan.", "ROUTE_PLAN_ALREADY_EXISTS");

        if (plan is null)
        {
            plan = new RoutePlan
            {
                TripId = tripId,
                ActiveVersionId = null,
                CreatedByUserId = actorUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                VersionNo = 1
            };

            db.RoutePlans.Add(plan);
            await db.SaveChangesAsync(ct);
        }

        var version = new RoutePlanVersion
        {
            RoutePlanId = plan.Id,
            VersionNo = 1,
            Status = PlanningConstants.RouteVersionStatus.Draft,
            CreatedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow
        };

        db.RoutePlanVersions.Add(version);
        // Persist the version before adding its default leg. The database enforces
        // RouteLeg -> RoutePlanVersion and the EF model does not expose a navigation
        // relationship that would otherwise guarantee this insert ordering.
        await db.SaveChangesAsync(ct);
        if (trip.OriginLocationId is Guid originId && trip.DestinationLocationId is Guid destinationId && originId != destinationId)
        {
            db.RouteLegs.Add(new RouteLeg
            {
                RoutePlanVersionId = version.Id,
                SequenceNo = 1,
                OriginLocationId = originId,
                DestinationLocationId = destinationId,
                TransportMode = NormalizeRequestedMode(trip.PreferredTransportMode) ?? PlanningConstants.TransportMode.Road,
                PlannedDepartureAt = trip.RequestedDepartureAt,
                PlannedArrivalAt = trip.RequestedArrivalAt,
                Status = "PLANNED"
            });
        }
        await db.SaveChangesAsync(ct);

        return MapPlan(plan);
    }

    public async Task<IReadOnlyList<RoutePlanVersionDto>> GetVersionsAsync(Guid planId, CancellationToken ct = default)
    {
        if (!await db.RoutePlans.AnyAsync(p => p.Id == planId, ct))
            throw new NotFoundException("RoutePlan", planId);

        return await db.RoutePlanVersions.AsNoTracking()
            .Where(v => v.RoutePlanId == planId)
            .OrderBy(v => v.VersionNo)
            .Select(v => MapVersion(v))
            .ToListAsync(ct);
    }

    public async Task<RoutePlanVersionDto> CreateNewVersionAsync(Guid planId, Guid actorUserId, string? reason = null, CancellationToken ct = default)
    {
        var plan = await db.RoutePlans.FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new NotFoundException("RoutePlan", planId);

        var latestVersionNo = await db.RoutePlanVersions
            .Where(v => v.RoutePlanId == planId)
            .MaxAsync(v => (int?)v.VersionNo, ct) ?? 0;

        var newVersion = new RoutePlanVersion
        {
            RoutePlanId = planId,
            VersionNo = latestVersionNo + 1,
            Status = PlanningConstants.RouteVersionStatus.Draft,
            Reason = reason?.Trim(),
            CreatedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow
        };

        db.RoutePlanVersions.Add(newVersion);
        var sourceVersionId = await db.RoutePlanVersions
            .Where(version => version.RoutePlanId == planId && version.Id != newVersion.Id)
            .OrderByDescending(version => version.VersionNo)
            .Select(version => (Guid?)version.Id)
            .FirstOrDefaultAsync(ct);
        if (sourceVersionId is null && plan.ActiveVersionId is Guid activeVersionId)
            sourceVersionId = activeVersionId;

        if (sourceVersionId is Guid sourceId)
        {
            var sourceLegs = await db.RouteLegs.AsNoTracking()
                .Where(leg => leg.RoutePlanVersionId == sourceId)
                .OrderBy(leg => leg.SequenceNo)
                .ToListAsync(ct);
            foreach (var sourceLeg in sourceLegs)
            {
                var copiedLeg = new RouteLeg
                {
                    RoutePlanVersionId = newVersion.Id,
                    SequenceNo = sourceLeg.SequenceNo,
                    OriginLocationId = sourceLeg.OriginLocationId,
                    DestinationLocationId = sourceLeg.DestinationLocationId,
                    TransportMode = sourceLeg.TransportMode,
                    PlannedDepartureAt = sourceLeg.PlannedDepartureAt,
                    PlannedArrivalAt = sourceLeg.PlannedArrivalAt,
                    DistanceKm = sourceLeg.DistanceKm,
                    RouteShapeJson = sourceLeg.RouteShapeJson,
                    Status = "PLANNED"
                };
                db.RouteLegs.Add(copiedLeg);

                var sourceCheckpoints = await db.Checkpoints.AsNoTracking()
                    .Where(checkpoint => checkpoint.RouteLegId == sourceLeg.Id)
                    .OrderBy(checkpoint => checkpoint.SequenceNo)
                    .ToListAsync(ct);
                foreach (var checkpoint in sourceCheckpoints)
                {
                    db.Checkpoints.Add(new Checkpoint
                    {
                        RouteLegId = copiedLeg.Id,
                        SequenceNo = checkpoint.SequenceNo,
                        LocationId = checkpoint.LocationId,
                        Type = checkpoint.Type,
                        PlannedArrivalAt = checkpoint.PlannedArrivalAt,
                        PlannedDepartureAt = checkpoint.PlannedDepartureAt,
                        Status = "PLANNED"
                    });
                }
            }
        }
        await db.SaveChangesAsync(ct);

        return MapVersion(newVersion);
    }

    public async Task<RoutePlanVersionDto> CreateRouteChangeProposalAsync(Guid tripId, Guid actorUserId, CreateRouteChangeRequestInput input, Guid? correlationId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 2000)
            throw new BusinessRuleException("A route-change reason of at most 2000 characters is required.", "ROUTE_CHANGE_REASON_REQUIRED", "reason");
        if (input.DelayEstimateMinutes < 0)
            throw new BusinessRuleException("Delay estimate cannot be negative.", "INVALID_DELAY_ESTIMATE", "delayEstimateMinutes");
        if (input.AdditionalCost < 0)
            throw new BusinessRuleException("Additional cost cannot be negative.", "INVALID_ADDITIONAL_COST", "additionalCost");
        if (string.IsNullOrWhiteSpace(input.CurrencyCode) || input.CurrencyCode.Trim().Length != 3)
            throw new BusinessRuleException("CurrencyCode must be a three-letter currency code.", "INVALID_CURRENCY_CODE", "currencyCode");

        var plan = await db.RoutePlans.SingleOrDefaultAsync(x => x.TripId == tripId, ct)
            ?? throw new BusinessRuleException("Create a route plan before proposing an emergency change.", "ROUTE_PLAN_REQUIRED", "tripId");
        var active = await db.RoutePlanVersions.AnyAsync(x => x.Id == plan.ActiveVersionId && x.Status == PlanningConstants.RouteVersionStatus.Active, ct);
        if (!active)
            throw new BusinessRuleException("An active route version is required before proposing an emergency change.", "ACTIVE_ROUTE_REQUIRED", "tripId");

        var reason = $"Emergency route change: {input.Reason.Trim()}";
        var proposal = await CreateNewVersionAsync(plan.Id, actorUserId, reason, ct);
        var version = await db.RoutePlanVersions.SingleAsync(x => x.Id == proposal.Id, ct);
        version.EstimatedDurationMinutes = input.DelayEstimateMinutes;
        version.AdditionalCost = input.AdditionalCost;
        version.CurrencyCode = input.CurrencyCode.Trim().ToUpperInvariant();
        var now = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            EntityType = "RoutePlanVersion",
            EntityId = version.Id,
            Action = "ROUTE_CHANGE_PROPOSED",
            OldState = PlanningConstants.RouteVersionStatus.Active,
            NewState = PlanningConstants.RouteVersionStatus.Draft,
            ActorUserId = actorUserId,
            Reason = reason,
            CorrelationId = correlationId,
            OccurredAt = now
        });
        await db.SaveChangesAsync(ct);
        return MapVersion(version);
    }

    // ==========================================
    // Route Legs
    // ==========================================
    public async Task<IReadOnlyList<RouteLegDto>> GetLegsAsync(Guid versionId, CancellationToken ct = default)
    {
        if (!await db.RoutePlanVersions.AnyAsync(v => v.Id == versionId, ct))
            throw new NotFoundException("RoutePlanVersion", versionId);

        return await db.RouteLegs.AsNoTracking()
            .Where(l => l.RoutePlanVersionId == versionId)
            .OrderBy(l => l.SequenceNo)
            .Select(l => MapLeg(l))
            .ToListAsync(ct);
    }

    public async Task<RouteLegDto> AddLegAsync(Guid versionId, RouteLegInput input, CancellationToken ct = default)
    {
        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", versionId);

        AssertVersionIsDraft(version);
        await ValidateLegLocationsAndTimesAsync(input, ct);

        if (await db.RouteLegs.AnyAsync(l => l.RoutePlanVersionId == versionId && l.SequenceNo == input.SequenceNo, ct))
            throw new BusinessRuleException($"Route leg sequence '{input.SequenceNo}' already exists in this version.", "DUPLICATE_SEQUENCE_NO", "sequenceNo");

        var leg = new RouteLeg
        {
            RoutePlanVersionId = versionId,
            SequenceNo = input.SequenceNo,
            OriginLocationId = input.OriginLocationId,
            DestinationLocationId = input.DestinationLocationId,
            TransportMode = input.Mode.Trim().ToUpperInvariant(),
            PlannedDepartureAt = input.DepartureAt,
            PlannedArrivalAt = input.ArrivalAt,
            DistanceKm = input.DistanceKm,
            Status = "PLANNED"
        };

        db.RouteLegs.Add(leg);
        await db.SaveChangesAsync(ct);

        return MapLeg(leg);
    }

    public async Task<RouteLegDto> UpdateLegAsync(Guid legId, RouteLegInput input, CancellationToken ct = default)
    {
        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == legId, ct)
            ?? throw new NotFoundException("RouteLeg", legId);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        AssertVersionIsDraft(version);
        await ValidateLegLocationsAndTimesAsync(input, ct);

        if (await db.RouteLegs.AnyAsync(l => l.Id != legId && l.RoutePlanVersionId == leg.RoutePlanVersionId && l.SequenceNo == input.SequenceNo, ct))
            throw new BusinessRuleException($"Route leg sequence '{input.SequenceNo}' already exists in this version.", "DUPLICATE_SEQUENCE_NO", "sequenceNo");

        leg.SequenceNo = input.SequenceNo;
        leg.OriginLocationId = input.OriginLocationId;
        leg.DestinationLocationId = input.DestinationLocationId;
        leg.TransportMode = input.Mode.Trim().ToUpperInvariant();
        leg.PlannedDepartureAt = input.DepartureAt;
        leg.PlannedArrivalAt = input.ArrivalAt;
        leg.DistanceKm = input.DistanceKm;

        await db.SaveChangesAsync(ct);
        return MapLeg(leg);
    }

    public async Task<RouteLegDto> UpdateRouteShapeAsync(Guid legId, RouteShapeInput input, CancellationToken ct = default)
    {
        var leg = await db.RouteLegs.FirstOrDefaultAsync(item => item.Id == legId, ct) ?? throw new NotFoundException("RouteLeg", legId);
        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(item => item.Id == leg.RoutePlanVersionId, ct) ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);
        AssertVersionIsDraft(version);
        if (input.Points is null || input.Points.Count > 20 || input.Points.Any(point => point is null || !double.IsFinite(point.Latitude) || !double.IsFinite(point.Longitude) || Math.Abs(point.Latitude) > 85.05112878 || Math.Abs(point.Longitude) > 180))
            throw new BusinessRuleException("Provide at most 20 valid route points.", "INVALID_ROUTE_SHAPE");
        leg.RouteShapeJson = JsonSerializer.Serialize(input.Points);
        await db.SaveChangesAsync(ct);
        return MapLeg(leg);
    }

    public async Task<IReadOnlyList<RouteTemplateDto>> GetRouteTemplatesAsync(Guid? originLocationId, Guid? destinationLocationId, string? mode, decimal? pickupLatitude, decimal? pickupLongitude, CancellationToken ct = default)
    {
        var normalizedMode = mode?.Trim().ToUpperInvariant();
        var query = db.RouteTemplates.AsNoTracking().AsQueryable();
        if (originLocationId.HasValue) query = query.Where(item => item.OriginLocationId == originLocationId);
        if (destinationLocationId.HasValue) query = query.Where(item => item.DestinationLocationId == destinationLocationId);
        if (!string.IsNullOrEmpty(normalizedMode)) query = query.Where(item => item.TransportMode == normalizedMode);
        if (originLocationId.HasValue || destinationLocationId.HasValue)
            query = query.Where(item => (item.CustomPickupLatitude == null && item.CustomPickupLongitude == null) || (item.CustomPickupLatitude == pickupLatitude && item.CustomPickupLongitude == pickupLongitude));
        var rows = await query.OrderBy(item => item.Name).ThenBy(item => item.Id).ToListAsync(ct);
        return rows.Select(item => new RouteTemplateDto(item.Id, item.Name, item.OriginLocationId, item.DestinationLocationId, item.TransportMode, ParseRouteShape(item.RouteShapeJson), item.CreatedAt, item.CustomPickupLatitude, item.CustomPickupLongitude)).ToList();
    }

    public async Task<RouteTemplateDto> CreateRouteTemplateAsync(RouteTemplateInput input, Guid actorUserId, CancellationToken ct = default)
    {
        var name = input.Name?.Trim();
        var mode = input.TransportMode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            throw new BusinessRuleException("Enter a route template name of up to 120 characters.", "INVALID_ROUTE_TEMPLATE_NAME");
        if (mode is not ("ROAD" or "AIR") || input.OriginLocationId == input.DestinationLocationId || input.OriginLocationId == Guid.Empty || input.DestinationLocationId == Guid.Empty)
            throw new BusinessRuleException("Choose different origin and destination locations and a valid transport mode.", "INVALID_ROUTE_TEMPLATE_ROUTE");
        if (input.CustomPickupLatitude.HasValue != input.CustomPickupLongitude.HasValue || input.CustomPickupLatitude is < -85.05112878m or > 85.05112878m || input.CustomPickupLongitude is < -180m or > 180m)
            throw new BusinessRuleException("Custom pickup coordinates are invalid.", "INVALID_ROUTE_TEMPLATE_PICKUP");
        if (input.Points is null || input.Points.Count > 20 || input.Points.Any(point => point is null || !double.IsFinite(point.Latitude) || !double.IsFinite(point.Longitude) || Math.Abs(point.Latitude) > 85.05112878 || Math.Abs(point.Longitude) > 180))
            throw new BusinessRuleException("Provide at most 20 valid route points.", "INVALID_ROUTE_SHAPE");
        var count = await db.Locations.CountAsync(location => (location.Id == input.OriginLocationId || location.Id == input.DestinationLocationId) && location.Active, ct);
        if (count != 2) throw new BusinessRuleException("Both route locations must be active.", "INVALID_ROUTE_TEMPLATE_LOCATION");
        if (await db.RouteTemplates.AnyAsync(item => item.Name == name && item.OriginLocationId == input.OriginLocationId && item.DestinationLocationId == input.DestinationLocationId && item.TransportMode == mode && item.CustomPickupLatitude == input.CustomPickupLatitude && item.CustomPickupLongitude == input.CustomPickupLongitude, ct))
            throw new BusinessRuleException("A template with this name already exists for this route.", "ROUTE_TEMPLATE_NAME_EXISTS");
        var template = new RouteTemplate { Name = name, OriginLocationId = input.OriginLocationId, DestinationLocationId = input.DestinationLocationId, TransportMode = mode, CustomPickupLatitude = input.CustomPickupLatitude, CustomPickupLongitude = input.CustomPickupLongitude, RouteShapeJson = JsonSerializer.Serialize(input.Points), CreatedByUserId = actorUserId, CreatedAt = DateTime.UtcNow };
        db.RouteTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return new RouteTemplateDto(template.Id, template.Name, template.OriginLocationId, template.DestinationLocationId, template.TransportMode, ParseRouteShape(template.RouteShapeJson), template.CreatedAt, template.CustomPickupLatitude, template.CustomPickupLongitude);
    }

    public async Task DeleteLegAsync(Guid legId, CancellationToken ct = default)
    {
        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == legId, ct)
            ?? throw new NotFoundException("RouteLeg", legId);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        AssertVersionIsDraft(version);

        if (await db.Checkpoints.AnyAsync(c => c.RouteLegId == legId, ct) ||
            await db.TripLegAssignments.AnyAsync(a => a.RouteLegId == legId, ct) ||
            await db.HorseLegAssignments.AnyAsync(h => h.RouteLegId == legId, ct))
        {
            throw new BusinessRuleException("Cannot delete route leg because it has dependent checkpoints or assignments.", "LEG_IN_USE");
        }

        db.RouteLegs.Remove(leg);
        await db.SaveChangesAsync(ct);
    }

    // ==========================================
    // Checkpoints
    // ==========================================
    public async Task<IReadOnlyList<CheckpointDto>> GetCheckpointsAsync(Guid legId, CancellationToken ct = default)
    {
        if (!await db.RouteLegs.AnyAsync(l => l.Id == legId, ct))
            throw new NotFoundException("RouteLeg", legId);

        return await db.Checkpoints.AsNoTracking()
            .Where(c => c.RouteLegId == legId)
            .OrderBy(c => c.SequenceNo)
            .Select(c => MapCheckpoint(c))
            .ToListAsync(ct);
    }

    public async Task<CheckpointDto> AddCheckpointAsync(Guid legId, CheckpointInput input, CancellationToken ct = default)
    {
        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == legId, ct)
            ?? throw new NotFoundException("RouteLeg", legId);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        AssertVersionIsDraft(version);

        if (!await db.Locations.AnyAsync(loc => loc.Id == input.LocationId, ct))
            throw new NotFoundException("Location", input.LocationId);

        if (input.ArrivalAt.HasValue && input.DepartureAt.HasValue && input.DepartureAt.Value < input.ArrivalAt.Value)
            throw new BusinessRuleException("Checkpoint departure time cannot be earlier than arrival time.", "INVALID_CHECKPOINT_TIMES");

        if (await db.Checkpoints.AnyAsync(c => c.RouteLegId == legId && c.SequenceNo == input.SequenceNo, ct))
            throw new BusinessRuleException($"Checkpoint sequence '{input.SequenceNo}' already exists in this leg.", "DUPLICATE_CHECKPOINT_SEQUENCE");

        var checkpoint = new Checkpoint
        {
            RouteLegId = legId,
            SequenceNo = input.SequenceNo,
            LocationId = input.LocationId,
            Type = input.Type.Trim().ToUpperInvariant(),
            PlannedArrivalAt = input.ArrivalAt,
            PlannedDepartureAt = input.DepartureAt,
            Status = "PLANNED"
        };

        db.Checkpoints.Add(checkpoint);
        await db.SaveChangesAsync(ct);

        return MapCheckpoint(checkpoint);
    }

    public async Task<CheckpointDto> UpdateCheckpointAsync(Guid checkpointId, CheckpointInput input, CancellationToken ct = default)
    {
        var checkpoint = await db.Checkpoints.FirstOrDefaultAsync(c => c.Id == checkpointId, ct)
            ?? throw new NotFoundException("Checkpoint", checkpointId);

        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == checkpoint.RouteLegId, ct)
            ?? throw new NotFoundException("RouteLeg", checkpoint.RouteLegId);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        AssertVersionIsDraft(version);

        if (!await db.Locations.AnyAsync(loc => loc.Id == input.LocationId, ct))
            throw new NotFoundException("Location", input.LocationId);

        if (input.ArrivalAt.HasValue && input.DepartureAt.HasValue && input.DepartureAt.Value < input.ArrivalAt.Value)
            throw new BusinessRuleException("Checkpoint departure time cannot be earlier than arrival time.", "INVALID_CHECKPOINT_TIMES");

        if (await db.Checkpoints.AnyAsync(c => c.Id != checkpointId && c.RouteLegId == checkpoint.RouteLegId && c.SequenceNo == input.SequenceNo, ct))
            throw new BusinessRuleException($"Checkpoint sequence '{input.SequenceNo}' already exists in this leg.", "DUPLICATE_CHECKPOINT_SEQUENCE");

        checkpoint.SequenceNo = input.SequenceNo;
        checkpoint.LocationId = input.LocationId;
        checkpoint.Type = input.Type.Trim().ToUpperInvariant();
        checkpoint.PlannedArrivalAt = input.ArrivalAt;
        checkpoint.PlannedDepartureAt = input.DepartureAt;

        await db.SaveChangesAsync(ct);
        return MapCheckpoint(checkpoint);
    }

    public async Task DeleteCheckpointAsync(Guid checkpointId, CancellationToken ct = default)
    {
        var checkpoint = await db.Checkpoints.FirstOrDefaultAsync(c => c.Id == checkpointId, ct)
            ?? throw new NotFoundException("Checkpoint", checkpointId);

        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == checkpoint.RouteLegId, ct)
            ?? throw new NotFoundException("RouteLeg", checkpoint.RouteLegId);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        AssertVersionIsDraft(version);

        db.Checkpoints.Remove(checkpoint);
        await db.SaveChangesAsync(ct);
    }

    // ==========================================
    // Assignments (Resource, Staff, Horse)
    // ==========================================
    public async Task<ResourceAssignmentDto> AssignResourceAsync(Guid legId, ResourceInput input, CancellationToken ct = default)
    {
        await using var tx = await db.BeginTransactionAsync(ct);

        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == legId, ct)
            ?? throw new NotFoundException("RouteLeg", legId);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        await EnsureAssignableRouteVersionAsync(version, ct);
        var tripId = await db.RoutePlans.Where(plan => plan.Id == version.RoutePlanId)
            .Select(plan => plan.TripId)
            .SingleAsync(ct);

        if ((input.VehicleId is null) == (input.FlightBookingId is null))
            throw new BusinessRuleException("Must provide exactly one of vehicleId or flightBookingId.", "INVALID_RESOURCE_EXCLUSIVE");

        if (input.VehicleId.HasValue)
        {
            if (leg.TransportMode != PlanningConstants.TransportMode.Road)
                throw new BusinessRuleException("Vehicle assignment is only permitted for ROAD transport mode.", "MODE_MISMATCH");

            var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == input.VehicleId.Value, ct)
                ?? throw new NotFoundException("Vehicle", input.VehicleId.Value);

            if (vehicle.Status != PlanningConstants.VehicleStatus.Available)
                throw new BusinessRuleException($"Vehicle is currently '{vehicle.Status}' and cannot be assigned.", "VEHICLE_NOT_AVAILABLE");

            // Check if current horses on leg exceed vehicle capacity
            var currentHorses = await db.HorseLegAssignments.CountAsync(h => h.RouteLegId == legId, ct);
            if (currentHorses > vehicle.HorseCapacity)
                throw new BusinessRuleException($"Cannot assign vehicle with capacity {vehicle.HorseCapacity} because leg already has {currentHorses} horses assigned.", "VEHICLE_CAPACITY_EXCEEDED");
            if (currentHorses > vehicle.StallCapacity)
                throw new BusinessRuleException($"Cannot assign vehicle with stall capacity {vehicle.StallCapacity} because leg already has {currentHorses} horses assigned.", "VEHICLE_STALL_CAPACITY_EXCEEDED");

            // Overlap check
            if (leg.PlannedDepartureAt.HasValue && leg.PlannedArrivalAt.HasValue)
            {
                var dep = leg.PlannedDepartureAt.Value;
                var arr = leg.PlannedArrivalAt.Value;

                var isConflict = await db.TripLegAssignments
                    .Where(a => a.VehicleId == vehicle.Id && a.RouteLegId != legId)
                    .Join(db.RouteLegs, assignment => assignment.RouteLegId, otherLeg => otherLeg.Id, (assignment, otherLeg) => new { assignment, otherLeg })
                    .Join(db.RoutePlanVersions, row => row.otherLeg.RoutePlanVersionId, otherVersion => otherVersion.Id, (row, otherVersion) => new { row.assignment, row.otherLeg, otherVersion.Id, otherVersion.Status, otherVersion.RoutePlanId })
                    .Join(db.RoutePlans, row => row.RoutePlanId, plan => plan.Id, (row, plan) => new { row.assignment, row.otherLeg, row.Id, row.Status, plan.TripId, plan.ActiveVersionId })
                    .AnyAsync(row => (row.otherLeg.RoutePlanVersionId == version.Id ||
                        (row.TripId != tripId && (row.Status == PlanningConstants.RouteVersionStatus.Draft ||
                            (row.Status == PlanningConstants.RouteVersionStatus.Active && row.ActiveVersionId == row.Id)))) &&
                        row.otherLeg.PlannedDepartureAt.HasValue && row.otherLeg.PlannedArrivalAt.HasValue &&
                        row.otherLeg.PlannedDepartureAt.Value < arr && dep < row.otherLeg.PlannedArrivalAt.Value &&
                        !db.TransportTrips.Any(otherTrip => otherTrip.Id == row.TripId &&
                            (otherTrip.Status == PlanningConstants.TripStatus.Completed || otherTrip.Status == PlanningConstants.TripStatus.Cancelled)), ct);

                if (isConflict)
                    throw new BusinessRuleException("Vehicle is already assigned to another route leg during this period.", "VEHICLE_DOUBLE_BOOKED");
            }
        }
        else if (input.FlightBookingId.HasValue)
        {
            if (leg.TransportMode != PlanningConstants.TransportMode.Air)
                throw new BusinessRuleException("Flight booking assignment is only permitted for AIR transport mode.", "MODE_MISMATCH");

            var flight = await db.FlightBookings.FirstOrDefaultAsync(f => f.Id == input.FlightBookingId.Value, ct)
                ?? throw new NotFoundException("FlightBooking", input.FlightBookingId.Value);

            if (flight.ConfirmationStatus != "CONFIRMED")
                throw new BusinessRuleException("Flight booking must be manually confirmed before it can be assigned.", "FLIGHT_BOOKING_NOT_CONFIRMED");

            if (flight.OriginAirportId != leg.OriginLocationId || flight.DestinationAirportId != leg.DestinationLocationId)
                throw new BusinessRuleException("Flight booking origin/destination must match the route leg airports.", "AIRPORT_MISMATCH");

            if (await db.TripLegAssignments.AnyAsync(assignment => assignment.FlightBookingId == flight.Id && assignment.RouteLegId != legId, ct))
                throw new BusinessRuleException("Flight booking is already assigned to another route leg.", "FLIGHT_BOOKING_ALREADY_ASSIGNED");

            if ((leg.PlannedDepartureAt.HasValue && flight.ScheduledDepartureAt < leg.PlannedDepartureAt.Value) ||
                (leg.PlannedArrivalAt.HasValue && flight.ScheduledArrivalAt > leg.PlannedArrivalAt.Value))
                throw new BusinessRuleException("Flight booking schedule must fit within the planned route leg window.", "FLIGHT_SCHEDULE_MISMATCH");
        }

        // Replace any existing assignment on this leg
        var existing = await db.TripLegAssignments.Where(a => a.RouteLegId == legId).ToListAsync(ct);
        if (existing.Count > 0)
        {
            db.TripLegAssignments.RemoveRange(existing);
        }

        var assignment = new TripLegAssignment
        {
            RouteLegId = legId,
            VehicleId = input.VehicleId,
            FlightBookingId = input.FlightBookingId,
            AssignedByUserId = input.AssignedByUserId ?? Guid.Empty,
            AssignedAt = DateTime.UtcNow
        };

        db.TripLegAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);

        if (tx is not null)
            await tx.CommitAsync(ct);

        return new ResourceAssignmentDto(assignment.Id, assignment.RouteLegId, assignment.VehicleId, assignment.FlightBookingId, assignment.AssignedByUserId, assignment.AssignedAt);
    }

    public async Task<StaffAssignmentDto> AssignStaffAsync(Guid tripId, StaffInput input, CancellationToken ct = default, Guid? actorUserId = null, bool canOverrideOperatingArea = false)
    {
        await using var tx = await db.BeginTransactionAsync(ct);

        if (!await db.TransportTrips.AnyAsync(t => t.Id == tripId, ct))
            throw new NotFoundException("Trip", tripId);

        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == input.LegId, ct)
            ?? throw new NotFoundException("RouteLeg", input.LegId);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        await EnsureAssignableRouteVersionAsync(version, ct);

        if (!await db.RoutePlanVersions.AnyAsync(routeVersion => routeVersion.Id == version.Id &&
            db.RoutePlans.Any(routePlan => routePlan.Id == routeVersion.RoutePlanId && routePlan.TripId == tripId), ct))
            throw new BusinessRuleException("The selected route leg does not belong to this trip.", "ROUTE_LEG_TRIP_MISMATCH");

        var role = input.Role?.Trim().ToUpperInvariant();
        if (role is not (PlanningConstants.StaffRole.Driver or PlanningConstants.StaffRole.Escort))
            throw new BusinessRuleException("Staff role must be DRIVER or ESCORT.", "INVALID_STAFF_ROLE");

        string? operatingAreaOverrideReason = null;

        if (await db.StaffAssignments.AnyAsync(assignment => assignment.UserId == input.UserId && assignment.RouteLegId == input.LegId && assignment.ReleasedAt == null, ct))
            throw new BusinessRuleException("This staff member already has an active assignment on this leg.", "STAFF_ALREADY_ASSIGNED");

        if (!leg.PlannedDepartureAt.HasValue || !leg.PlannedArrivalAt.HasValue || leg.PlannedArrivalAt.Value <= leg.PlannedDepartureAt.Value)
            throw new BusinessRuleException("Set a valid departure and arrival time before assigning staff so schedule conflicts can be checked.", "STAFF_SCHEDULE_REQUIRED");

        if (role == PlanningConstants.StaffRole.Driver)
        {
            var routeCountryIds = await db.Locations.AsNoTracking()
                .Where(location => location.Id == leg.OriginLocationId || location.Id == leg.DestinationLocationId ||
                    db.Checkpoints.Any(checkpoint => checkpoint.RouteLegId == leg.Id && checkpoint.LocationId == location.Id))
                .Select(location => location.CountryId)
                .Distinct()
                .ToListAsync(ct);
            var validUntil = DateOnly.FromDateTime(leg.PlannedArrivalAt.Value);
            var authorizedCountryIds = await db.StaffRouteCountryAuthorizations.AsNoTracking()
                .Where(item => item.UserId == input.UserId && routeCountryIds.Contains(item.CountryId) &&
                    (!item.ValidUntil.HasValue || item.ValidUntil.Value >= validUntil))
                .Select(item => item.CountryId)
                .Distinct()
                .ToListAsync(ct);
            if (routeCountryIds.Count == 0 || authorizedCountryIds.Count != routeCountryIds.Count)
                throw new BusinessRuleException("This driver is not approved for every country on the route leg through its scheduled arrival date.", "STAFF_ROUTE_COUNTRY_NOT_AUTHORIZED");

            var trip = await db.TransportTrips.AsNoTracking().FirstAsync(item => item.Id == tripId, ct);
            var legOrigin = await db.Locations.AsNoTracking().FirstAsync(item => item.Id == leg.OriginLocationId, ct);
            var customPickupApplies = leg.SequenceNo == 1 && trip.CustomPickupLatitude.HasValue && trip.CustomPickupLongitude.HasValue;
            var pickupLatitude = customPickupApplies ? (double)trip.CustomPickupLatitude!.Value : legOrigin.Latitude is decimal latitude ? (double)latitude : (double?)null;
            var pickupLongitude = customPickupApplies ? (double)trip.CustomPickupLongitude!.Value : legOrigin.Longitude is decimal longitude ? (double)longitude : (double?)null;
            if (!pickupLatitude.HasValue || !pickupLongitude.HasValue)
                throw new BusinessRuleException("The driver pickup must have map coordinates before assignment.", "ROUTE_ORIGIN_COORDINATES_REQUIRED");

            var operatingAreas = await (from area in db.StaffOperatingAreas.AsNoTracking()
                                        join location in db.Locations.AsNoTracking() on area.LocationId equals location.Id
                                        where area.UserId == input.UserId && location.Active && location.Latitude.HasValue && location.Longitude.HasValue
                                        select new { area.RadiusKm, Latitude = location.Latitude!.Value, Longitude = location.Longitude!.Value })
                .ToListAsync(ct);
            var covered = operatingAreas.Any(area => DistanceKm(pickupLatitude.Value, pickupLongitude.Value,
                (double)area.Latitude, (double)area.Longitude) <= (double)area.RadiusKm);
            if (!covered)
            {
                operatingAreaOverrideReason = input.OutsideAreaOverrideReason?.Trim();
                if (!canOverrideOperatingArea || actorUserId is null || string.IsNullOrWhiteSpace(operatingAreaOverrideReason) || operatingAreaOverrideReason.Length > 1000)
                    throw new BusinessRuleException("Driver pickup is outside the driver's operating areas. A LOGISTICS_MANAGER or ADMIN must provide an override reason.", "DRIVER_OUTSIDE_OPERATING_AREA", "outsideAreaOverrideReason");
            }
        }

        // Staff overlap check
        {
            var dep = leg.PlannedDepartureAt.Value;
            var arr = leg.PlannedArrivalAt.Value;

            var isConflict = await db.StaffAssignments
                .Where(s => s.UserId == input.UserId && s.ReleasedAt == null && s.RouteLegId.HasValue && s.RouteLegId != input.LegId)
                .Join(db.RouteLegs,
                    assignment => assignment.RouteLegId!.Value,
                    otherLeg => otherLeg.Id,
                    (assignment, otherLeg) => new { assignment, otherLeg })
                .Join(db.RoutePlanVersions,
                    row => row.otherLeg.RoutePlanVersionId,
                    otherVersion => otherVersion.Id,
                    (row, otherVersion) => new { row.assignment, row.otherLeg, otherVersion.Status })
                .AnyAsync(row => (row.otherLeg.RoutePlanVersionId == version.Id ||
                    row.Status == PlanningConstants.RouteVersionStatus.Active || row.Status == PlanningConstants.RouteVersionStatus.Draft ||
                    row.Status == PlanningConstants.RouteVersionStatus.PendingApproval || row.Status == PlanningConstants.RouteVersionStatus.Approved) &&
                    row.otherLeg.PlannedDepartureAt.HasValue && row.otherLeg.PlannedArrivalAt.HasValue &&
                    row.otherLeg.PlannedDepartureAt.Value < arr && dep < row.otherLeg.PlannedArrivalAt.Value &&
                    !db.TransportTrips.Any(otherTrip => otherTrip.Id == row.assignment.TripId &&
                        (otherTrip.Status == PlanningConstants.TripStatus.Completed || otherTrip.Status == PlanningConstants.TripStatus.Cancelled)), ct);

            if (isConflict)
                throw new BusinessRuleException("Staff member is already assigned to a conflicting route leg.", "STAFF_DOUBLE_BOOKED");
        }

        var assignment = new StaffAssignment
        {
            TripId = tripId,
            RouteLegId = input.LegId,
            UserId = input.UserId,
            Role = role,
            AssignedAt = DateTime.UtcNow
        };

        db.StaffAssignments.Add(assignment);
        if (operatingAreaOverrideReason is not null)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "StaffAssignment",
                EntityId = assignment.Id,
                Action = "DRIVER_OPERATING_AREA_OVERRIDE",
                OldState = "OUTSIDE_OPERATING_AREA",
                NewState = "ASSIGNED",
                ActorUserId = actorUserId!.Value,
                Reason = operatingAreaOverrideReason,
                CorrelationId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync(ct);

        if (tx is not null)
            await tx.CommitAsync(ct);

        return new StaffAssignmentDto(assignment.Id, assignment.TripId, assignment.RouteLegId, assignment.UserId, assignment.Role, assignment.AssignedAt, assignment.ReleasedAt);
    }

    public async Task<HorseAssignmentDto> AssignHorseAsync(Guid tripId, HorseInput input, CancellationToken ct = default)
    {
        await using var tx = await db.BeginTransactionAsync(ct);

        var trip = await db.TransportTrips.FirstOrDefaultAsync(t => t.Id == tripId, ct);
        if (trip is null)
            throw new NotFoundException("Trip", tripId);
        if (trip.MergedIntoTripId.HasValue)
            throw new BusinessRuleException("Assign horses on the consolidated dispatch trip, not on an order that was merged into it.", "TRIP_CONSOLIDATED");

        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == input.LegId, ct)
            ?? throw new NotFoundException("RouteLeg", input.LegId);

        if (!await db.RoutePlanVersions.AnyAsync(version => version.Id == leg.RoutePlanVersionId &&
            db.RoutePlans.Any(plan => plan.Id == version.RoutePlanId && plan.TripId == tripId), ct))
            throw new BusinessRuleException("The selected route leg does not belong to this trip.", "ROUTE_LEG_TRIP_MISMATCH");

        var requestedHorseIds = (await db.TripOrders.AsNoTracking().Where(order => order.TripId == tripId)
            .Select(order => order.HorseIdsJson).ToListAsync(ct))
            .SelectMany(value => { try { return JsonSerializer.Deserialize<Guid[]>(value) ?? []; } catch (JsonException) { return []; } })
            .ToHashSet();
        if (requestedHorseIds.Count > 0 && !requestedHorseIds.Contains(input.HorseId))
            throw new BusinessRuleException("Choose a horse included in one of this dispatch trip’s approved orders.", "HORSE_NOT_IN_DISPATCH_ORDERS", "horseId");

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        await EnsureAssignableRouteVersionAsync(version, ct);

        var existingAssignment = await db.HorseLegAssignments
            .FirstOrDefaultAsync(h => h.RouteLegId == input.LegId && h.HorseId == input.HorseId, ct);

        // Check vehicle horse capacity on this leg if vehicle is assigned
        var vehicleAssignment = await db.TripLegAssignments.FirstOrDefaultAsync(a => a.RouteLegId == input.LegId && a.VehicleId != null, ct);
        if (vehicleAssignment?.VehicleId != null)
        {
            var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleAssignment.VehicleId.Value, ct);
            if (vehicle is not null)
            {
                var assignedHorseCount = await db.HorseLegAssignments.CountAsync(h => h.RouteLegId == input.LegId, ct);
                var additionalHorseCount = existingAssignment is null ? 1 : 0;
                if (assignedHorseCount + additionalHorseCount > vehicle.HorseCapacity)
                {
                    throw new BusinessRuleException(
                        $"Vehicle horse capacity ({vehicle.HorseCapacity}) exceeded for route leg {leg.SequenceNo}.",
                        "VEHICLE_CAPACITY_EXCEEDED"
                    );
                }
                if (assignedHorseCount + additionalHorseCount > vehicle.StallCapacity)
                    throw new BusinessRuleException($"Vehicle stall capacity ({vehicle.StallCapacity}) exceeded for route leg {leg.SequenceNo}.", "VEHICLE_STALL_CAPACITY_EXCEEDED");
            }
        }

        if (input.StallId.HasValue)
        {
            var stall = await db.Stalls.FirstOrDefaultAsync(s => s.Id == input.StallId.Value, ct)
                ?? throw new NotFoundException("Stall", input.StallId.Value);

            if (!stall.Active)
                throw new BusinessRuleException("Stall is inactive and cannot be assigned.", "STALL_INACTIVE");

            // Check stall capacity overlap
            if (leg.PlannedDepartureAt.HasValue && leg.PlannedArrivalAt.HasValue)
            {
                var dep = leg.PlannedDepartureAt.Value;
                var arr = leg.PlannedArrivalAt.Value;

                var bookedCount = await db.HorseLegAssignments
                    .Where(h => h.StallId == stall.Id)
                    .Join(db.RouteLegs, assignment => assignment.RouteLegId, otherLeg => otherLeg.Id, (assignment, otherLeg) => new { assignment, otherLeg })
                    .Join(db.RoutePlanVersions, row => row.otherLeg.RoutePlanVersionId, otherVersion => otherVersion.Id, (row, otherVersion) => new { row.assignment, row.otherLeg, otherVersion.Status, otherVersion.RoutePlanId })
                    .Join(db.RoutePlans, row => row.RoutePlanId, plan => plan.Id, (row, plan) => new { row.assignment, row.otherLeg, row.Status, plan.TripId, plan.ActiveVersionId })
                    .CountAsync(row => (row.assignment.RouteLegId != input.LegId || row.assignment.HorseId != input.HorseId) &&
                        (row.otherLeg.RoutePlanVersionId == version.Id ||
                         (row.TripId != tripId && (row.Status == PlanningConstants.RouteVersionStatus.Draft ||
                          (row.Status == PlanningConstants.RouteVersionStatus.Active && row.ActiveVersionId == row.otherLeg.RoutePlanVersionId)))) &&
                        row.otherLeg.PlannedDepartureAt.HasValue && row.otherLeg.PlannedArrivalAt.HasValue &&
                        row.otherLeg.PlannedDepartureAt.Value < arr && dep < row.otherLeg.PlannedArrivalAt.Value &&
                        !db.TransportTrips.Any(otherTrip => otherTrip.Id == row.TripId &&
                            (otherTrip.Status == PlanningConstants.TripStatus.Completed || otherTrip.Status == PlanningConstants.TripStatus.Cancelled)), ct);

                if (bookedCount >= stall.Capacity)
                    throw new BusinessRuleException($"Stall capacity ({stall.Capacity}) is full for this interval.", "STALL_CAPACITY_EXCEEDED");
            }
        }

        if (existingAssignment is not null)
        {
            existingAssignment.StallId = input.StallId;
            await db.SaveChangesAsync(ct);

            if (tx is not null)
                await tx.CommitAsync(ct);

            return new HorseAssignmentDto(existingAssignment.Id, existingAssignment.TripId, existingAssignment.HorseId,
                existingAssignment.RouteLegId, existingAssignment.StallId, existingAssignment.BoardedAt, existingAssignment.UnboardedAt);
        }

        var assignment = new HorseLegAssignment
        {
            TripId = tripId,
            HorseId = input.HorseId,
            RouteLegId = input.LegId,
            StallId = input.StallId
        };

        db.HorseLegAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);

        if (tx is not null)
            await tx.CommitAsync(ct);

        return new HorseAssignmentDto(assignment.Id, assignment.TripId, assignment.HorseId, assignment.RouteLegId, assignment.StallId, assignment.BoardedAt, assignment.UnboardedAt);
    }

    public async Task<TripAssignmentsSummaryDto> GetTripAssignmentsAsync(Guid tripId, CancellationToken ct = default)
    {
        if (!await db.TransportTrips.AnyAsync(t => t.Id == tripId, ct))
            throw new NotFoundException("Trip", tripId);

        var resources = await db.TripLegAssignments.AsNoTracking()
            .Join(
                db.RouteLegs.AsNoTracking(),
                a => a.RouteLegId,
                l => l.Id,
                (a, l) => new { a, l }
            )
            .Join(
                db.RoutePlanVersions.AsNoTracking(),
                al => al.l.RoutePlanVersionId,
                v => v.Id,
                (al, v) => new { al.a, al.l, v }
            )
            .Join(
                db.RoutePlans.AsNoTracking(),
                alv => alv.v.RoutePlanId,
                p => p.Id,
                (alv, p) => new { alv.a, p.TripId }
            )
            .Where(x => x.TripId == tripId)
            .Select(x => new ResourceAssignmentDto(x.a.Id, x.a.RouteLegId, x.a.VehicleId, x.a.FlightBookingId, x.a.AssignedByUserId, x.a.AssignedAt))
            .ToListAsync(ct);

        var staff = await db.StaffAssignments.AsNoTracking()
            .Where(s => s.TripId == tripId)
            .Select(s => new StaffAssignmentDto(s.Id, s.TripId, s.RouteLegId, s.UserId, s.Role, s.AssignedAt, s.ReleasedAt))
            .ToListAsync(ct);

        var horses = await db.HorseLegAssignments.AsNoTracking()
            .Where(h => h.TripId == tripId)
            .Select(h => new HorseAssignmentDto(h.Id, h.TripId, h.HorseId, h.RouteLegId, h.StallId, h.BoardedAt, h.UnboardedAt))
            .ToListAsync(ct);

        return new TripAssignmentsSummaryDto(resources, staff, horses);
    }

    // ==========================================
    // State Transitions
    // ==========================================
    public async Task<RoutePlanVersionDto> SubmitRouteVersionAsync(Guid versionId, Guid actorUserId, CancellationToken ct = default)
    {
        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", versionId);

        if (version.Status != PlanningConstants.RouteVersionStatus.Draft)
            throw new BusinessRuleException($"Route plan version must be in DRAFT to submit. Current status: {version.Status}", "INVALID_VERSION_TRANSITION");

        var routeLegs = await db.RouteLegs.AsNoTracking().Where(l => l.RoutePlanVersionId == versionId).ToListAsync(ct);
        if (routeLegs.Count == 0)
            throw new BusinessRuleException("Cannot submit a route plan version with no route legs.", "NO_ROUTE_LEGS");

        var routeLegIds = routeLegs.Select(leg => leg.Id).ToArray();
        var assignedResourceLegIds = await db.TripLegAssignments.AsNoTracking()
            .Where(assignment => routeLegIds.Contains(assignment.RouteLegId))
            .Select(assignment => assignment.RouteLegId).Distinct().ToListAsync(ct);
        var missingResourceLegs = routeLegs.Where(leg => !assignedResourceLegIds.Contains(leg.Id)).ToArray();
        if (missingResourceLegs.Length > 0)
            throw new BusinessRuleException($"Assign a vehicle or confirmed flight to every route leg before sending this dispatch plan for approval. Missing: {string.Join(", ", missingResourceLegs.Select(leg => $"leg {leg.SequenceNo}"))}.", "DISPATCH_RESOURCES_INCOMPLETE");

        var roadLegs = routeLegs.Where(leg => string.Equals(leg.TransportMode, PlanningConstants.TransportMode.Road, StringComparison.OrdinalIgnoreCase)).ToArray();
        var assignedDriverLegIds = await db.StaffAssignments.AsNoTracking()
            .Where(assignment => routeLegIds.Contains(assignment.RouteLegId ?? Guid.Empty) && assignment.Role == PlanningConstants.StaffRole.Driver && assignment.ReleasedAt == null)
            .Select(assignment => assignment.RouteLegId!.Value).Distinct().ToListAsync(ct);
        var missingDriverLegs = roadLegs.Where(leg => !assignedDriverLegIds.Contains(leg.Id)).ToArray();
        if (missingDriverLegs.Length > 0)
            throw new BusinessRuleException($"Assign an approved driver to every road leg before sending this dispatch plan for approval. Missing: {string.Join(", ", missingDriverLegs.Select(leg => $"leg {leg.SequenceNo}"))}.", "DISPATCH_DRIVERS_INCOMPLETE");

        // Revalidate assignments at submit because route times and checkpoints may have changed since assignment.
        var assignedStaff = await db.StaffAssignments.AsNoTracking()
            .Where(assignment => routeLegIds.Contains(assignment.RouteLegId ?? Guid.Empty) && assignment.ReleasedAt == null)
            .Select(assignment => new { assignment.UserId, RouteLegId = assignment.RouteLegId!.Value, assignment.Role })
            .ToListAsync(ct);
        foreach (var assignment in assignedStaff)
        {
            var leg = routeLegs.First(item => item.Id == assignment.RouteLegId);
            if (!leg.PlannedDepartureAt.HasValue || !leg.PlannedArrivalAt.HasValue || leg.PlannedArrivalAt.Value <= leg.PlannedDepartureAt.Value)
                throw new BusinessRuleException($"Set a valid schedule for leg {leg.SequenceNo} before submitting the dispatch plan.", "STAFF_SCHEDULE_REQUIRED");

            var hasScheduleConflict = await db.StaffAssignments.AsNoTracking()
                .Where(other => other.UserId == assignment.UserId && other.ReleasedAt == null && other.RouteLegId.HasValue && other.RouteLegId != assignment.RouteLegId)
                .Join(db.RouteLegs.AsNoTracking(), other => other.RouteLegId!.Value, otherLeg => otherLeg.Id,
                    (other, otherLeg) => new { other, otherLeg })
                .Join(db.RoutePlanVersions.AsNoTracking(), row => row.otherLeg.RoutePlanVersionId, otherVersion => otherVersion.Id,
                    (row, otherVersion) => new { row.other, row.otherLeg, otherVersion.Status })
                .AnyAsync(row => (row.otherLeg.RoutePlanVersionId == versionId ||
                    row.Status == PlanningConstants.RouteVersionStatus.Active || row.Status == PlanningConstants.RouteVersionStatus.Draft ||
                    row.Status == PlanningConstants.RouteVersionStatus.PendingApproval || row.Status == PlanningConstants.RouteVersionStatus.Approved) &&
                    row.otherLeg.PlannedDepartureAt.HasValue && row.otherLeg.PlannedArrivalAt.HasValue &&
                    row.otherLeg.PlannedDepartureAt.Value < leg.PlannedArrivalAt.Value && leg.PlannedDepartureAt.Value < row.otherLeg.PlannedArrivalAt.Value &&
                    !db.TransportTrips.Any(otherTrip => otherTrip.Id == row.other.TripId &&
                        (otherTrip.Status == PlanningConstants.TripStatus.Completed || otherTrip.Status == PlanningConstants.TripStatus.Cancelled)), ct);
            if (hasScheduleConflict)
                throw new BusinessRuleException($"Staff member {assignment.UserId} has a conflicting route leg; update the assignment before submitting.", "STAFF_DOUBLE_BOOKED");

            if (assignment.Role == PlanningConstants.StaffRole.Driver && roadLegs.Any(roadLeg => roadLeg.Id == leg.Id))
            {
                var routeCountryIds = await db.Locations.AsNoTracking()
                    .Where(location => location.Id == leg.OriginLocationId || location.Id == leg.DestinationLocationId ||
                        db.Checkpoints.Any(checkpoint => checkpoint.RouteLegId == leg.Id && checkpoint.LocationId == location.Id))
                    .Select(location => location.CountryId)
                    .Distinct()
                    .ToListAsync(ct);
                var validUntil = DateOnly.FromDateTime(leg.PlannedArrivalAt.Value);
                var authorizedCountryIds = await db.StaffRouteCountryAuthorizations.AsNoTracking()
                    .Where(countryAuthorization => countryAuthorization.UserId == assignment.UserId && routeCountryIds.Contains(countryAuthorization.CountryId) &&
                        (!countryAuthorization.ValidUntil.HasValue || countryAuthorization.ValidUntil.Value >= validUntil))
                    .Select(countryAuthorization => countryAuthorization.CountryId)
                    .Distinct()
                    .ToListAsync(ct);
                if (routeCountryIds.Count == 0 || authorizedCountryIds.Count != routeCountryIds.Count)
                    throw new BusinessRuleException($"Driver {assignment.UserId} is not approved for every country on leg {leg.SequenceNo} through its scheduled arrival.", "STAFF_ROUTE_COUNTRY_NOT_AUTHORIZED");
            }
        }

        var assignedEscortLegIds = await db.StaffAssignments.AsNoTracking()
            .Where(assignment => routeLegIds.Contains(assignment.RouteLegId ?? Guid.Empty) && assignment.Role == PlanningConstants.StaffRole.Escort && assignment.ReleasedAt == null)
            .Select(assignment => assignment.RouteLegId!.Value).Distinct().ToListAsync(ct);
        var missingEscortLegs = roadLegs.Where(leg => !assignedEscortLegIds.Contains(leg.Id)).ToArray();
        if (missingEscortLegs.Length > 0)
            throw new BusinessRuleException($"Assign an escort to every road leg before sending this dispatch plan for approval. Missing: {string.Join(", ", missingEscortLegs.Select(leg => $"leg {leg.SequenceNo}"))}.", "DISPATCH_ESCORTS_INCOMPLETE");

        var tripId = await db.RoutePlans.Where(plan => plan.Id == version.RoutePlanId).Select(plan => plan.TripId).SingleAsync(ct);
        var requestedHorseIds = (await db.TripOrders.AsNoTracking().Where(order => order.TripId == tripId)
            .Select(order => order.HorseIdsJson).ToListAsync(ct))
            .SelectMany(value => { try { return JsonSerializer.Deserialize<Guid[]>(value) ?? []; } catch (JsonException) { return []; } })
            .Distinct().ToHashSet();
        var assignedHorses = await db.HorseLegAssignments.AsNoTracking()
            .Where(assignment => assignment.TripId == tripId && routeLegIds.Contains(assignment.RouteLegId))
            .ToListAsync(ct);
        var missingHorseCount = requestedHorseIds.Except(assignedHorses.Select(assignment => assignment.HorseId)).Count();
        if (missingHorseCount > 0)
            throw new BusinessRuleException($"Assign all {missingHorseCount} remaining horse(s) to this route before sending it for approval.", "DISPATCH_HORSES_INCOMPLETE");
        if (assignedHorses.Any(assignment => requestedHorseIds.Contains(assignment.HorseId) && !assignment.StallId.HasValue))
            throw new BusinessRuleException("Assign a compartment to every horse before sending this route for approval.", "DISPATCH_HORSE_COMPARTMENTS_INCOMPLETE");

        version.Status = PlanningConstants.RouteVersionStatus.PendingApproval;
        await db.SaveChangesAsync(ct);

        return MapVersion(version);
    }

    public async Task<RoutePlanVersionDto> ApproveRouteVersionAsync(Guid versionId, Guid actorUserId, CancellationToken ct = default)
    {
        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", versionId);

        if (version.Status != PlanningConstants.RouteVersionStatus.PendingApproval)
            throw new BusinessRuleException($"Route plan version must be in PENDING_APPROVAL to approve. Current status: {version.Status}", "INVALID_VERSION_TRANSITION");

        version.Status = PlanningConstants.RouteVersionStatus.Approved;
        version.ApprovedByUserId = actorUserId;
        version.ApprovedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            EntityType = "RoutePlanVersion",
            EntityId = version.Id,
            Action = "APPROVE",
            OldState = PlanningConstants.RouteVersionStatus.PendingApproval,
            NewState = PlanningConstants.RouteVersionStatus.Approved,
            ActorUserId = actorUserId,
            OccurredAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return MapVersion(version);
    }

    public async Task<RoutePlanVersionDto> RejectRouteVersionAsync(Guid versionId, Guid actorUserId, string reason, Guid? correlationId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            throw new BusinessRuleException("A rejection reason of at most 2000 characters is required.", "REJECTION_REASON_REQUIRED", "reason");
        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", versionId);
        if (version.Status != PlanningConstants.RouteVersionStatus.PendingApproval)
            throw new BusinessRuleException("Only a PENDING_APPROVAL route change can be rejected.", "INVALID_VERSION_TRANSITION");
        version.Status = PlanningConstants.RouteVersionStatus.Rejected;
        db.AuditLogs.Add(new AuditLog
        {
            EntityType = "RoutePlanVersion",
            EntityId = version.Id,
            Action = "ROUTE_CHANGE_REJECTED",
            OldState = PlanningConstants.RouteVersionStatus.PendingApproval,
            NewState = PlanningConstants.RouteVersionStatus.Rejected,
            ActorUserId = actorUserId,
            Reason = reason.Trim(),
            CorrelationId = correlationId,
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        return MapVersion(version);
    }

    public async Task<RoutePlanVersionDto> ActivateRouteVersionAsync(Guid versionId, Guid actorUserId, CancellationToken ct = default)
    {
        await using var tx = await db.BeginTransactionAsync(ct);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", versionId);

        if (version.Status != PlanningConstants.RouteVersionStatus.Approved)
            throw new BusinessRuleException($"Only an APPROVED route version can be activated. Current status: {version.Status}", "INVALID_VERSION_TRANSITION");

        if (!await db.RouteLegs.AnyAsync(l => l.RoutePlanVersionId == versionId, ct))
            throw new BusinessRuleException("Route version contains no route legs and cannot be activated.", "NO_ROUTE_LEGS");

        var plan = await db.RoutePlans.FirstOrDefaultAsync(p => p.Id == version.RoutePlanId, ct)
            ?? throw new NotFoundException("RoutePlan", version.RoutePlanId);

        // Archive previous active versions
        var activeVersions = await db.RoutePlanVersions
            .Where(v => v.RoutePlanId == plan.Id && v.Status == PlanningConstants.RouteVersionStatus.Active)
            .ToListAsync(ct);

        foreach (var av in activeVersions)
        {
            av.Status = PlanningConstants.RouteVersionStatus.Archived;
        }

        // The database enforces one ACTIVE version per plan with a unique index.
        // Persist archiving before setting the replacement ACTIVE so SQL Server
        // cannot attempt the new ACTIVE insert/update before the old row is archived.
        if (activeVersions.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        version.Status = PlanningConstants.RouteVersionStatus.Active;
        plan.ActiveVersionId = version.Id;
        plan.UpdatedAt = DateTime.UtcNow;
        plan.VersionNo++;

        // Audit Log
        db.AuditLogs.Add(new AuditLog
        {
            EntityType = "RoutePlanVersion",
            EntityId = version.Id,
            Action = "ACTIVATE",
            OldState = PlanningConstants.RouteVersionStatus.Approved,
            NewState = PlanningConstants.RouteVersionStatus.Active,
            ActorUserId = actorUserId,
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow
        });

        var isRouteChange = version.Reason?.StartsWith("Emergency route change:", StringComparison.Ordinal) == true;
        // Outbox Event
        var payload = JsonSerializer.Serialize(new
        {
            tripId = plan.TripId,
            routePlanId = plan.Id,
            routePlanVersionId = version.Id,
            versionNo = version.VersionNo,
            delayEstimateMinutes = isRouteChange ? version.EstimatedDurationMinutes : null,
            additionalCost = isRouteChange ? version.AdditionalCost : 0,
            currencyCode = isRouteChange ? version.CurrencyCode : null,
            reason = isRouteChange ? version.Reason : null
        });

        db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = isRouteChange ? "Planning.RouteChangeApplied" : "Planning.RouteActivated",
            AggregateType = "RoutePlanVersion",
            AggregateId = version.Id,
            Payload = payload,
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Status = "PENDING"
        });

        await db.SaveChangesAsync(ct);
        if (tx is not null)
        {
            await tx.CommitAsync(ct);
        }
        return MapVersion(version);
    }

    // ==========================================
    // Helpers & Mappers
    // ==========================================
    private static void AssertVersionIsDraft(RoutePlanVersion version)
    {
        if (version.Status != PlanningConstants.RouteVersionStatus.Draft)
            throw new BusinessRuleException("Only DRAFT route versions can be edited; create a new version after activation.", "ROUTE_VERSION_IMMUTABLE");
    }

    private async Task EnsureAssignableRouteVersionAsync(RoutePlanVersion version, CancellationToken ct)
    {
        if (version.Status == PlanningConstants.RouteVersionStatus.Draft) return;
        if (version.Status != PlanningConstants.RouteVersionStatus.Active ||
            !await db.RoutePlans.AnyAsync(plan => plan.Id == version.RoutePlanId && plan.ActiveVersionId == version.Id, ct))
            throw new BusinessRuleException("Assignments can be prepared on a draft route or updated on the active route.", "ASSIGNABLE_ROUTE_REQUIRED");
    }

    private async Task ValidateLegLocationsAndTimesAsync(RouteLegInput input, CancellationToken ct)
    {
        if (input.DistanceKm is < 0)
            throw new BusinessRuleException("Route leg distance cannot be negative.", "INVALID_DISTANCE", "distanceKm");

        if (input.ArrivalAt <= input.DepartureAt)
            throw new BusinessRuleException("Route leg arrival time must be after departure time.", "INVALID_LEG_TIMES", "arrivalAt");

        if (input.OriginLocationId == input.DestinationLocationId)
            throw new BusinessRuleException("Route leg origin and destination locations must be distinct.", "SAME_LOCATION", "destinationLocationId");

        var mode = input.Mode.Trim().ToUpperInvariant();
        if (mode is not (PlanningConstants.TransportMode.Road or PlanningConstants.TransportMode.Air))
            throw new BusinessRuleException("Transport mode must be ROAD or AIR.", "INVALID_TRANSPORT_MODE", "mode");

        if (!await db.Locations.AnyAsync(l => l.Id == input.OriginLocationId, ct))
            throw new NotFoundException("Origin Location", input.OriginLocationId);

        if (!await db.Locations.AnyAsync(l => l.Id == input.DestinationLocationId, ct))
            throw new NotFoundException("Destination Location", input.DestinationLocationId);
    }

    private static TripDto MapTrip(TransportTrip t, int orderCount = 1, IReadOnlyList<string>? customerNames = null, IReadOnlyList<string>? horseNames = null) =>
        new(t.Id, t.TripNo, t.OrderId, t.Status, t.PlannedDepartureAt, t.PlannedArrivalAt, t.ComplianceReady, t.CreatedAt, t.UpdatedAt, t.VersionNo, t.CustomPickupLatitude, t.CustomPickupLongitude, t.CustomPickupLabel, t.OriginLocationId, t.DestinationLocationId, orderCount, t.MergedIntoTripId, customerNames, horseNames);

    private static TripOrderDto MapTripOrder(TripOrder order)
    {
        IReadOnlyList<Guid> horseIds;
        try { horseIds = JsonSerializer.Deserialize<Guid[]>(order.HorseIdsJson) ?? []; }
        catch (JsonException) { horseIds = []; }
        return new TripOrderDto(order.OrderId, order.RequestId, order.CustomerId, order.OriginLocationId, order.DestinationLocationId,
            order.RequestedDepartureAt, order.RequestedArrivalAt, order.PreferredTransportMode, horseIds, order.ReceiverName, order.ReceiverPhone, order.ReceiverEmail, order.CompletedAt,
            order.CustomPickupLatitude, order.CustomPickupLongitude, order.CustomPickupLabel, order.OrderNo, order.RequestNo, order.CustomerName, ParseHorseNames(order.HorseNamesJson), order.PaymentStatus);
    }

    private static IReadOnlyDictionary<Guid, string> ParseHorseNames(string value)
    {
        try { return JsonSerializer.Deserialize<Dictionary<Guid, string>>(value) ?? new Dictionary<Guid, string>(); }
        catch (JsonException) { return new Dictionary<Guid, string>(); }
    }

    private static RoutePlanDto MapPlan(RoutePlan p) =>
        new(p.Id, p.TripId, p.ActiveVersionId, p.CreatedByUserId, p.CreatedAt, p.UpdatedAt, p.VersionNo);

    private static RoutePlanVersionDto MapVersion(RoutePlanVersion v) =>
        new(v.Id, v.RoutePlanId, v.VersionNo, v.Status, v.Reason, v.TotalDistanceKm, v.EstimatedDurationMinutes, v.PlannedDepartureAt, v.PlannedArrivalAt, v.AdditionalCost, v.CurrencyCode, v.CreatedByUserId, v.ApprovedByUserId, v.ApprovedAt, v.CreatedAt);

    private static RouteLegDto MapLeg(RouteLeg l) =>
        new(l.Id, l.RoutePlanVersionId, l.SequenceNo, l.OriginLocationId, l.DestinationLocationId, l.TransportMode, l.PlannedDepartureAt, l.PlannedArrivalAt, l.DistanceKm, l.Status, ParseRouteShape(l.RouteShapeJson));

    private static IReadOnlyList<RouteShapePoint> ParseRouteShape(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<RouteShapePoint[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static CheckpointDto MapCheckpoint(Checkpoint c) =>
        new(c.Id, c.RouteLegId, c.SequenceNo, c.LocationId, c.Type, c.PlannedArrivalAt, c.PlannedDepartureAt, c.Status);
}
