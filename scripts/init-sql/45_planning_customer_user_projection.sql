USE [PlanningDb];
GO

IF COL_LENGTH(N'dbo.TripOrders', N'CustomerUserId') IS NULL
    ALTER TABLE dbo.TripOrders ADD CustomerUserId UNIQUEIDENTIFIER NULL;
GO

/* Backfill the authorization projection from the Booking-owned customer identity. */
UPDATE membership
SET CustomerUserId = customer.IdentityUserId
FROM dbo.TripOrders membership
INNER JOIN BookingDb.dbo.TransportRequests request ON request.Id = membership.RequestId
INNER JOIN BookingDb.dbo.Customers customer ON customer.Id = request.CustomerId
WHERE membership.CustomerUserId IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TripOrders') AND name = N'IX_TripOrders_CustomerUserId')
    CREATE INDEX IX_TripOrders_CustomerUserId ON dbo.TripOrders(CustomerUserId, TripId);
GO
