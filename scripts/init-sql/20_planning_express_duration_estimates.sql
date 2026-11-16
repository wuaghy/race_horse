USE [PlanningDb];
GO

IF COL_LENGTH(N'dbo.RouteTransportCapabilityModes', N'MinExpressDurationMinutes') IS NULL
    ALTER TABLE dbo.RouteTransportCapabilityModes ADD MinExpressDurationMinutes INT NULL;
IF COL_LENGTH(N'dbo.RouteTransportCapabilityModes', N'MaxExpressDurationMinutes') IS NULL
    ALTER TABLE dbo.RouteTransportCapabilityModes ADD MaxExpressDurationMinutes INT NULL;
GO
