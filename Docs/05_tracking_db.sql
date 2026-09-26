/* =========================================================
   TrackingDb — owned by Tracking Service
   Owns: TripEvents, TripLocations, HorseHealthLogs
   Append-only domain: no AuditLogs/Outbox producer state-machine
   beyond the two integration events below, which are published
   straight from the command handler since these rows are themselves
   immutable facts (no separate "outbox" needed per docs — still
   included for reliability, matching docs/INTEGRATION_EVENTS.md §5).
   ========================================================= */

CREATE TABLE TripEvents (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    TripId              UNIQUEIDENTIFIER NOT NULL,   -- logical ref -> PlanningDb.TransportTrips
    RouteLegId          UNIQUEIDENTIFIER NULL,
    CheckpointId        UNIQUEIDENTIFIER NULL,
    EventType           VARCHAR(50)      NOT NULL,   -- DEPARTED, ARRIVED_CHECKPOINT, CLEARED_CUSTOMS,
                                                      -- DELIVERED, DELAYED, MILESTONE
    EventTime           DATETIME2(3)     NOT NULL,
    Latitude            DECIMAL(10,7)    NULL,
    Longitude           DECIMAL(10,7)    NULL,
    Note                NVARCHAR(2000)   NULL,
    CreatedByUserId     UNIQUEIDENTIFIER NOT NULL,
    CreatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TripEvents PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_TripEvents_TripId_EventTime ON TripEvents (TripId, EventTime);

CREATE TABLE TripLocations (
    Id          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    TripId      UNIQUEIDENTIFIER NOT NULL,
    RecordedAt  DATETIME2(3)     NOT NULL,
    Latitude    DECIMAL(10,7)    NOT NULL,
    Longitude   DECIMAL(10,7)    NOT NULL,
    SpeedKmh    DECIMAL(8,2)     NULL,
    Heading     DECIMAL(8,2)     NULL,
    Source      VARCHAR(50)      NULL,      -- MANUAL, DRIVER_APP, DEVICE
    CONSTRAINT PK_TripLocations PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_TripLocations_TripId_RecordedAt ON TripLocations (TripId, RecordedAt);
-- MVP note (docs/PRODUCT_SCOPE.md §5): "real-time GPS" only applies once a device/driver-app
-- source actually populates Source='DEVICE'; a manual-milestone-only trip must not display
-- itself as real-time-tracked in the UI.

CREATE TABLE HorseHealthLogs (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    TripId              UNIQUEIDENTIFIER NOT NULL,
    HorseId             UNIQUEIDENTIFIER NOT NULL,   -- logical ref -> BookingDb.Horses
    CheckpointId        UNIQUEIDENTIFIER NULL,
    RecordedByUserId    UNIQUEIDENTIFIER NOT NULL,
    RecordedAt          DATETIME2(3)     NOT NULL,
    Condition           VARCHAR(50)      NOT NULL,   -- STABLE, STRESSED, NOT_EATING, ILL
    StressLevel         VARCHAR(30)      NULL,       -- LOW, MEDIUM, HIGH
    Temperature         DECIMAL(5,2)     NULL,
    Appetite            NVARCHAR(100)    NULL,
    Medication          NVARCHAR(1000)   NULL,
    Observation         NVARCHAR(2000)   NULL,
    ImageUrl            VARCHAR(1000)    NULL,
    HealthAlert         BIT              NOT NULL DEFAULT 0,
    CONSTRAINT PK_HorseHealthLogs PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_HorseHealthLogs_TripId ON HorseHealthLogs (TripId);
CREATE INDEX IX_HorseHealthLogs_HorseId_RecordedAt ON HorseHealthLogs (HorseId, RecordedAt);
CREATE INDEX IX_HorseHealthLogs_HealthAlert ON HorseHealthLogs (HealthAlert) WHERE HealthAlert = 1;
-- HealthAlert = 1 is the trigger condition for the Tracking.HealthAlertRaised event
-- consumed by Notification and Incident (docs/INTEGRATION_EVENTS.md §4).

/* ---------------------------------------------------------
   OutboxMessages — producer of Tracking.TripDeparted, Tracking.CheckpointArrived,
   Tracking.HealthAlertRaised
   --------------------------------------------------------- */
CREATE TABLE OutboxMessages (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    EventType       VARCHAR(150)     NOT NULL,
    AggregateType   VARCHAR(100)     NOT NULL,
    AggregateId     UNIQUEIDENTIFIER NOT NULL,
    Payload         NVARCHAR(MAX)    NOT NULL,
    CorrelationId   UNIQUEIDENTIFIER NOT NULL,
    OccurredAt      DATETIME2(3)     NOT NULL,
    Status          VARCHAR(20)      NOT NULL DEFAULT 'PENDING',
    RetryCount      INT              NOT NULL DEFAULT 0,
    ProcessedAt     DATETIME2(3)     NULL,
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_OutboxMessages_Tracking PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_OutboxMessages_Tracking_Status ON OutboxMessages (Status, CreatedAt);
