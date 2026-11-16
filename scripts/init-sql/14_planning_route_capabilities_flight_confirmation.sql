USE [PlanningDb];
GO

IF COL_LENGTH(N'dbo.FlightBookings', N'ConfirmationStatus') IS NULL
    ALTER TABLE dbo.FlightBookings ADD ConfirmationStatus VARCHAR(20) NOT NULL CONSTRAINT DF_FlightBookings_ConfirmationStatus DEFAULT 'PENDING' WITH VALUES;
IF COL_LENGTH(N'dbo.FlightBookings', N'ConfirmedAt') IS NULL
    ALTER TABLE dbo.FlightBookings ADD ConfirmedAt DATETIME2(3) NULL;
IF COL_LENGTH(N'dbo.FlightBookings', N'ConfirmedByUserId') IS NULL
    ALTER TABLE dbo.FlightBookings ADD ConfirmedByUserId UNIQUEIDENTIFIER NULL;
GO

IF OBJECT_ID(N'dbo.RouteTransportCapabilities', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RouteTransportCapabilities (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_RouteTransportCapabilities_Id DEFAULT NEWSEQUENTIALID(),
        OriginLocationId UNIQUEIDENTIFIER NOT NULL,
        DestinationLocationId UNIQUEIDENTIFIER NOT NULL,
        AllowedModesJson NVARCHAR(100) NOT NULL,
        UpdatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_RouteTransportCapabilities_UpdatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedByUserId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_RouteTransportCapabilities PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_RouteTransportCapabilities_Locations UNIQUE (OriginLocationId, DestinationLocationId),
        CONSTRAINT FK_RouteTransportCapabilities_Origin FOREIGN KEY (OriginLocationId) REFERENCES dbo.Locations(Id),
        CONSTRAINT FK_RouteTransportCapabilities_Destination FOREIGN KEY (DestinationLocationId) REFERENCES dbo.Locations(Id),
        CONSTRAINT CK_RouteTransportCapabilities_DistinctLocations CHECK (OriginLocationId <> DestinationLocationId)
    );
END;
GO
