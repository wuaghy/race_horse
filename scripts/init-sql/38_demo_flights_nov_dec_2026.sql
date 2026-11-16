USE PlanningDb;
GO

-- Synthetic bookings for the two seeded demo airports. These are test data,
-- not reservations with an airline. Times are UTC.
DECLARE @Airline UNIQUEIDENTIFIER = '66666666-6666-4666-8666-666666666665';
DECLARE @AirportA UNIQUEIDENTIFIER = '66666666-6666-4666-8666-666666666663';
DECLARE @AirportB UNIQUEIDENTIFIER = '66666666-6666-4666-8666-666666666664';

IF NOT EXISTS (SELECT 1 FROM dbo.Airlines WHERE Id = @Airline)
    OR NOT EXISTS (SELECT 1 FROM dbo.Locations WHERE Id = @AirportA AND Active = 1)
    OR NOT EXISTS (SELECT 1 FROM dbo.Locations WHERE Id = @AirportB AND Active = 1)
    THROW 50001, 'Demo airline and active demo airports must exist before seeding flights.', 1;

;WITH Dates AS (
    SELECT CONVERT(date, '2026-11-01') AS FlightDate
    UNION ALL SELECT DATEADD(day, 1, FlightDate) FROM Dates WHERE FlightDate < '2026-12-31'
), Flights AS (
    SELECT d.FlightDate, slot.HourUtc, direction.Code, direction.OriginId, direction.DestinationId,
        CONCAT('DEMO-2026-', CONVERT(char(8), d.FlightDate, 112), '-', direction.Code, '-', RIGHT('0' + CONVERT(varchar(2), slot.HourUtc), 2)) AS BookingReference
    FROM Dates d
    CROSS JOIN (VALUES (0), (6), (12), (18)) slot(HourUtc)
    CROSS JOIN (VALUES ('AB', @AirportA, @AirportB), ('BA', @AirportB, @AirportA)) direction(Code, OriginId, DestinationId)
)
INSERT INTO dbo.FlightBookings (Id, BookingReference, AirlineId, FlightNumber, OriginAirportId, DestinationAirportId, ScheduledDepartureAt, ScheduledArrivalAt, ConfirmationStatus, ConfirmedAt)
SELECT NEWID(), f.BookingReference, @Airline, CONCAT('DEMO-', f.Code, '-', RIGHT('0' + CONVERT(varchar(2), f.HourUtc), 2)),
       f.OriginId, f.DestinationId, DATEADD(hour, f.HourUtc, CONVERT(datetime2(3), f.FlightDate)),
       DATEADD(hour, f.HourUtc + 2, CONVERT(datetime2(3), f.FlightDate)), 'CONFIRMED', SYSUTCDATETIME()
FROM Flights f
WHERE NOT EXISTS (SELECT 1 FROM dbo.FlightBookings existing WHERE existing.BookingReference = f.BookingReference)
OPTION (MAXRECURSION 62);
GO
