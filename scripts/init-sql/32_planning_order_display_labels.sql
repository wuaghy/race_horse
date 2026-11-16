/* Human-readable labels copied from Booking.OrderCreated for dispatch search and display. */
USE PlanningDb;
GO

IF COL_LENGTH('dbo.TripOrders', 'OrderNo') IS NULL
    ALTER TABLE dbo.TripOrders ADD OrderNo NVARCHAR(50) NULL;
IF COL_LENGTH('dbo.TripOrders', 'RequestNo') IS NULL
    ALTER TABLE dbo.TripOrders ADD RequestNo NVARCHAR(50) NULL;
IF COL_LENGTH('dbo.TripOrders', 'CustomerName') IS NULL
    ALTER TABLE dbo.TripOrders ADD CustomerName NVARCHAR(255) NULL;
IF COL_LENGTH('dbo.TripOrders', 'HorseNamesJson') IS NULL
    ALTER TABLE dbo.TripOrders ADD HorseNamesJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_TripOrders_HorseNamesJson DEFAULT N'{}';
GO

UPDATE planningOrder
SET planningOrder.OrderNo = bookingOrder.OrderNo,
    planningOrder.RequestNo = request.RequestNo,
    planningOrder.CustomerName = customer.Name,
    planningOrder.HorseNamesJson = N'{' + ISNULL(horseNames.JsonNames, N'') + N'}'
FROM dbo.TripOrders AS planningOrder
INNER JOIN BookingDb.dbo.TransportOrders AS bookingOrder ON bookingOrder.Id = planningOrder.OrderId
INNER JOIN BookingDb.dbo.TransportRequests AS request ON request.Id = bookingOrder.RequestId
INNER JOIN BookingDb.dbo.Customers AS customer ON customer.Id = request.CustomerId
OUTER APPLY
(
    SELECT STUFF((
        SELECT N',' + N'"' + CONVERT(NVARCHAR(36), horse.Id) + N'":"' + STRING_ESCAPE(horse.HorseName, 'json') + N'"'
        FROM BookingDb.dbo.TransportRequestHorses AS requestHorse
        INNER JOIN BookingDb.dbo.Horses AS horse ON horse.Id = requestHorse.HorseId
        WHERE requestHorse.RequestId = request.Id
        ORDER BY horse.Id
        FOR XML PATH(''), TYPE
    ).value('.', 'NVARCHAR(MAX)'), 1, 1, N'') AS JsonNames
) AS horseNames
WHERE planningOrder.CustomerName IS NULL OR planningOrder.OrderNo IS NULL OR planningOrder.RequestNo IS NULL OR planningOrder.HorseNamesJson = N'{}';
GO
