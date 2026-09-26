/* =========================================================
   HandoverDb — owned by Handover Service
   Owns: HandoverRecords, HandoverHorses, TripCostItems,
         TripRevenueItems, OutboxMessages, AuditLogs
   ========================================================= */

CREATE TABLE HandoverRecords (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    HandoverNo          VARCHAR(50)      NOT NULL,
    TripId              UNIQUEIDENTIFIER NOT NULL,   -- logical ref -> PlanningDb.TransportTrips (1:1)
    Status              VARCHAR(40)      NOT NULL,   -- DRAFT, PENDING_ACCEPTANCE, ACCEPTED, DISPUTED, COMPLETED
    ReceiverName        NVARCHAR(200)    NULL,
    ReceiverPhone       VARCHAR(50)      NULL,
    ReceiverEmail       VARCHAR(255)     NULL,
    HandoverLocationId  UNIQUEIDENTIFIER NULL,       -- logical ref -> PlanningDb.Locations
    ScheduledAt         DATETIME2(3)     NULL,
    ActualAt            DATETIME2(3)     NULL,
    Notes               NVARCHAR(3000)   NULL,
    SignatureUrl        VARCHAR(1000)    NULL,
    CreatedByUserId     UNIQUEIDENTIFIER NOT NULL,
    CompletedByUserId   UNIQUEIDENTIFIER NULL,
    CreatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    VersionNo           INT              NOT NULL DEFAULT 1,   -- ADDED: creator (driver) + completer (manager) both update
    CONSTRAINT PK_HandoverRecords PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_HandoverRecords_No UNIQUE (HandoverNo),
    CONSTRAINT UQ_HandoverRecords_TripId UNIQUE (TripId),
    CONSTRAINT CK_HandoverRecords_Status CHECK (Status IN
        ('DRAFT','PENDING_ACCEPTANCE','ACCEPTED','DISPUTED','COMPLETED'))
);
CREATE INDEX IX_HandoverRecords_Status ON HandoverRecords (Status);

CREATE TABLE HandoverHorses (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    HandoverId          UNIQUEIDENTIFIER NOT NULL,
    HorseId             UNIQUEIDENTIFIER NOT NULL,   -- logical ref -> BookingDb.Horses
    Status              VARCHAR(30)      NOT NULL,   -- PENDING, ACCEPTED, DISPUTED
    Condition           NVARCHAR(2000)   NULL,
    IssueNote           NVARCHAR(2000)   NULL,
    EvidenceImageUrl    VARCHAR(1000)    NULL,
    AcceptedAt          DATETIME2(3)     NULL,
    CONSTRAINT PK_HandoverHorses PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_HandoverHorses_HandoverRecords FOREIGN KEY (HandoverId) REFERENCES HandoverRecords (Id),
    CONSTRAINT UQ_HandoverHorses_Handover_Horse UNIQUE (HandoverId, HorseId)
);
-- Business rule (docs/DATABASE.md §5): Trip completion requires every HandoverHorse row to be
-- ACCEPTED or a formally resolved DISPUTED row -> enforced in the Handover service's
-- CompleteHandover command, which scans all rows for the HandoverId before allowing COMPLETED.

CREATE TABLE TripCostItems (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    TripId              UNIQUEIDENTIFIER NOT NULL,
    Category            NVARCHAR(100)    NOT NULL,   -- FUEL, TOLL, QUARANTINE_FEE, FLIGHT, LABOR, EMERGENCY
    Description         NVARCHAR(1000)   NULL,
    Amount              DECIMAL(18,2)    NOT NULL,
    CurrencyCode        VARCHAR(10)      NOT NULL,
    Status              VARCHAR(30)      NOT NULL,   -- PENDING, CONFIRMED, VOID
    IncurredAt          DATETIME2(3)     NULL,
    CreatedByUserId     UNIQUEIDENTIFIER NOT NULL,
    CreatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TripCostItems PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_TripCostItems_Amount CHECK (Amount >= 0)
);
CREATE INDEX IX_TripCostItems_TripId ON TripCostItems (TripId);

CREATE TABLE TripRevenueItems (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    TripId              UNIQUEIDENTIFIER NOT NULL,
    Category            NVARCHAR(100)    NOT NULL,   -- BASE_FARE, EXTRA_SERVICE, SURCHARGE
    Description         NVARCHAR(1000)   NULL,
    Amount              DECIMAL(18,2)    NOT NULL,
    CurrencyCode        VARCHAR(10)      NOT NULL,
    Status              VARCHAR(30)      NOT NULL,   -- PENDING, RECOGNIZED, VOID
    RecognizedAt        DATETIME2(3)     NULL,
    CreatedByUserId     UNIQUEIDENTIFIER NOT NULL,
    CreatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TripRevenueItems PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_TripRevenueItems_Amount CHECK (Amount >= 0)
);
CREATE INDEX IX_TripRevenueItems_TripId ON TripRevenueItems (TripId);

/* OutboxMessages — producer of Handover.HandoverCompleted */
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
    CONSTRAINT PK_OutboxMessages_Handover PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_OutboxMessages_Handover_Status ON OutboxMessages (Status, CreatedAt);

/* AuditLogs — Handover accept/dispute/completion transitions */
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
    CONSTRAINT PK_AuditLogs_Handover PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_AuditLogs_Handover_Entity ON AuditLogs (EntityType, EntityId);
