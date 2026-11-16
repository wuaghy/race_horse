USE [BookingDb];
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @CustomerId UNIQUEIDENTIFIER = '33333333-3333-4333-8333-333333333333';
DECLARE @CustomerUserId UNIQUEIDENTIFIER = '11111111-1111-4111-8111-111111111111';
DECLARE @ManagerUserId UNIQUEIDENTIFIER = '22222222-2222-4222-8222-222222222222';
DECLARE @HorseOne UNIQUEIDENTIFIER = '44444444-4444-4444-8444-444444444441';
DECLARE @HorseTwo UNIQUEIDENTIFIER = '44444444-4444-4444-8444-444444444442';
DECLARE @HorseThree UNIQUEIDENTIFIER = '44444444-4444-4444-8444-444444444443';
DECLARE @SixFlowHorse UNIQUEIDENTIFIER = '44444444-4444-4444-8444-444444444446';
DECLARE @AvailableHorse UNIQUEIDENTIFIER = '44444444-4444-4444-8444-444444444447';
DECLARE @DraftRequest UNIQUEIDENTIFIER = '55555555-5555-4555-8555-555555555551';
DECLARE @SubmittedRequest UNIQUEIDENTIFIER = '55555555-5555-4555-8555-555555555552';
DECLARE @SecondQueueRequest UNIQUEIDENTIFIER = '55555555-5555-4555-8555-555555555553';
DECLARE @ReviewRequest UNIQUEIDENTIFIER = '55555555-5555-4555-8555-555555555554';
DECLARE @InformationRequest UNIQUEIDENTIFIER = '55555555-5555-4555-8555-555555555555';
DECLARE @SixFlowRequest UNIQUEIDENTIFIER = '55555555-5555-4555-8555-555555555556';
DECLARE @Origin UNIQUEIDENTIFIER = '66666666-6666-4666-8666-666666666661';
DECLARE @Destination UNIQUEIDENTIFIER = '66666666-6666-4666-8666-666666666662';

IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE IdentityUserId = @CustomerUserId)
BEGIN
    INSERT INTO dbo.Customers (Id, CustomerCode, IdentityUserId, Name, CustomerType, ContactPerson, Phone, Email, Address, Status)
    VALUES (@CustomerId, 'DEMO-CUSTOMER-001', @CustomerUserId, N'Demo Horse Transport Stable', 'CLUB', N'Alex Morgan', '+1-555-0101', 'customer.demo@racehorse.local', N'100 Stable Road', 'ACTIVE');
END
ELSE
BEGIN
    SELECT @CustomerId = Id FROM dbo.Customers WHERE IdentityUserId = @CustomerUserId;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Horses WHERE HorseCode = 'DEMO-HORSE-001')
    INSERT INTO dbo.Horses (Id, HorseCode, OwnerCustomerId, HorseName, RegistrationNumber, PassportNumber, Breed, Sex, DateOfBirth, Color, SpecialRequirements, Status)
    VALUES (@HorseOne, 'DEMO-HORSE-001', @CustomerId, N'Silver Comet', 'REG-DEMO-001', 'PASS-DEMO-001', N'Thoroughbred', 'GELDING', '2020-03-12', N'Bay', N'Temperature monitored; calm loading procedure', 'ACTIVE');
ELSE SELECT @HorseOne = Id FROM dbo.Horses WHERE HorseCode = 'DEMO-HORSE-001';

IF NOT EXISTS (SELECT 1 FROM dbo.Horses WHERE HorseCode = 'DEMO-HORSE-002')
    INSERT INTO dbo.Horses (Id, HorseCode, OwnerCustomerId, HorseName, RegistrationNumber, PassportNumber, Breed, Sex, DateOfBirth, Color, SpecialRequirements, Status)
    VALUES (@HorseTwo, 'DEMO-HORSE-002', @CustomerId, N'Morning Star', 'REG-DEMO-002', 'PASS-DEMO-002', N'Arabian', 'MARE', '2019-08-04', N'Grey', N'Keep with familiar handler during transfers', 'ACTIVE');
ELSE SELECT @HorseTwo = Id FROM dbo.Horses WHERE HorseCode = 'DEMO-HORSE-002';

IF NOT EXISTS (SELECT 1 FROM dbo.Horses WHERE HorseCode = 'DEMO-HORSE-003')
    INSERT INTO dbo.Horses (Id, HorseCode, OwnerCustomerId, HorseName, RegistrationNumber, PassportNumber, Breed, Sex, DateOfBirth, Color, SpecialRequirements, Status)
    VALUES (@HorseThree, 'DEMO-HORSE-003', @CustomerId, N'Riverstone', 'REG-DEMO-003', 'PASS-DEMO-003', N'Warmblood', 'GELDING', '2021-05-21', N'Chestnut', N'Use the rear ramp for loading', 'ACTIVE');
ELSE SELECT @HorseThree = Id FROM dbo.Horses WHERE HorseCode = 'DEMO-HORSE-003';

-- Dedicated synthetic horse and request for the end-to-end six-flow FE walkthrough.
-- They intentionally do not share horses with the generic queue records above, so
-- the availability guard does not reject the walkthrough as a duplicate booking.
IF NOT EXISTS (SELECT 1 FROM dbo.Horses WHERE HorseCode = 'DEMO-HORSE-006')
    INSERT INTO dbo.Horses (Id, HorseCode, OwnerCustomerId, HorseName, RegistrationNumber, PassportNumber, Breed, Sex, DateOfBirth, Color, SpecialRequirements, Status)
    VALUES (@SixFlowHorse, 'DEMO-HORSE-006', @CustomerId, N'Six Flow Runner', 'REG-DEMO-006', 'PASS-DEMO-006', N'Warmblood', 'MARE', '2020-06-06', N'Dark Bay', N'Synthetic QA horse reserved for the six-flow walkthrough only.', 'ACTIVE');
ELSE SELECT @SixFlowHorse = Id FROM dbo.Horses WHERE HorseCode = 'DEMO-HORSE-006';

-- Keep one active horse unassigned so a new local demo request can always
-- complete the horse-selection step. This horse is intentionally not linked
-- to any seeded open request or order.
IF NOT EXISTS (SELECT 1 FROM dbo.Horses WHERE HorseCode = 'DEMO-HORSE-007')
    INSERT INTO dbo.Horses (Id, HorseCode, OwnerCustomerId, HorseName, RegistrationNumber, PassportNumber, Breed, Sex, DateOfBirth, Color, SpecialRequirements, Status)
    VALUES (@AvailableHorse, 'DEMO-HORSE-007', @CustomerId, N'Available Demo Horse', 'REG-DEMO-007', 'PASS-DEMO-007', N'Thoroughbred', 'GELDING', '2021-02-14', N'Brown', N'Synthetic local demo horse; available for a new request.', 'ACTIVE');

IF NOT EXISTS (SELECT 1 FROM dbo.TransportRequests WHERE RequestNo = 'DEMO-REQ-DRAFT-001')
BEGIN
    INSERT INTO dbo.TransportRequests (Id, RequestNo, CustomerId, CreatedByUserId, OriginLocationId, DestinationLocationId, RequestedDepartureAt, RequestedArrivalAt, PreferredTransportMode, SpecialRequirements, Notes, Status)
    VALUES (@DraftRequest, 'DEMO-REQ-DRAFT-001', @CustomerId, @CustomerUserId, @Origin, @Destination, DATEADD(day, 30, SYSUTCDATETIME()), DATEADD(day, 32, SYSUTCDATETIME()), 'ROAD', N'Please confirm climate-controlled vehicle availability.', N'Demo draft request.', 'DRAFT');
    INSERT INTO dbo.TransportRequestHorses (RequestId, HorseId) VALUES (@DraftRequest, @HorseOne);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.TransportRequests WHERE RequestNo = 'DEMO-REQ-QUEUE-001')
BEGIN
    INSERT INTO dbo.TransportRequests (Id, RequestNo, CustomerId, CreatedByUserId, OriginLocationId, DestinationLocationId, RequestedDepartureAt, RequestedArrivalAt, PreferredTransportMode, SpecialRequirements, Notes, Status, SubmittedAt)
    VALUES (@SubmittedRequest, 'DEMO-REQ-QUEUE-001', @CustomerId, @CustomerUserId, @Origin, @Destination, DATEADD(day, 45, SYSUTCDATETIME()), DATEADD(day, 47, SYSUTCDATETIME()), 'MIXED', N'Coordinate stable handover at destination.', N'Demo manager-queue request.', 'SUBMITTED', SYSUTCDATETIME());
    INSERT INTO dbo.TransportRequestHorses (RequestId, HorseId) VALUES (@SubmittedRequest, @HorseOne), (@SubmittedRequest, @HorseTwo);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.TransportRequests WHERE RequestNo = 'DEMO-REQ-QUEUE-002')
BEGIN
    INSERT INTO dbo.TransportRequests (Id, RequestNo, CustomerId, CreatedByUserId, OriginLocationId, DestinationLocationId, RequestedDepartureAt, RequestedArrivalAt, PreferredTransportMode, SpecialRequirements, Notes, Status, SubmittedAt)
    VALUES (@SecondQueueRequest, 'DEMO-REQ-QUEUE-002', @CustomerId, @CustomerUserId, @Destination, @Origin, DATEADD(day, 60, SYSUTCDATETIME()), DATEADD(day, 62, SYSUTCDATETIME()), 'ROAD', N'Please confirm overnight stable stop.', N'Second demo manager-queue request.', 'SUBMITTED', SYSUTCDATETIME());
    INSERT INTO dbo.TransportRequestHorses (RequestId, HorseId) VALUES (@SecondQueueRequest, @HorseThree);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.TransportRequests WHERE RequestNo = 'DEMO-REQ-REVIEW-001')
BEGIN
    INSERT INTO dbo.TransportRequests (Id, RequestNo, CustomerId, CreatedByUserId, AssignedManagerUserId, OriginLocationId, DestinationLocationId, RequestedDepartureAt, RequestedArrivalAt, PreferredTransportMode, SpecialRequirements, Notes, Status, SubmittedAt, ReviewedAt)
    VALUES (@ReviewRequest, 'DEMO-REQ-REVIEW-001', @CustomerId, @CustomerUserId, @ManagerUserId, @Origin, @Destination, DATEADD(day, 75, SYSUTCDATETIME()), DATEADD(day, 77, SYSUTCDATETIME()), 'AIR', N'Confirm flight booking and horse handling plan.', N'Demo request ready for manager decision.', 'UNDER_REVIEW', SYSUTCDATETIME(), SYSUTCDATETIME());
    INSERT INTO dbo.TransportRequestHorses (RequestId, HorseId) VALUES (@ReviewRequest, @HorseOne), (@ReviewRequest, @HorseThree);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.TransportRequests WHERE RequestNo = 'DEMO-REQ-INFO-001')
BEGIN
    INSERT INTO dbo.TransportRequests (Id, RequestNo, CustomerId, CreatedByUserId, AssignedManagerUserId, OriginLocationId, DestinationLocationId, RequestedDepartureAt, RequestedArrivalAt, PreferredTransportMode, SpecialRequirements, Notes, Status, RejectionReason, SubmittedAt, ReviewedAt)
    VALUES (@InformationRequest, 'DEMO-REQ-INFO-001', @CustomerId, @CustomerUserId, @ManagerUserId, @Origin, @Destination, DATEADD(day, 90, SYSUTCDATETIME()), DATEADD(day, 92, SYSUTCDATETIME()), 'MIXED', N'Update the veterinary document before review.', N'Demo request returned for more information.', 'NEED_INFORMATION', N'Please attach the latest veterinary clearance.', SYSUTCDATETIME(), SYSUTCDATETIME());
    INSERT INTO dbo.TransportRequestHorses (RequestId, HorseId) VALUES (@InformationRequest, @HorseTwo);
END;

-- Start the walk-through at the Manager queue. The Customer, saved locations and
-- horse are real local-demo records; the request deliberately has no approval,
-- order, trip, document or assignment yet. Flow 1 can take/review it, Flow 2 can
-- upload/review its evidence, and the same chain then continues through Flows 3–6.
IF NOT EXISTS (SELECT 1 FROM dbo.TransportRequests WHERE RequestNo = 'DEMO-REQ-SIX-FLOW-001')
BEGIN
    INSERT INTO dbo.TransportRequests (Id, RequestNo, CustomerId, CreatedByUserId, OriginLocationId, DestinationLocationId, RequestedDepartureAt, RequestedArrivalAt, PreferredTransportMode, SpecialRequirements, Notes, Status, SubmittedAt)
    VALUES (@SixFlowRequest, 'DEMO-REQ-SIX-FLOW-001', @CustomerId, @CustomerUserId, @Origin, @Destination, DATEADD(day, 120, SYSUTCDATETIME()), DATEADD(day, 122, SYSUTCDATETIME()), 'ROAD', N'Synthetic QA chain; use only for the six-flow local FE walkthrough.', N'Synthetic local-only seed. No real transport, animal, legal or health data.', 'SUBMITTED', SYSUTCDATETIME());
    INSERT INTO dbo.TransportRequestHorses (RequestId, HorseId) VALUES (@SixFlowRequest, @SixFlowHorse);
END;

COMMIT TRANSACTION;
GO

USE [PlanningDb];
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Countries WHERE Id = '66666666-6666-4666-8666-666666666660')
    INSERT INTO dbo.Countries (Id, IsoCode, Name, Active)
    VALUES ('66666666-6666-4666-8666-666666666660', 'DEMO', N'Demo Country', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.StaffRouteCountryAuthorizations WHERE UserId = '33333333-3333-4333-8333-333333333333' AND CountryId = '66666666-6666-4666-8666-666666666660')
    INSERT INTO dbo.StaffRouteCountryAuthorizations (Id, UserId, CountryId)
    VALUES (NEWID(), '33333333-3333-4333-8333-333333333333', '66666666-6666-4666-8666-666666666660');

IF NOT EXISTS (SELECT 1 FROM dbo.Locations WHERE Id = '66666666-6666-4666-8666-666666666661')
    INSERT INTO dbo.Locations (Id, LocationCode, Name, LocationType, Address, City, CountryId, Active)
    VALUES ('66666666-6666-4666-8666-666666666661', 'DEMO-ORIGIN', N'Demo Origin Stable', 'STABLE', N'Demo address', N'Demo City', '66666666-6666-4666-8666-666666666660', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Locations WHERE Id = '66666666-6666-4666-8666-666666666662')
    INSERT INTO dbo.Locations (Id, LocationCode, Name, LocationType, Address, City, CountryId, Active)
    VALUES ('66666666-6666-4666-8666-666666666662', 'DEMO-DESTINATION', N'Demo Destination Stable', 'STABLE', N'Demo address', N'Demo City', '66666666-6666-4666-8666-666666666660', 1);

IF OBJECT_ID(N'dbo.RouteTransportCapabilities', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.RouteTransportCapabilities WHERE OriginLocationId = '66666666-6666-4666-8666-666666666661' AND DestinationLocationId = '66666666-6666-4666-8666-666666666662')
        INSERT INTO dbo.RouteTransportCapabilities (OriginLocationId, DestinationLocationId, AllowedModesJson, UpdatedByUserId)
        VALUES ('66666666-6666-4666-8666-666666666661', '66666666-6666-4666-8666-666666666662', N'["ROAD","AIR","MIXED"]', '00000000-0000-0000-0000-000000000000');
    IF NOT EXISTS (SELECT 1 FROM dbo.RouteTransportCapabilities WHERE OriginLocationId = '66666666-6666-4666-8666-666666666662' AND DestinationLocationId = '66666666-6666-4666-8666-666666666661')
        INSERT INTO dbo.RouteTransportCapabilities (OriginLocationId, DestinationLocationId, AllowedModesJson, UpdatedByUserId)
        VALUES ('66666666-6666-4666-8666-666666666662', '66666666-6666-4666-8666-666666666661', N'["ROAD","AIR","MIXED"]', '00000000-0000-0000-0000-000000000000');
END;

-- Synthetic schedule choices for the demo environment only. These rows do not represent live airline inventory.
IF NOT EXISTS (SELECT 1 FROM dbo.Airlines WHERE Id = '66666666-6666-4666-8666-666666666665')
    INSERT INTO dbo.Airlines (Id, AirlineCode, Name, Active)
    VALUES ('66666666-6666-4666-8666-666666666665', 'EQ', N'EquiRoute Demo Air', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Locations WHERE Id = '66666666-6666-4666-8666-666666666663')
    INSERT INTO dbo.Locations (Id, LocationCode, Name, LocationType, Address, City, CountryId, Latitude, Longitude, Active)
    VALUES ('66666666-6666-4666-8666-666666666663', 'DEMO-AIRPORT-A', N'Demo Airport A', 'AIRPORT', N'Demo terminal', N'Demo City', '66666666-6666-4666-8666-666666666660', 10.8188, 106.6519, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Locations WHERE Id = '66666666-6666-4666-8666-666666666664')
    INSERT INTO dbo.Locations (Id, LocationCode, Name, LocationType, Address, City, CountryId, Latitude, Longitude, Active)
    VALUES ('66666666-6666-4666-8666-666666666664', 'DEMO-AIRPORT-B', N'Demo Airport B', 'AIRPORT', N'Demo terminal', N'Demo City', '66666666-6666-4666-8666-666666666660', 21.2187, 105.8042, 1);

-- Keep all seeded locations mappable. Receiving-location lookup uses the two
-- demo stables; airports remain valid route endpoints and have their own map pins.
UPDATE dbo.Locations
SET Latitude = COALESCE(Latitude, CASE Id
        WHEN '66666666-6666-4666-8666-666666666661' THEN 10.8231
        WHEN '66666666-6666-4666-8666-666666666662' THEN 21.0278
        WHEN '66666666-6666-4666-8666-666666666663' THEN 10.8188
        WHEN '66666666-6666-4666-8666-666666666664' THEN 21.2187
    END),
    Longitude = COALESCE(Longitude, CASE Id
        WHEN '66666666-6666-4666-8666-666666666661' THEN 106.6297
        WHEN '66666666-6666-4666-8666-666666666662' THEN 105.8342
        WHEN '66666666-6666-4666-8666-666666666663' THEN 106.6519
        WHEN '66666666-6666-4666-8666-666666666664' THEN 105.8042
    END),
    Active = 1
WHERE Id IN (
    '66666666-6666-4666-8666-666666666661',
    '66666666-6666-4666-8666-666666666662',
    '66666666-6666-4666-8666-666666666663',
    '66666666-6666-4666-8666-666666666664'
);

-- Every pair of the four seeded demo locations must have a directed route
-- capability so the customer request wizard can proceed for any pickup/delivery.
-- Keep this additive: existing route-specific configuration is not overwritten.
IF OBJECT_ID(N'dbo.RouteTransportCapabilities', N'U') IS NOT NULL
BEGIN
    ;WITH DemoLocationIds AS
    (
        SELECT CONVERT(UNIQUEIDENTIFIER, '66666666-6666-4666-8666-666666666661') AS LocationId
        UNION ALL SELECT CONVERT(UNIQUEIDENTIFIER, '66666666-6666-4666-8666-666666666662')
        UNION ALL SELECT CONVERT(UNIQUEIDENTIFIER, '66666666-6666-4666-8666-666666666663')
        UNION ALL SELECT CONVERT(UNIQUEIDENTIFIER, '66666666-6666-4666-8666-666666666664')
    )
    INSERT INTO dbo.RouteTransportCapabilities (OriginLocationId, DestinationLocationId, AllowedModesJson, UpdatedByUserId)
    SELECT origin.Id, destination.Id, N'["ROAD","AIR","MIXED"]', '00000000-0000-0000-0000-000000000000'
    FROM DemoLocationIds AS originSeed
    INNER JOIN dbo.Locations AS origin ON origin.Id = originSeed.LocationId AND origin.Active = 1
    CROSS JOIN DemoLocationIds AS destinationSeed
    INNER JOIN dbo.Locations AS destination ON destination.Id = destinationSeed.LocationId AND destination.Active = 1
    WHERE origin.Id <> destination.Id
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.RouteTransportCapabilities AS existing
          WHERE existing.OriginLocationId = origin.Id
            AND existing.DestinationLocationId = destination.Id
      );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.FlightBookings WHERE BookingReference = 'DEMO-EQ-9001')
    INSERT INTO dbo.FlightBookings (Id, BookingReference, AirlineId, FlightNumber, OriginAirportId, DestinationAirportId, ScheduledDepartureAt, ScheduledArrivalAt, ConfirmationStatus)
    VALUES ('66666666-6666-4666-8666-666666666666', 'DEMO-EQ-9001', '66666666-6666-4666-8666-666666666665', 'EQ9001', '66666666-6666-4666-8666-666666666663', '66666666-6666-4666-8666-666666666664', DATEADD(day, 20, SYSUTCDATETIME()), DATEADD(day, 20, DATEADD(hour, 2, SYSUTCDATETIME())), 'PENDING');

IF NOT EXISTS (SELECT 1 FROM dbo.FlightBookings WHERE BookingReference = 'DEMO-EQ-9002')
    INSERT INTO dbo.FlightBookings (Id, BookingReference, AirlineId, FlightNumber, OriginAirportId, DestinationAirportId, ScheduledDepartureAt, ScheduledArrivalAt, ConfirmationStatus)
    VALUES ('66666666-6666-4666-8666-666666666667', 'DEMO-EQ-9002', '66666666-6666-4666-8666-666666666665', 'EQ9002', '66666666-6666-4666-8666-666666666664', '66666666-6666-4666-8666-666666666663', DATEADD(day, 21, SYSUTCDATETIME()), DATEADD(day, 21, DATEADD(hour, 2, SYSUTCDATETIME())), 'PENDING');
GO

