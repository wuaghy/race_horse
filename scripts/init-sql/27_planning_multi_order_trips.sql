USE [PlanningDb];
GO

IF COL_LENGTH('dbo.TransportTrips', 'MergedIntoTripId') IS NULL
    ALTER TABLE dbo.TransportTrips ADD MergedIntoTripId UNIQUEIDENTIFIER NULL;
GO

IF OBJECT_ID('dbo.TripOrders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TripOrders (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_TripOrders_Id DEFAULT NEWSEQUENTIALID(),
        TripId UNIQUEIDENTIFIER NOT NULL,
        OrderId UNIQUEIDENTIFIER NOT NULL,
        RequestId UNIQUEIDENTIFIER NULL,
        CustomerId UNIQUEIDENTIFIER NULL,
        OriginLocationId UNIQUEIDENTIFIER NULL,
        DestinationLocationId UNIQUEIDENTIFIER NULL,
        RequestedDepartureAt DATETIME2(3) NULL,
        RequestedArrivalAt DATETIME2(3) NULL,
        PreferredTransportMode VARCHAR(30) NULL,
        HorseIdsJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_TripOrders_HorseIdsJson DEFAULT '[]',
        ReceiverName NVARCHAR(200) NULL,
        ReceiverPhone NVARCHAR(50) NULL,
        ReceiverEmail NVARCHAR(255) NULL,
        CompletedAt DATETIME2(3) NULL,
        CONSTRAINT PK_TripOrders PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_TripOrders_OrderId UNIQUE (OrderId)
    );
    CREATE INDEX IX_TripOrders_TripId ON dbo.TripOrders (TripId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.TransportTrips') AND name = 'IX_TransportTrips_MergedIntoTripId')
    CREATE INDEX IX_TransportTrips_MergedIntoTripId ON dbo.TransportTrips (MergedIntoTripId);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.TripOrders)
    INSERT INTO dbo.TripOrders (TripId, OrderId, RequestId, OriginLocationId, DestinationLocationId, RequestedDepartureAt, RequestedArrivalAt, PreferredTransportMode)
    SELECT Id, OrderId, RequestId, OriginLocationId, DestinationLocationId, RequestedDepartureAt, RequestedArrivalAt, PreferredTransportMode
    FROM dbo.TransportTrips;
GO

/* Backfill the membership details for trips that existed before consolidation. */
UPDATE membership
SET CustomerId = COALESCE(membership.CustomerId, request.CustomerId),
    HorseIdsJson = CASE WHEN membership.HorseIdsJson IS NULL OR membership.HorseIdsJson = N'[]'
                        THEN COALESCE(horses.HorseIdsJson, N'[]') ELSE membership.HorseIdsJson END,
    ReceiverName = COALESCE(membership.ReceiverName,
        CASE WHEN request.RecipientIsRequester = 1 THEN COALESCE(customer.ContactPerson, customer.Name) ELSE request.RecipientName END),
    ReceiverPhone = COALESCE(membership.ReceiverPhone,
        CASE WHEN request.RecipientIsRequester = 1 THEN customer.Phone ELSE request.RecipientPhone END),
    ReceiverEmail = COALESCE(membership.ReceiverEmail,
        CASE WHEN request.RecipientIsRequester = 1 THEN customer.Email ELSE request.RecipientEmail END)
FROM dbo.TripOrders AS membership
INNER JOIN BookingDb.dbo.TransportRequests AS request ON request.Id = membership.RequestId
INNER JOIN BookingDb.dbo.Customers AS customer ON customer.Id = request.CustomerId
OUTER APPLY (
    SELECT N'[' + STRING_AGG(CONVERT(NVARCHAR(MAX), N'"' + CONVERT(NVARCHAR(36), requestHorse.HorseId) + N'"'), N',') + N']' AS HorseIdsJson
    FROM BookingDb.dbo.TransportRequestHorses AS requestHorse
    WHERE requestHorse.RequestId = request.Id
) AS horses;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('dbo.TransportTrips') AND name = 'CK_TransportTrips_Status')
    ALTER TABLE dbo.TransportTrips DROP CONSTRAINT CK_TransportTrips_Status;
GO
ALTER TABLE dbo.TransportTrips WITH CHECK ADD CONSTRAINT CK_TransportTrips_Status CHECK (Status IN
    ('PLANNED','READY','DEPARTED','IN_TRANSIT','DELAYED','ARRIVED','HANDED_OVER','COMPLETED','CANCELLED','CONSOLIDATED'));
GO
