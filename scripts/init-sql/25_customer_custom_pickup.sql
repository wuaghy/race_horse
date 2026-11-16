USE [BookingDb];
GO

IF COL_LENGTH('dbo.TransportRequests', 'CustomPickupLatitude') IS NULL
    ALTER TABLE dbo.TransportRequests ADD CustomPickupLatitude DECIMAL(10,7) NULL;
IF COL_LENGTH('dbo.TransportRequests', 'CustomPickupLongitude') IS NULL
    ALTER TABLE dbo.TransportRequests ADD CustomPickupLongitude DECIMAL(10,7) NULL;
IF COL_LENGTH('dbo.TransportRequests', 'CustomPickupLabel') IS NULL
    ALTER TABLE dbo.TransportRequests ADD CustomPickupLabel NVARCHAR(300) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TransportRequests_CustomPickupCoordinates')
    ALTER TABLE dbo.TransportRequests ADD CONSTRAINT CK_TransportRequests_CustomPickupCoordinates CHECK
    ((CustomPickupLatitude IS NULL AND CustomPickupLongitude IS NULL) OR
     (CustomPickupLatitude BETWEEN -90 AND 90 AND CustomPickupLongitude BETWEEN -180 AND 180));

USE [PlanningDb];
GO

IF COL_LENGTH('dbo.TransportTrips', 'CustomPickupLatitude') IS NULL
    ALTER TABLE dbo.TransportTrips ADD CustomPickupLatitude DECIMAL(10,7) NULL;
IF COL_LENGTH('dbo.TransportTrips', 'CustomPickupLongitude') IS NULL
    ALTER TABLE dbo.TransportTrips ADD CustomPickupLongitude DECIMAL(10,7) NULL;
IF COL_LENGTH('dbo.TransportTrips', 'CustomPickupLabel') IS NULL
    ALTER TABLE dbo.TransportTrips ADD CustomPickupLabel NVARCHAR(300) NULL;
