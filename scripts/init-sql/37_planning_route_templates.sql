USE PlanningDb;
GO
IF OBJECT_ID(N'dbo.RouteTemplates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RouteTemplates (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Name NVARCHAR(120) NOT NULL,
        OriginLocationId UNIQUEIDENTIFIER NOT NULL,
        DestinationLocationId UNIQUEIDENTIFIER NOT NULL,
        TransportMode VARCHAR(30) NOT NULL,
        CustomPickupLatitude DECIMAL(10,7) NULL,
        CustomPickupLongitude DECIMAL(10,7) NULL,
        RouteShapeJson NVARCHAR(MAX) NOT NULL,
        CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT FK_RouteTemplates_Origin FOREIGN KEY (OriginLocationId) REFERENCES dbo.Locations(Id),
        CONSTRAINT FK_RouteTemplates_Destination FOREIGN KEY (DestinationLocationId) REFERENCES dbo.Locations(Id)
    );
    CREATE INDEX IX_RouteTemplates_Route ON dbo.RouteTemplates (OriginLocationId, DestinationLocationId, TransportMode);
END;
GO
