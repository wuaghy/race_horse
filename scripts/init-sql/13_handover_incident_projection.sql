USE [HandoverDb];
GO

IF OBJECT_ID(N'dbo.HandoverIncidentFlags', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HandoverIncidentFlags (
        IncidentId UNIQUEIDENTIFIER NOT NULL,
        TripId UNIQUEIDENTIFIER NOT NULL,
        Severity VARCHAR(30) NOT NULL,
        Status VARCHAR(30) NOT NULL,
        UpdatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_HandoverIncidentFlags PRIMARY KEY CLUSTERED (IncidentId)
    );
    CREATE INDEX IX_HandoverIncidentFlags_Readiness ON dbo.HandoverIncidentFlags (TripId, Severity, Status);
END;
GO
