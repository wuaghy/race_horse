/* =========================================================
   ComplianceDb — owned by Compliance Service
   Owns: DocumentTypes, Documents, DocumentReviews, RegulationRules,
         RegulationDocumentRequirements, ClearanceCases,
         ClearanceCaseDocuments, OutboxMessages, AuditLogs
   HorseId / RequestId / TripId / RouteLegId / CountryId / BorderPointId
   are logical UUID references only (no cross-service FK).
   ========================================================= */

CREATE TABLE DocumentTypes (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    Code                VARCHAR(50)      NOT NULL,      -- VACCINATION_CERT, HORSE_PASSPORT, EXPORT_PERMIT ...
    Name                NVARCHAR(200)    NOT NULL,
    Scope               VARCHAR(30)      NOT NULL,      -- HORSE, REQUEST, TRIP
    Description         NVARCHAR(1000)   NULL,
    DefaultValidityDays INT              NULL,
    Active              BIT              NOT NULL DEFAULT 1,
    CONSTRAINT PK_DocumentTypes PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_DocumentTypes_Code UNIQUE (Code),
    CONSTRAINT CK_DocumentTypes_Scope CHECK (Scope IN ('HORSE','REQUEST','TRIP'))
);

CREATE TABLE Documents (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    DocumentNo          VARCHAR(50)      NOT NULL,
    DocumentTypeId      UNIQUEIDENTIFIER NOT NULL,
    HorseId             UNIQUEIDENTIFIER NULL,          -- logical ref -> BookingDb.Horses
    RequestId           UNIQUEIDENTIFIER NULL,          -- logical ref -> BookingDb.TransportRequests
    TripId              UNIQUEIDENTIFIER NULL,          -- logical ref -> PlanningDb.TransportTrips
    UploadedByUserId    UNIQUEIDENTIFIER NOT NULL,
    FileName            NVARCHAR(255)    NOT NULL,
    FileUrl             VARCHAR(1000)    NOT NULL,      -- object-storage key, never a client-supplied path
    FileSize            BIGINT           NULL,
    MimeType            VARCHAR(100)     NULL,
    IssueDate           DATE             NULL,
    ExpiryDate          DATE             NULL,
    IssuingAuthority    NVARCHAR(255)    NULL,
    Status              VARCHAR(40)      NOT NULL,      -- UPLOADED, UNDER_REVIEW, APPROVED, REJECTED,
                                                         -- NEED_ADDITIONAL_INFO  (EXPIRED is derived, not stored)
    CurrentReviewNote   NVARCHAR(2000)   NULL,
    UploadedAt          DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    VersionNo           INT              NOT NULL DEFAULT 1,
    CONSTRAINT PK_Documents PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Documents_No UNIQUE (DocumentNo),
    CONSTRAINT FK_Documents_DocumentTypes FOREIGN KEY (DocumentTypeId) REFERENCES DocumentTypes (Id),
    CONSTRAINT CK_Documents_Status CHECK (Status IN
        ('UPLOADED','UNDER_REVIEW','APPROVED','REJECTED','NEED_ADDITIONAL_INFO')),
    CONSTRAINT CK_Documents_HasOwner CHECK (HorseId IS NOT NULL OR RequestId IS NOT NULL OR TripId IS NOT NULL)
);
CREATE INDEX IX_Documents_HorseId ON Documents (HorseId);
CREATE INDEX IX_Documents_RequestId ON Documents (RequestId);
CREATE INDEX IX_Documents_TripId ON Documents (TripId);
CREATE INDEX IX_Documents_Status ON Documents (Status);
CREATE INDEX IX_Documents_ExpiryDate ON Documents (ExpiryDate) WHERE ExpiryDate IS NOT NULL;

CREATE TABLE DocumentReviews (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    DocumentId      UNIQUEIDENTIFIER NOT NULL,
    ReviewerUserId  UNIQUEIDENTIFIER NOT NULL,
    Status          VARCHAR(40)      NOT NULL,      -- APPROVED, REJECTED, NEED_ADDITIONAL_INFO
    Comment         NVARCHAR(2000)   NULL,
    ReviewedAt      DATETIME2(3)     NOT NULL,
    CONSTRAINT PK_DocumentReviews PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_DocumentReviews_Documents FOREIGN KEY (DocumentId) REFERENCES Documents (Id)
);
-- History must be preserved (docs/DATABASE.md §5) -> append-only, no UPDATE/DELETE from the app layer.
CREATE INDEX IX_DocumentReviews_DocumentId ON DocumentReviews (DocumentId);

CREATE TABLE RegulationRules (
    Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    RuleCode                VARCHAR(50)      NOT NULL,
    OriginCountryId         UNIQUEIDENTIFIER NOT NULL,   -- logical ref -> PlanningDb.Countries
    DestinationCountryId    UNIQUEIDENTIFIER NOT NULL,   -- logical ref -> PlanningDb.Countries
    BorderPointId           UNIQUEIDENTIFIER NULL,       -- logical ref -> PlanningDb.Locations
    Title                   NVARCHAR(255)    NOT NULL,
    Description             NVARCHAR(2000)   NULL,
    QuarantineRequired      BIT              NOT NULL,
    CustomsRequired         BIT              NOT NULL,
    InspectionRequired      BIT              NOT NULL,
    EffectiveFrom           DATE             NOT NULL,
    EffectiveTo             DATE             NULL,
    Active                  BIT              NOT NULL DEFAULT 1,
    CONSTRAINT PK_RegulationRules PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_RegulationRules_Code UNIQUE (RuleCode),
    CONSTRAINT CK_RegulationRules_EffectiveRange CHECK (EffectiveTo IS NULL OR EffectiveTo > EffectiveFrom)
);
CREATE INDEX IX_RegulationRules_Countries ON RegulationRules (OriginCountryId, DestinationCountryId);

CREATE TABLE RegulationDocumentRequirements (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    RegulationRuleId    UNIQUEIDENTIFIER NOT NULL,
    DocumentTypeId      UNIQUEIDENTIFIER NOT NULL,
    Required            BIT              NOT NULL,
    MinValidityDays     INT              NULL,
    AuthorityName       NVARCHAR(255)    NULL,
    Notes               NVARCHAR(1000)   NULL,
    CONSTRAINT PK_RegulationDocumentRequirements PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_RDR_RegulationRules FOREIGN KEY (RegulationRuleId) REFERENCES RegulationRules (Id),
    CONSTRAINT FK_RDR_DocumentTypes FOREIGN KEY (DocumentTypeId) REFERENCES DocumentTypes (Id),
    CONSTRAINT UQ_RDR_Rule_DocType UNIQUE (RegulationRuleId, DocumentTypeId)
);

CREATE TABLE ClearanceCases (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    CaseNo              VARCHAR(50)      NOT NULL,
    TripId              UNIQUEIDENTIFIER NOT NULL,       -- logical ref -> PlanningDb.TransportTrips
    RouteLegId          UNIQUEIDENTIFIER NULL,           -- logical ref -> PlanningDb.RouteLegs
    CountryId           UNIQUEIDENTIFIER NOT NULL,       -- logical ref -> PlanningDb.Countries
    BorderPointId       UNIQUEIDENTIFIER NULL,           -- logical ref -> PlanningDb.Locations
    AuthorityName       NVARCHAR(255)    NOT NULL,
    ReferenceNumber     VARCHAR(100)     NULL,
    Status              VARCHAR(40)      NOT NULL,       -- DRAFT, SUBMITTED, UNDER_REVIEW,
                                                          -- NEED_ADDITIONAL_INFO, APPROVED, REJECTED, COMPLETED
    SubmittedAt         DATETIME2(3)     NULL,
    ReviewedAt          DATETIME2(3)     NULL,
    CompletedAt         DATETIME2(3)     NULL,
    RejectionReason     NVARCHAR(2000)   NULL,
    Notes               NVARCHAR(2000)   NULL,
    CreatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    VersionNo           INT              NOT NULL DEFAULT 1,   -- ADDED: multi-actor updates (specialist + authority review)
    CONSTRAINT PK_ClearanceCases PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_ClearanceCases_No UNIQUE (CaseNo),
    CONSTRAINT CK_ClearanceCases_Status CHECK (Status IN
        ('DRAFT','SUBMITTED','UNDER_REVIEW','NEED_ADDITIONAL_INFO','APPROVED','REJECTED','COMPLETED'))
);
CREATE INDEX IX_ClearanceCases_TripId ON ClearanceCases (TripId);
CREATE INDEX IX_ClearanceCases_Status ON ClearanceCases (Status);

CREATE TABLE ClearanceCaseDocuments (
    Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    ClearanceCaseId     UNIQUEIDENTIFIER NOT NULL,
    DocumentId          UNIQUEIDENTIFIER NOT NULL,
    Required            BIT              NOT NULL,
    SubmittedAt         DATETIME2(3)     NULL,
    Status              VARCHAR(40)      NOT NULL,
    CONSTRAINT PK_ClearanceCaseDocuments PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_CCD_ClearanceCases FOREIGN KEY (ClearanceCaseId) REFERENCES ClearanceCases (Id),
    CONSTRAINT FK_CCD_Documents FOREIGN KEY (DocumentId) REFERENCES Documents (Id),
    CONSTRAINT UQ_CCD_Case_Document UNIQUE (ClearanceCaseId, DocumentId)
);

/* ---------------------------------------------------------
   OutboxMessages — producer of Compliance.DocumentApproved, Compliance.ComplianceReady
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
    CONSTRAINT PK_OutboxMessages_Compliance PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_OutboxMessages_Compliance_Status ON OutboxMessages (Status, CreatedAt);

/* AuditLogs — Document review & Clearance transitions */
CREATE TABLE AuditLogs (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    EntityType      VARCHAR(100)     NOT NULL,      -- 'Document','ClearanceCase'
    EntityId        UNIQUEIDENTIFIER NOT NULL,
    Action          VARCHAR(50)      NOT NULL,
    OldState        VARCHAR(50)      NULL,
    NewState        VARCHAR(50)      NULL,
    ActorUserId     UNIQUEIDENTIFIER NOT NULL,
    Reason          NVARCHAR(2000)   NULL,
    CorrelationId   UNIQUEIDENTIFIER NULL,
    OccurredAt      DATETIME2(3)     NOT NULL,
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AuditLogs_Compliance PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_AuditLogs_Compliance_Entity ON AuditLogs (EntityType, EntityId);
