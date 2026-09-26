/* =========================================================
   IncidentDb — owned by Incident Service
   Owns: Incidents, IncidentAttachments, OutboxMessages, AuditLogs
   ========================================================= */

CREATE TABLE Incidents (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    IncidentNo          VARCHAR(50)      NOT NULL,
    TripId              UNIQUEIDENTIFIER NOT NULL,   -- logical ref -> PlanningDb.TransportTrips
    RouteLegId          UNIQUEIDENTIFIER NULL,
    CheckpointId        UNIQUEIDENTIFIER NULL,
    HorseId             UNIQUEIDENTIFIER NULL,       -- logical ref -> BookingDb.Horses
    ReportedByUserId    UNIQUEIDENTIFIER NOT NULL,
    Type                VARCHAR(50)      NOT NULL,   -- VEHICLE_BREAKDOWN, TRAFFIC_DELAY, WEATHER,
                                                      -- HORSE_MEDICAL, BORDER_ISSUE, OTHER
    Severity            VARCHAR(30)      NOT NULL,   -- LOW, MEDIUM, HIGH, CRITICAL
    Status              VARCHAR(30)      NOT NULL,   -- OPEN, INVESTIGATING, RESOLVED, CANCELLED, CLOSED
    LocationId          UNIQUEIDENTIFIER NULL,
    Latitude            DECIMAL(10,7)    NULL,
    Longitude           DECIMAL(10,7)    NULL,
    Title               NVARCHAR(255)    NOT NULL,
    Description         NVARCHAR(4000)   NOT NULL,
    OccurredAt          DATETIME2(3)     NOT NULL,
    ResolvedAt          DATETIME2(3)     NULL,
    ResolutionNote      NVARCHAR(4000)   NULL,
    CreatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    VersionNo           INT              NOT NULL DEFAULT 1,   -- ADDED: reporter + investigator both update this row
    CONSTRAINT PK_Incidents PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Incidents_No UNIQUE (IncidentNo),
    CONSTRAINT CK_Incidents_Severity CHECK (Severity IN ('LOW','MEDIUM','HIGH','CRITICAL')),
    CONSTRAINT CK_Incidents_Status CHECK (Status IN ('OPEN','INVESTIGATING','RESOLVED','CANCELLED','CLOSED'))
);
CREATE INDEX IX_Incidents_TripId ON Incidents (TripId);
CREATE INDEX IX_Incidents_Status ON Incidents (Status);
CREATE INDEX IX_Incidents_Severity ON Incidents (Severity);

CREATE TABLE IncidentAttachments (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    IncidentId          UNIQUEIDENTIFIER NOT NULL,
    FileName            NVARCHAR(255)    NOT NULL,
    FileUrl             VARCHAR(1000)    NOT NULL,
    MimeType            VARCHAR(100)     NULL,
    UploadedByUserId    UNIQUEIDENTIFIER NOT NULL,
    UploadedAt          DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_IncidentAttachments PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_IncidentAttachments_Incidents FOREIGN KEY (IncidentId) REFERENCES Incidents (Id)
);
CREATE INDEX IX_IncidentAttachments_IncidentId ON IncidentAttachments (IncidentId);

/* OutboxMessages — producer of Incident.IncidentCreated */
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
    CONSTRAINT PK_OutboxMessages_Incident PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_OutboxMessages_Incident_Status ON OutboxMessages (Status, CreatedAt);

/* AuditLogs — Incident status transitions (OPEN -> INVESTIGATING -> RESOLVED/CANCELLED -> CLOSED) */
CREATE TABLE AuditLogs (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    EntityType      VARCHAR(100)     NOT NULL,
    EntityId        UNIQUEIDENTIFIER NOT NULL,
    Action          VARCHAR(50)      NOT NULL,
    OldState        VARCHAR(50)      NULL,
    NewState        VARCHAR(50)      NULL,
    ActorUserId     UNIQUEIDENTIFIER NOT NULL,
    Reason          NVARCHAR(2000)   NULL,
    CorrelationId   UNIQUEIDENTIFIER NULL,
    OccurredAt      DATETIME2(3)     NOT NULL,
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AuditLogs_Incident PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_AuditLogs_Incident_Entity ON AuditLogs (EntityType, EntityId);
