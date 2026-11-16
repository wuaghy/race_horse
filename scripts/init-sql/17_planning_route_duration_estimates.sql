USE [PlanningDb];
GO

IF OBJECT_ID(N'dbo.RouteTransportCapabilityModes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RouteTransportCapabilityModes (
        RouteTransportCapabilityId UNIQUEIDENTIFIER NOT NULL,
        Mode VARCHAR(20) NOT NULL,
        MinDurationMinutes INT NOT NULL,
        MaxDurationMinutes INT NOT NULL,
        CONSTRAINT PK_RouteTransportCapabilityModes PRIMARY KEY CLUSTERED (RouteTransportCapabilityId, Mode),
        CONSTRAINT FK_RouteTransportCapabilityModes_Capability FOREIGN KEY (RouteTransportCapabilityId)
            REFERENCES dbo.RouteTransportCapabilities(Id) ON DELETE CASCADE,
        CONSTRAINT CK_RouteTransportCapabilityModes_Mode CHECK (Mode IN ('ROAD', 'AIR', 'MIXED')),
        CONSTRAINT CK_RouteTransportCapabilityModes_PositiveRange CHECK (MinDurationMinutes > 0 AND MaxDurationMinutes >= MinDurationMinutes)
    );
END;
GO
