USE [PlanningDb];
GO

/* Orders live in the date-based Logistics dispatch queue until attached to a real trip. */
IF COL_LENGTH(N'dbo.TripOrders', N'CustomPickupLatitude') IS NULL
    ALTER TABLE dbo.TripOrders ADD CustomPickupLatitude DECIMAL(10,7) NULL;
IF COL_LENGTH(N'dbo.TripOrders', N'CustomPickupLongitude') IS NULL
    ALTER TABLE dbo.TripOrders ADD CustomPickupLongitude DECIMAL(10,7) NULL;
IF COL_LENGTH(N'dbo.TripOrders', N'CustomPickupLabel') IS NULL
    ALTER TABLE dbo.TripOrders ADD CustomPickupLabel NVARCHAR(300) NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TripOrders') AND name = N'TripId' AND is_nullable = 0)
    ALTER TABLE dbo.TripOrders ALTER COLUMN TripId UNIQUEIDENTIFIER NULL;
GO

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.TransportTrips') AND name = N'UQ_TransportTrips_OrderId')
    ALTER TABLE dbo.TransportTrips DROP CONSTRAINT UQ_TransportTrips_OrderId;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.TransportTrips') AND name = N'OrderId' AND is_nullable = 0)
    ALTER TABLE dbo.TransportTrips ALTER COLUMN OrderId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TransportTrips') AND name = N'IX_TransportTrips_OrderId')
    CREATE INDEX IX_TransportTrips_OrderId ON dbo.TransportTrips (OrderId);
GO

UPDATE membership
SET CustomPickupLatitude = COALESCE(membership.CustomPickupLatitude, trip.CustomPickupLatitude),
    CustomPickupLongitude = COALESCE(membership.CustomPickupLongitude, trip.CustomPickupLongitude),
    CustomPickupLabel = COALESCE(membership.CustomPickupLabel, trip.CustomPickupLabel)
FROM dbo.TripOrders AS membership
INNER JOIN dbo.TransportTrips AS trip ON trip.Id = membership.TripId;
GO

/* Return automatically-created, still-unplanned single-order trips to the date queue. */
UPDATE membership
SET TripId = NULL
FROM dbo.TripOrders AS membership
INNER JOIN dbo.TransportTrips AS trip ON trip.Id = membership.TripId
WHERE trip.Status = 'PLANNED'
  AND NOT EXISTS (SELECT 1 FROM dbo.RoutePlans AS routePlanRecord WHERE routePlanRecord.TripId = trip.Id)
  AND NOT EXISTS (SELECT 1 FROM dbo.StaffAssignments AS assignment WHERE assignment.TripId = trip.Id)
  AND NOT EXISTS (SELECT 1 FROM dbo.HorseLegAssignments AS assignment WHERE assignment.TripId = trip.Id);

UPDATE trip
SET OrderId = NULL, Status = 'CONSOLIDATED', UpdatedAt = SYSUTCDATETIME(), VersionNo = VersionNo + 1
FROM dbo.TransportTrips AS trip
WHERE trip.Status = 'PLANNED' AND trip.OrderId IS NOT NULL
  AND EXISTS (SELECT 1 FROM dbo.TripOrders AS membership WHERE membership.OrderId = trip.OrderId AND membership.TripId IS NULL)
  AND NOT EXISTS (SELECT 1 FROM dbo.RoutePlans AS routePlanRecord WHERE routePlanRecord.TripId = trip.Id)
  AND NOT EXISTS (SELECT 1 FROM dbo.StaffAssignments AS assignment WHERE assignment.TripId = trip.Id)
  AND NOT EXISTS (SELECT 1 FROM dbo.HorseLegAssignments AS assignment WHERE assignment.TripId = trip.Id);
GO
