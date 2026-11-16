USE [PlanningDb];
GO

IF OBJECT_ID(N'dbo.TripIncidentFlags', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TripIncidentFlags (
        IncidentId UNIQUEIDENTIFIER NOT NULL,
        TripId UNIQUEIDENTIFIER NOT NULL,
        Severity VARCHAR(30) NOT NULL,
        Status VARCHAR(30) NOT NULL,
        UpdatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TripIncidentFlags PRIMARY KEY CLUSTERED (IncidentId)
    );
    CREATE INDEX IX_TripIncidentFlags_Readiness ON dbo.TripIncidentFlags (TripId, Severity, Status);
END;
GO
