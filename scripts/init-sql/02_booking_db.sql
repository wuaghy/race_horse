USE [BookingDb];
GO

/* =========================================================
   BookingDb — owned by Booking Service
   Owns: Customers, Horses, TransportRequests, TransportRequestHorses,
         TransportOrders, OutboxMessages, AuditLogs
   No cross-service FK: CountryId/LocationId/UserId/ManagerUserId
   are logical UUID references only (owned by Identity/Planning).
   ========================================================= */

CREATE TABLE Customers (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    CustomerCode    VARCHAR(50)      NOT NULL,
    Name            NVARCHAR(255)    NOT NULL,
    CustomerType    VARCHAR(30)      NOT NULL,      -- CLUB, INDIVIDUAL_OWNER, BREEDER, AGENT
    ContactPerson   NVARCHAR(200)    NULL,
    Phone           VARCHAR(50)      NULL,
    Email           VARCHAR(255)     NULL,
    Address         NVARCHAR(500)    NULL,
    CountryId       UNIQUEIDENTIFIER NULL,          -- logical ref -> PlanningDb.Countries
    Status          VARCHAR(30)      NOT NULL,      -- ACTIVE, SUSPENDED
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    VersionNo       INT              NOT NULL DEFAULT 1,
    CONSTRAINT PK_Customers PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Customers_Code UNIQUE (CustomerCode)
);
CREATE INDEX IX_Customers_Status ON Customers (Status);

CREATE TABLE Horses (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    HorseCode           VARCHAR(50)      NOT NULL,
    OwnerCustomerId     UNIQUEIDENTIFIER NOT NULL,
    HorseName           NVARCHAR(200)    NOT NULL,
    RegistrationNumber  VARCHAR(100)     NULL,
    PassportNumber      VARCHAR(100)     NULL,
    Breed               NVARCHAR(100)    NULL,
    Sex                 VARCHAR(30)      NULL,      -- STALLION, MARE, GELDING
    DateOfBirth         DATE             NULL,
    Color               NVARCHAR(100)    NULL,
    CountryOfOriginId   UNIQUEIDENTIFIER NULL,       -- logical ref -> PlanningDb.Countries
    SpecialRequirements NVARCHAR(1000)   NULL,
    Status              VARCHAR(30)      NOT NULL,   -- ACTIVE, IN_TRANSIT, RETIRED, DECEASED
    CreatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    VersionNo           INT              NOT NULL DEFAULT 1,
    CONSTRAINT PK_Horses PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Horses_Code UNIQUE (HorseCode),
    CONSTRAINT FK_Horses_Customers FOREIGN KEY (OwnerCustomerId) REFERENCES Customers (Id),
    CONSTRAINT CK_Horses_DateOfBirth CHECK (DateOfBirth IS NULL OR DateOfBirth <= CAST(SYSUTCDATETIME() AS DATE))
);
CREATE INDEX IX_Horses_OwnerCustomerId ON Horses (OwnerCustomerId);
CREATE INDEX IX_Horses_Status ON Horses (Status);

CREATE TABLE TransportRequests (
    Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    RequestNo               VARCHAR(50)      NOT NULL,
    CustomerId              UNIQUEIDENTIFIER NOT NULL,
    CreatedByUserId         UNIQUEIDENTIFIER NOT NULL,   -- logical ref -> IdentityDb.Users
    AssignedManagerUserId   UNIQUEIDENTIFIER NULL,       -- logical ref -> IdentityDb.Users
    OriginLocationId        UNIQUEIDENTIFIER NULL,       -- logical ref -> PlanningDb.Locations
    DestinationLocationId   UNIQUEIDENTIFIER NULL,       -- logical ref -> PlanningDb.Locations
    RequestedDepartureAt    DATETIME2(3)     NOT NULL,
    RequestedArrivalAt      DATETIME2(3)     NULL,
    PreferredTransportMode  VARCHAR(30)      NULL,       -- ROAD, AIR, MIXED
    SpecialRequirements     NVARCHAR(2000)   NULL,
    Notes                   NVARCHAR(2000)   NULL,
    Status                  VARCHAR(40)      NOT NULL,   -- DRAFT, SUBMITTED, UNDER_REVIEW,
                                                          -- NEED_INFORMATION, APPROVED, REJECTED, CANCELLED
    RejectionReason         NVARCHAR(1000)   NULL,
    SubmittedAt             DATETIME2(3)     NULL,
    ReviewedAt              DATETIME2(3)     NULL,
    ApprovedAt              DATETIME2(3)     NULL,
    CreatedAt               DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt               DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    VersionNo               INT              NOT NULL DEFAULT 1,
    CONSTRAINT PK_TransportRequests PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_TransportRequests_No UNIQUE (RequestNo),
    CONSTRAINT FK_TransportRequests_Customers FOREIGN KEY (CustomerId) REFERENCES Customers (Id),
    CONSTRAINT CK_TransportRequests_Status CHECK (Status IN
        ('DRAFT','SUBMITTED','UNDER_REVIEW','NEED_INFORMATION','APPROVED','REJECTED','CANCELLED')),
    CONSTRAINT CK_TransportRequests_ArrivalAfterDeparture CHECK
        (RequestedArrivalAt IS NULL OR RequestedArrivalAt > RequestedDepartureAt)
);
CREATE INDEX IX_TransportRequests_CustomerId ON TransportRequests (CustomerId);
CREATE INDEX IX_TransportRequests_Status ON TransportRequests (Status);
CREATE INDEX IX_TransportRequests_AssignedManagerUserId ON TransportRequests (AssignedManagerUserId);

CREATE TABLE TransportRequestHorses (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    RequestId       UNIQUEIDENTIFIER NOT NULL,
    HorseId         UNIQUEIDENTIFIER NOT NULL,
    SpecialHandling NVARCHAR(1000)   NULL,
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TransportRequestHorses PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_TRH_Requests FOREIGN KEY (RequestId) REFERENCES TransportRequests (Id),
    CONSTRAINT FK_TRH_Horses FOREIGN KEY (HorseId) REFERENCES Horses (Id),
    CONSTRAINT UQ_TRH_Request_Horse UNIQUE (RequestId, HorseId)
);
-- Business rule (docs/DATABASE.md §5): a Request must have >=1 Horse before submission.
-- Enforced in the application layer at the SUBMIT transition, not as a DB constraint
-- (a request legitimately has zero horses while still DRAFT).

CREATE TABLE TransportOrders (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    OrderNo         VARCHAR(50)      NOT NULL,
    RequestId       UNIQUEIDENTIFIER NOT NULL,
    ApprovedByUserId UNIQUEIDENTIFIER NOT NULL,
    ApprovedAt      DATETIME2(3)     NOT NULL,
    Status          VARCHAR(40)      NOT NULL,      -- CREATED, PLANNING, READY_FOR_EXECUTION,
                                                     -- IN_PROGRESS, COMPLETED, CANCELLED
    QuotedAmount    DECIMAL(18,2)    NULL,
    CurrencyCode    VARCHAR(10)      NULL,
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    VersionNo       INT              NOT NULL DEFAULT 1,
    CONSTRAINT PK_TransportOrders PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_TransportOrders_No UNIQUE (OrderNo),
    CONSTRAINT UQ_TransportOrders_RequestId UNIQUE (RequestId),   -- 1-1 with Request
    CONSTRAINT FK_TransportOrders_Requests FOREIGN KEY (RequestId) REFERENCES TransportRequests (Id),
    CONSTRAINT CK_TransportOrders_Status CHECK (Status IN
        ('CREATED','PLANNING','READY_FOR_EXECUTION','IN_PROGRESS','COMPLETED','CANCELLED'))
);
CREATE INDEX IX_TransportOrders_Status ON TransportOrders (Status);

/* ---------------------------------------------------------
   OutboxMessages — reliable event publishing (docs/INTEGRATION_EVENTS.md §5)
   Booking is the producer of: Booking.RequestApproved, Booking.OrderCreated
   --------------------------------------------------------- */
CREATE TABLE OutboxMessages (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    EventType       VARCHAR(150)     NOT NULL,      -- e.g. 'Booking.RequestApproved'
    AggregateType   VARCHAR(100)     NOT NULL,      -- 'TransportRequest','TransportOrder'
    AggregateId     UNIQUEIDENTIFIER NOT NULL,
    Payload         NVARCHAR(MAX)    NOT NULL,      -- full event envelope JSON
    CorrelationId   UNIQUEIDENTIFIER NOT NULL,
    OccurredAt      DATETIME2(3)     NOT NULL,
    Status          VARCHAR(20)      NOT NULL DEFAULT 'PENDING',  -- PENDING, PUBLISHED, FAILED
    RetryCount      INT              NOT NULL DEFAULT 0,
    ProcessedAt     DATETIME2(3)     NULL,
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_OutboxMessages PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_OutboxMessages_Status_CreatedAt ON OutboxMessages (Status, CreatedAt);

/* ---------------------------------------------------------
   AuditLogs — Request/Order state transitions (docs/WORKFLOW_STATE.md §10)
   --------------------------------------------------------- */
CREATE TABLE AuditLogs (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    EntityType      VARCHAR(100)     NOT NULL,      -- 'TransportRequest','TransportOrder'
    EntityId        UNIQUEIDENTIFIER NOT NULL,
    Action          VARCHAR(50)      NOT NULL,
    OldState        VARCHAR(50)      NULL,
    NewState        VARCHAR(50)      NULL,
    ActorUserId     UNIQUEIDENTIFIER NOT NULL,
    Reason          NVARCHAR(2000)   NULL,
    CorrelationId   UNIQUEIDENTIFIER NULL,
    OccurredAt      DATETIME2(3)     NOT NULL,
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AuditLogs_Booking PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_AuditLogs_Booking_Entity ON AuditLogs (EntityType, EntityId);
