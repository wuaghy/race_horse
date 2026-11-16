/*
   Preserve the pickup, delivery, requested schedule and mode in PlanningDb so
   every trip's initial route can be created from the approved request.
   Backfill existing trips and seed their first draft/active route leg.
*/
USE PlanningDb;
GO

IF COL_LENGTH('dbo.TransportTrips', 'RequestId') IS NULL
    ALTER TABLE dbo.TransportTrips ADD RequestId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('dbo.TransportTrips', 'OriginLocationId') IS NULL
    ALTER TABLE dbo.TransportTrips ADD OriginLocationId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('dbo.TransportTrips', 'DestinationLocationId') IS NULL
    ALTER TABLE dbo.TransportTrips ADD DestinationLocationId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('dbo.TransportTrips', 'RequestedDepartureAt') IS NULL
    ALTER TABLE dbo.TransportTrips ADD RequestedDepartureAt DATETIME2(3) NULL;
IF COL_LENGTH('dbo.TransportTrips', 'RequestedArrivalAt') IS NULL
    ALTER TABLE dbo.TransportTrips ADD RequestedArrivalAt DATETIME2(3) NULL;
IF COL_LENGTH('dbo.TransportTrips', 'PreferredTransportMode') IS NULL
    ALTER TABLE dbo.TransportTrips ADD PreferredTransportMode VARCHAR(30) NULL;
GO

UPDATE trip
SET trip.RequestId = request.Id,
    trip.OriginLocationId = request.OriginLocationId,
    trip.DestinationLocationId = request.DestinationLocationId,
    trip.RequestedDepartureAt = request.RequestedDepartureAt,
    trip.RequestedArrivalAt = request.RequestedArrivalAt,
    trip.PreferredTransportMode = request.PreferredTransportMode,
    trip.PlannedDepartureAt = COALESCE(trip.PlannedDepartureAt, request.RequestedDepartureAt),
    trip.PlannedArrivalAt = COALESCE(trip.PlannedArrivalAt, request.RequestedArrivalAt)
FROM dbo.TransportTrips AS trip
INNER JOIN BookingDb.dbo.TransportOrders AS orders ON orders.Id = trip.OrderId
INNER JOIN BookingDb.dbo.TransportRequests AS request ON request.Id = orders.RequestId
WHERE trip.OriginLocationId IS NULL OR trip.DestinationLocationId IS NULL OR trip.RequestId IS NULL;
GO

;WITH RouteVersionForTrip AS
(
    SELECT routePlan.TripId,
           COALESCE(routePlan.ActiveVersionId, latest.Id) AS RoutePlanVersionId
    FROM dbo.RoutePlans AS routePlan
    OUTER APPLY
    (
        SELECT TOP (1) version.Id
        FROM dbo.RoutePlanVersions AS version
        WHERE version.RoutePlanId = routePlan.Id
        ORDER BY version.VersionNo DESC
    ) AS latest
)
INSERT INTO dbo.RouteLegs
    (RoutePlanVersionId, SequenceNo, OriginLocationId, DestinationLocationId,
     TransportMode, PlannedDepartureAt, PlannedArrivalAt, Status)
SELECT target.RoutePlanVersionId,
       1,
       trip.OriginLocationId,
       trip.DestinationLocationId,
       CASE WHEN UPPER(ISNULL(trip.PreferredTransportMode, 'ROAD')) = 'AIR' THEN 'AIR' ELSE 'ROAD' END,
       trip.RequestedDepartureAt,
       trip.RequestedArrivalAt,
       'PLANNED'
FROM RouteVersionForTrip AS target
INNER JOIN dbo.TransportTrips AS trip ON trip.Id = target.TripId
WHERE target.RoutePlanVersionId IS NOT NULL
  AND trip.OriginLocationId IS NOT NULL
  AND trip.DestinationLocationId IS NOT NULL
  AND trip.OriginLocationId <> trip.DestinationLocationId
  AND NOT EXISTS (SELECT 1 FROM dbo.RouteLegs AS leg WHERE leg.RoutePlanVersionId = target.RoutePlanVersionId);
GO
