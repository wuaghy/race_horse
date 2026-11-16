USE [PlanningDb];
GO

IF COL_LENGTH(N'dbo.TransportTrips', N'CancellationPending') IS NULL
    ALTER TABLE dbo.TransportTrips ADD CancellationPending BIT NOT NULL
        CONSTRAINT DF_TransportTrips_CancellationPending DEFAULT 0;
GO
