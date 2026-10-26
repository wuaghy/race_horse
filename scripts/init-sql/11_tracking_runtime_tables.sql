USE [TrackingDb];
GO

/* Additive durable tables used by Tracking.Infrastructure EF repositories.
   The original Tracking schema (TripEvents, TripLocations, HorseHealthLogs,
   OutboxMessages) is retained for compatibility with the published contract. */

IF OBJECT_ID(N'dbo.TripExecutions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TripExecutions (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TripExecutions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        TripId UNIQUEIDENTIFIER NOT NULL,
        TripNo NVARCHAR(50) NOT NULL,
        Status NVARCHAR(30) NOT NULL,
        DepartureAt DATETIME2 NULL,
        ArrivalAt DATETIME2 NULL,
        CurrentEta DATETIME2 NULL,
        CurrentCheckpointId UNIQUEIDENTIFIER NULL,
        CurrentLatitude DECIMAL(10,7) NULL,
        CurrentLongitude DECIMAL(10,7) NULL,
        LastLocationUpdateAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TripExecutions_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_TripExecutions_UpdatedAt DEFAULT SYSUTCDATETIME(),
        VersionNo INT NOT NULL DEFAULT 1
    );
    CREATE UNIQUE INDEX UX_TripExecutions_TripId ON dbo.TripExecutions(TripId);
END;
GO

IF OBJECT_ID(N'dbo.CheckpointEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CheckpointEvents (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CheckpointEvents PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        TripExecutionId UNIQUEIDENTIFIER NOT NULL,
        LocationId UNIQUEIDENTIFIER NULL,
        SequenceNo INT NOT NULL,
        CheckpointType NVARCHAR(50) NOT NULL,
        Status NVARCHAR(30) NOT NULL,
        PlannedArrivalAt DATETIME2 NULL,
        ActualArrivalAt DATETIME2 NULL,
        PlannedDepartureAt DATETIME2 NULL,
        ActualDepartureAt DATETIME2 NULL,
        ArrivalLatitude DECIMAL(10,7) NULL,
        ArrivalLongitude DECIMAL(10,7) NULL,
        Notes NVARCHAR(2000) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_CheckpointEvents_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_CheckpointEvents_UpdatedAt DEFAULT SYSUTCDATETIME(),
        VersionNo INT NOT NULL DEFAULT 1
    );
    CREATE INDEX IX_CheckpointEvents_TripExecutionId ON dbo.CheckpointEvents(TripExecutionId, SequenceNo);
END;
GO

IF OBJECT_ID(N'dbo.GpsPoints', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GpsPoints (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GpsPoints PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        TripExecutionId UNIQUEIDENTIFIER NOT NULL,
        Latitude DECIMAL(10,7) NOT NULL,
        Longitude DECIMAL(10,7) NOT NULL,
        Altitude DECIMAL(10,2) NULL,
        Accuracy DECIMAL(8,2) NULL,
        Speed DECIMAL(8,2) NULL,
        Heading DECIMAL(8,2) NULL,
        RecordedAt DATETIME2 NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_GpsPoints_CreatedAt DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_GpsPoints_TripExecutionId ON dbo.GpsPoints(TripExecutionId, RecordedAt DESC);
END;
GO

IF OBJECT_ID(N'dbo.LocationHistories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LocationHistories (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_LocationHistories PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        TripExecutionId UNIQUEIDENTIFIER NOT NULL,
        Latitude DECIMAL(10,7) NOT NULL,
        Longitude DECIMAL(10,7) NOT NULL,
        LocationId UNIQUEIDENTIFIER NULL,
        LocationName NVARCHAR(200) NULL,
        ArrivedAt DATETIME2 NOT NULL,
        DepartedAt DATETIME2 NULL,
        SequenceNo INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_LocationHistories_CreatedAt DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_LocationHistories_TripExecutionId ON dbo.LocationHistories(TripExecutionId, SequenceNo);
END;
GO

IF OBJECT_ID(N'dbo.EstimatedTimesOfArrival', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EstimatedTimesOfArrival (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EstimatedTimesOfArrival PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        TripExecutionId UNIQUEIDENTIFIER NOT NULL,
        CurrentEta DATETIME2 NOT NULL,
        PreviousEta DATETIME2 NULL,
        DistanceRemainingKm DECIMAL(10,2) NULL,
        EstimatedMinutesRemaining INT NULL,
        AverageSpeedKph DECIMAL(8,2) NULL,
        DelayReason NVARCHAR(500) NULL,
        OnTrack BIT NULL,
        CalculatedAt DATETIME2 NOT NULL CONSTRAINT DF_EstimatedTimesOfArrival_CalculatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_EstimatedTimesOfArrival_UpdatedAt DEFAULT SYSUTCDATETIME()
    );
    CREATE UNIQUE INDEX UX_EstimatedTimesOfArrival_TripExecutionId ON dbo.EstimatedTimesOfArrival(TripExecutionId);
END;
GO

IF OBJECT_ID(N'dbo.HealthLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HealthLogs (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_HealthLogs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        TripExecutionId UNIQUEIDENTIFIER NOT NULL,
        HorseId UNIQUEIDENTIFIER NULL,
        RecordedAt DATETIME2 NOT NULL,
        ConditionType NVARCHAR(50) NOT NULL,
        StressLevel INT NULL,
        BodyTemperature DECIMAL(5,2) NULL,
        HeartRate INT NULL,
        RespiratoryRate INT NULL,
        Observation NVARCHAR(2000) NULL,
        PhotoUrl NVARCHAR(1000) NULL,
        RecordedByUserId NVARCHAR(100) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_HealthLogs_CreatedAt DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_HealthLogs_TripExecutionId ON dbo.HealthLogs(TripExecutionId, RecordedAt DESC);
END;
GO

IF OBJECT_ID(N'dbo.HealthAlerts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HealthAlerts (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_HealthAlerts PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        TripExecutionId UNIQUEIDENTIFIER NOT NULL,
        HorseId UNIQUEIDENTIFIER NULL,
        AlertType NVARCHAR(50) NOT NULL,
        Severity NVARCHAR(30) NOT NULL,
        Message NVARCHAR(2000) NOT NULL,
        MetricValue INT NULL,
        MetricThreshold INT NULL,
        Recommendation NVARCHAR(2000) NULL,
        Acknowledged BIT NOT NULL DEFAULT 0,
        AcknowledgedAt DATETIME2 NULL,
        AcknowledgedByUserId NVARCHAR(100) NULL,
        AlertedAt DATETIME2 NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_HealthAlerts_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_HealthAlerts_UpdatedAt DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_HealthAlerts_TripExecutionId ON dbo.HealthAlerts(TripExecutionId, AlertedAt DESC);
END;
GO

IF OBJECT_ID(N'dbo.HealthStatuses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HealthStatuses (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_HealthStatuses PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        TripExecutionId UNIQUEIDENTIFIER NOT NULL,
        HorseId UNIQUEIDENTIFIER NULL,
        CurrentStatus NVARCHAR(30) NOT NULL,
        CurrentStressLevel INT NULL,
        AlertCount INT NULL,
        AcknowledgedAlertCount INT NULL,
        LastHealthCheckAt DATETIME2 NULL,
        LastAlertAt DATETIME2 NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_HealthStatuses_UpdatedAt DEFAULT SYSUTCDATETIME()
    );
    CREATE UNIQUE INDEX UX_HealthStatuses_TripExecutionId ON dbo.HealthStatuses(TripExecutionId);
END;
GO
