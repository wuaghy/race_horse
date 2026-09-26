/* =========================================================
   PlanningDb — owned by Planning Service
   Owns: Countries, Locations, TransportTrips, Vehicles, Stalls,
         Airlines, FlightBookings, RoutePlans, RoutePlanVersions,
         RouteLegs, Checkpoints, TripLegAssignments, StaffAssignments,
         HorseLegAssignments, OutboxMessages, AuditLogs
   OrderId / HorseId / UserId are logical references only.
   ========================================================= */

CREATE TABLE Countries (
    Id      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    IsoCode VARCHAR(10)      NOT NULL,
    Name    NVARCHAR(150)    NOT NULL,
    Active  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT PK_Countries PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Countries_IsoCode UNIQUE (IsoCode)
);

CREATE TABLE Locations (
    Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    LocationCode VARCHAR(50)      NULL,
    Name         NVARCHAR(255)    NOT NULL,
    LocationType VARCHAR(40)      NOT NULL,      -- STABLE, AIRPORT, BORDER_CROSSING, QUARANTINE_STATION,
                                                  -- REST_STOP, FUEL_STOP, CUSTOMER_SITE, HANDOVER_POINT
    Address      NVARCHAR(500)    NULL,
    City         NVARCHAR(150)    NULL,
    CountryId    UNIQUEIDENTIFIER NOT NULL,
    Latitude     DECIMAL(10,7)    NULL,
    Longitude    DECIMAL(10,7)    NULL,
    Active       BIT              NOT NULL DEFAULT 1,
    CONSTRAINT PK_Locations PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Locations_Code UNIQUE (LocationCode),
    CONSTRAINT FK_Locations_Countries FOREIGN KEY (CountryId) REFERENCES Countries (Id)
);
CREATE INDEX IX_Locations_CountryId ON Locations (CountryId);
CREATE INDEX IX_Locations_Type ON Locations (LocationType);

CREATE TABLE TransportTrips (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    TripNo              VARCHAR(50)      NOT NULL,
    OrderId             UNIQUEIDENTIFIER NOT NULL,   -- logical ref -> BookingDb.TransportOrders (1:1)
    Status              VARCHAR(40)      NOT NULL,   -- PLANNED, READY, DEPARTED, IN_TRANSIT, DELAYED,
                                                      -- ARRIVED, HANDED_OVER, COMPLETED, CANCELLED
    PlannedDepartureAt  DATETIME2(3)     NULL,
    PlannedArrivalAt    DATETIME2(3)     NULL,
    ActualDepartureAt   DATETIME2(3)     NULL,
    ActualArrivalAt     DATETIME2(3)     NULL,
    CurrentLocationId   UNIQUEIDENTIFIER NULL,
    CurrentLatitude     DECIMAL(10,7)    NULL,
    CurrentLongitude    DECIMAL(10,7)    NULL,
    CurrentEta          DATETIME2(3)     NULL,
    DelayMinutes        INT              NOT NULL DEFAULT 0,
    OnTime              BIT              NULL,
    CreatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    VersionNo           INT              NOT NULL DEFAULT 1,
    CONSTRAINT PK_TransportTrips PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_TransportTrips_TripNo UNIQUE (TripNo),
    CONSTRAINT UQ_TransportTrips_OrderId UNIQUE (OrderId),   -- one Trip per Order in this version
    CONSTRAINT FK_TransportTrips_Locations FOREIGN KEY (CurrentLocationId) REFERENCES Locations (Id),
    CONSTRAINT CK_TransportTrips_Status CHECK (Status IN
        ('PLANNED','READY','DEPARTED','IN_TRANSIT','DELAYED','ARRIVED','HANDED_OVER','COMPLETED','CANCELLED'))
);
CREATE INDEX IX_TransportTrips_Status ON TransportTrips (Status);
CREATE INDEX IX_TransportTrips_OrderId ON TransportTrips (OrderId);

CREATE TABLE Vehicles (
    Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    VehicleCode             VARCHAR(50)      NOT NULL,
    PlateNumber             VARCHAR(50)      NOT NULL,
    VehicleType             NVARCHAR(100)    NOT NULL,   -- HORSE_TRUCK, TRAILER, ...
    HorseCapacity           INT              NOT NULL,
    StallCapacity           INT              NOT NULL,
    TemperatureControlled   BIT              NOT NULL DEFAULT 0,
    Status                  VARCHAR(40)      NOT NULL,   -- AVAILABLE, IN_USE, MAINTENANCE, RETIRED
    CreatedAt               DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt               DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    VersionNo               INT              NOT NULL DEFAULT 1,
    CONSTRAINT PK_Vehicles PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Vehicles_Code UNIQUE (VehicleCode),
    CONSTRAINT UQ_Vehicles_Plate UNIQUE (PlateNumber),
    CONSTRAINT CK_Vehicles_Capacity CHECK (HorseCapacity > 0 AND StallCapacity > 0)
);
CREATE INDEX IX_Vehicles_Status ON Vehicles (Status);

CREATE TABLE Stalls (
    Id          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    StallCode   VARCHAR(50)      NOT NULL,
    StallType   NVARCHAR(100)    NULL,
    Capacity    INT              NOT NULL,
    Active      BIT              NOT NULL DEFAULT 1,
    Notes       NVARCHAR(1000)   NULL,
    CONSTRAINT PK_Stalls PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Stalls_Code UNIQUE (StallCode),
    CONSTRAINT CK_Stalls_Capacity CHECK (Capacity > 0)
);

CREATE TABLE Airlines (
    Id          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    AirlineCode VARCHAR(20)      NOT NULL,
    Name        NVARCHAR(200)    NOT NULL,
    Active      BIT              NOT NULL DEFAULT 1,
    CONSTRAINT PK_Airlines PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Airlines_Code UNIQUE (AirlineCode)
);

CREATE TABLE FlightBookings (
    Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    BookingReference        VARCHAR(100)     NOT NULL,
    AirlineId               UNIQUEIDENTIFIER NOT NULL,
    FlightNumber            VARCHAR(50)      NOT NULL,
    OriginAirportId         UNIQUEIDENTIFIER NOT NULL,
    DestinationAirportId    UNIQUEIDENTIFIER NOT NULL,
    ScheduledDepartureAt    DATETIME2(3)     NOT NULL,
    ScheduledArrivalAt      DATETIME2(3)     NOT NULL,
    ActualDepartureAt       DATETIME2(3)     NULL,
    ActualArrivalAt         DATETIME2(3)     NULL,
    CONSTRAINT PK_FlightBookings PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_FlightBookings_Ref UNIQUE (BookingReference),
    CONSTRAINT FK_FlightBookings_Airlines FOREIGN KEY (AirlineId) REFERENCES Airlines (Id),
    CONSTRAINT FK_FlightBookings_OriginAirport FOREIGN KEY (OriginAirportId) REFERENCES Locations (Id),
    CONSTRAINT FK_FlightBookings_DestAirport FOREIGN KEY (DestinationAirportId) REFERENCES Locations (Id),
    CONSTRAINT CK_FlightBookings_Schedule CHECK (ScheduledArrivalAt > ScheduledDepartureAt)
);

CREATE TABLE RoutePlans (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    TripId          UNIQUEIDENTIFIER NOT NULL,
    ActiveVersionId UNIQUEIDENTIFIER NULL,       -- FK added below, after RoutePlanVersions exists
    CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    VersionNo       INT              NOT NULL DEFAULT 1,   -- ADDED: optimistic concurrency on ActiveVersionId pointer
    CONSTRAINT PK_RoutePlans PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_RoutePlans_TripId UNIQUE (TripId),
    CONSTRAINT FK_RoutePlans_Trips FOREIGN KEY (TripId) REFERENCES TransportTrips (Id)
);

CREATE TABLE RoutePlanVersions (
    Id                          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    RoutePlanId                 UNIQUEIDENTIFIER NOT NULL,
    VersionNo                   INT              NOT NULL,   -- business version number, NOT a concurrency token
    Status                      VARCHAR(40)      NOT NULL,   -- DRAFT, PENDING_APPROVAL, REJECTED, APPROVED, ACTIVE, ARCHIVED
    Reason                      NVARCHAR(2000)   NULL,       -- required when created as an emergency-change version
    TotalDistanceKm             DECIMAL(12,2)    NULL,
    EstimatedDurationMinutes    INT              NULL,
    PlannedDepartureAt          DATETIME2(3)     NULL,
    PlannedArrivalAt            DATETIME2(3)     NULL,
    AdditionalCost              DECIMAL(18,2)    NOT NULL DEFAULT 0,
    CurrencyCode                VARCHAR(10)      NULL,
    CreatedByUserId              UNIQUEIDENTIFIER NOT NULL,
    ApprovedByUserId             UNIQUEIDENTIFIER NULL,
    ApprovedAt                  DATETIME2(3)     NULL,
    CreatedAt                   DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_RoutePlanVersions PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_RoutePlanVersions_RoutePlans FOREIGN KEY (RoutePlanId) REFERENCES RoutePlans (Id),
    CONSTRAINT UQ_RoutePlanVersions_Plan_VersionNo UNIQUE (RoutePlanId, VersionNo),
    CONSTRAINT CK_RoutePlanVersions_Status CHECK (Status IN
        ('DRAFT','PENDING_APPROVAL','REJECTED','APPROVED','ACTIVE','ARCHIVED'))
);
-- Business rule (docs/DATABASE.md §5): only one ACTIVE version per plan, enforced with a filtered unique index.
CREATE UNIQUE INDEX UX_RoutePlanVersions_OneActivePerPlan
    ON RoutePlanVersions (RoutePlanId)
    WHERE Status = 'ACTIVE';

ALTER TABLE RoutePlans
    ADD CONSTRAINT FK_RoutePlans_ActiveVersion FOREIGN KEY (ActiveVersionId) REFERENCES RoutePlanVersions (Id);

CREATE TABLE RouteLegs (
    Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    RoutePlanVersionId      UNIQUEIDENTIFIER NOT NULL,
    SequenceNo              INT              NOT NULL,
    OriginLocationId        UNIQUEIDENTIFIER NOT NULL,
    DestinationLocationId   UNIQUEIDENTIFIER NOT NULL,
    TransportMode           VARCHAR(30)      NOT NULL,   -- ROAD, AIR
    PlannedDepartureAt      DATETIME2(3)     NULL,
    PlannedArrivalAt        DATETIME2(3)     NULL,
    ActualDepartureAt       DATETIME2(3)     NULL,
    ActualArrivalAt         DATETIME2(3)     NULL,
    DistanceKm              DECIMAL(12,2)    NULL,
    Status                  VARCHAR(40)      NOT NULL,   -- PLANNED, IN_PROGRESS, COMPLETED, SKIPPED
    CONSTRAINT PK_RouteLegs PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_RouteLegs_RoutePlanVersions FOREIGN KEY (RoutePlanVersionId) REFERENCES RoutePlanVersions (Id),
    CONSTRAINT FK_RouteLegs_OriginLocation FOREIGN KEY (OriginLocationId) REFERENCES Locations (Id),
    CONSTRAINT FK_RouteLegs_DestLocation FOREIGN KEY (DestinationLocationId) REFERENCES Locations (Id),
    CONSTRAINT UQ_RouteLegs_Version_Sequence UNIQUE (RoutePlanVersionId, SequenceNo)
);

CREATE TABLE Checkpoints (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    RouteLegId          UNIQUEIDENTIFIER NOT NULL,
    SequenceNo          INT              NOT NULL,
    LocationId          UNIQUEIDENTIFIER NOT NULL,
    Type                VARCHAR(40)      NOT NULL,   -- REST_STOP, FUEL_STOP, QUARANTINE_STATION,
                                                      -- BORDER_CROSSING, CUSTOMS, HANDOVER_POINT
    PlannedArrivalAt    DATETIME2(3)     NULL,
    PlannedDepartureAt  DATETIME2(3)     NULL,
    ActualArrivalAt     DATETIME2(3)     NULL,
    ActualDepartureAt   DATETIME2(3)     NULL,
    Status              VARCHAR(40)      NOT NULL,   -- PLANNED, ARRIVED, CLEARED, DEPARTED, SKIPPED
    CONSTRAINT PK_Checkpoints PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Checkpoints_RouteLegs FOREIGN KEY (RouteLegId) REFERENCES RouteLegs (Id),
    CONSTRAINT FK_Checkpoints_Locations FOREIGN KEY (LocationId) REFERENCES Locations (Id),
    CONSTRAINT UQ_Checkpoints_Leg_Sequence UNIQUE (RouteLegId, SequenceNo)
);

CREATE TABLE TripLegAssignments (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    RouteLegId          UNIQUEIDENTIFIER NOT NULL,
    VehicleId           UNIQUEIDENTIFIER NULL,
    FlightBookingId     UNIQUEIDENTIFIER NULL,
    AssignedByUserId    UNIQUEIDENTIFIER NOT NULL,
    AssignedAt          DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_TripLegAssignments PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_TLA_RouteLegs FOREIGN KEY (RouteLegId) REFERENCES RouteLegs (Id),
    CONSTRAINT FK_TLA_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles (Id),
    CONSTRAINT FK_TLA_FlightBookings FOREIGN KEY (FlightBookingId) REFERENCES FlightBookings (Id),
    CONSTRAINT CK_TLA_ModeExclusive CHECK
        ((VehicleId IS NOT NULL AND FlightBookingId IS NULL) OR (VehicleId IS NULL AND FlightBookingId IS NOT NULL))
);
CREATE INDEX IX_TLA_VehicleId ON TripLegAssignments (VehicleId);
-- Business rule (docs/DATABASE.md §5): a Vehicle cannot be double-booked for overlapping intervals.
-- SQL Server has no native range-exclusion constraint; enforce with a SERIALIZABLE transaction
-- that checks overlapping RouteLeg.PlannedDepartureAt/PlannedArrivalAt for the same VehicleId
-- before insert, inside the Planning service's assignment command handler.

CREATE TABLE StaffAssignments (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    TripId          UNIQUEIDENTIFIER NOT NULL,
    RouteLegId      UNIQUEIDENTIFIER NULL,
    UserId          UNIQUEIDENTIFIER NOT NULL,
    Role            VARCHAR(40)      NOT NULL,   -- DRIVER, ESCORT
    AssignedAt      DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    ReleasedAt      DATETIME2(3)     NULL,
    CONSTRAINT PK_StaffAssignments PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_StaffAssignments_Trips FOREIGN KEY (TripId) REFERENCES TransportTrips (Id),
    CONSTRAINT FK_StaffAssignments_RouteLegs FOREIGN KEY (RouteLegId) REFERENCES RouteLegs (Id)
);
CREATE INDEX IX_StaffAssignments_UserId ON StaffAssignments (UserId);
-- Business rule (docs/DATABASE.md §5): Driver/Escort cannot be assigned to conflicting legs
-- -> same overlap-check pattern as TripLegAssignments, keyed on UserId.

CREATE TABLE HorseLegAssignments (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    TripId          UNIQUEIDENTIFIER NOT NULL,
    HorseId         UNIQUEIDENTIFIER NOT NULL,   -- logical ref -> BookingDb.Horses
    RouteLegId      UNIQUEIDENTIFIER NOT NULL,
    StallId         UNIQUEIDENTIFIER NULL,
    BoardedAt       DATETIME2(3)     NULL,
    UnboardedAt     DATETIME2(3)     NULL,
    CONSTRAINT PK_HorseLegAssignments PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_HLA_Trips FOREIGN KEY (TripId) REFERENCES TransportTrips (Id),
    CONSTRAINT FK_HLA_RouteLegs FOREIGN KEY (RouteLegId) REFERENCES RouteLegs (Id),
    CONSTRAINT FK_HLA_Stalls FOREIGN KEY (StallId) REFERENCES Stalls (Id),
    CONSTRAINT UQ_HLA_Leg_Horse UNIQUE (RouteLegId, HorseId)
);
CREATE INDEX IX_HLA_HorseId ON HorseLegAssignments (HorseId);
-- Business rule: Stall capacity must not be exceeded / no overlapping double-booking
-- -> enforce via the same transactional overlap-check pattern, keyed on StallId + time window.

/* ---------------------------------------------------------
   OutboxMessages — producer of Planning.TripCreated, Planning.TripReady,
   Planning.RouteActivated, Planning.RouteChangeApplied
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
    CONSTRAINT PK_OutboxMessages_Planning PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_OutboxMessages_Planning_Status ON OutboxMessages (Status, CreatedAt);

/* AuditLogs — Trip/RoutePlanVersion transitions (approvals, activations, emergency changes) */
CREATE TABLE AuditLogs (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    EntityType      VARCHAR(100)     NOT NULL,      -- 'TransportTrip','RoutePlanVersion'
    EntityId        UNIQUEIDENTIFIER NOT NULL,
    Action          VARCHAR(50)      NOT NULL,
    OldState        VARCHAR(50)      NULL,
    NewState        VARCHAR(50)      NULL,
    ActorUserId     UNIQUEIDENTIFIER NOT NULL,
    Reason          NVARCHAR(2000)   NULL,
    CorrelationId   UNIQUEIDENTIFIER NULL,
    OccurredAt      DATETIME2(3)     NOT NULL,
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AuditLogs_Planning PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_AuditLogs_Planning_Entity ON AuditLogs (EntityType, EntityId);
