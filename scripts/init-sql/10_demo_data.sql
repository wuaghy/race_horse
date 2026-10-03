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
DECLARE @DraftRequest UNIQUEIDENTIFIER = '55555555-5555-4555-8555-555555555551';
DECLARE @SubmittedRequest UNIQUEIDENTIFIER = '55555555-5555-4555-8555-555555555552';
DECLARE @SecondQueueRequest UNIQUEIDENTIFIER = '55555555-5555-4555-8555-555555555553';
DECLARE @ReviewRequest UNIQUEIDENTIFIER = '55555555-5555-4555-8555-555555555554';
DECLARE @InformationRequest UNIQUEIDENTIFIER = '55555555-5555-4555-8555-555555555555';
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

COMMIT TRANSACTION;
GO