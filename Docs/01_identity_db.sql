/* =========================================================
   IdentityDb — owned by Identity Service
   Owns: Users, Roles, RefreshTokens, AuditLogs
   ========================================================= */

CREATE TABLE Roles (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    Code            VARCHAR(50)      NOT NULL,      -- CUSTOMER, LOGISTICS_MANAGER, TRANSPORT_SPECIALIST,
                                                     -- FLEET_COORDINATOR, DRIVER_ESCORT, ADMIN
    Name            NVARCHAR(100)    NOT NULL,
    Description     NVARCHAR(500)    NULL,
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Roles_Code UNIQUE (Code)
);

CREATE TABLE Users (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    RoleId          UNIQUEIDENTIFIER NOT NULL,
    ExternalAuthId  VARCHAR(200)     NULL,          -- for future SSO/eKYC integration
    Email           VARCHAR(255)     NOT NULL,
    FullName        NVARCHAR(200)    NOT NULL,
    Phone           VARCHAR(50)      NULL,
    PasswordHash    VARCHAR(500)     NULL,          -- null when auth is fully external
    Status          VARCHAR(30)      NOT NULL,      -- ACTIVE, SUSPENDED, DISABLED
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    VersionNo       INT              NOT NULL DEFAULT 1,   -- optimistic concurrency (multi-admin edits)
    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES Roles (Id),
    CONSTRAINT CK_Users_Status CHECK (Status IN ('ACTIVE','SUSPENDED','DISABLED'))
);
CREATE INDEX IX_Users_RoleId ON Users (RoleId);
CREATE INDEX IX_Users_Status ON Users (Status);

CREATE TABLE RefreshTokens (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    UserId          UNIQUEIDENTIFIER NOT NULL,
    TokenHash       VARCHAR(500)     NOT NULL,      -- never store raw token
    DeviceInfo      NVARCHAR(300)    NULL,
    ExpiresAt       DATETIME2(3)     NOT NULL,
    RevokedAt       DATETIME2(3)     NULL,
    ReplacedByTokenHash VARCHAR(500) NULL,           -- rotation chain
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_RefreshTokens PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES Users (Id)
);
CREATE INDEX IX_RefreshTokens_UserId_ExpiresAt ON RefreshTokens (UserId, ExpiresAt);
CREATE UNIQUE INDEX UX_RefreshTokens_TokenHash ON RefreshTokens (TokenHash);

/* ---------------------------------------------------------
   AuditLogs — required by docs/WORKFLOW_STATE.md §10
   Tracks account/role changes performed by Admin.
   --------------------------------------------------------- */
CREATE TABLE AuditLogs (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    EntityType      VARCHAR(100)     NOT NULL,      -- 'User','Role'
    EntityId        UNIQUEIDENTIFIER NOT NULL,
    Action          VARCHAR(50)      NOT NULL,      -- CREATED, STATUS_CHANGED, ROLE_CHANGED, DELETED
    OldState        VARCHAR(50)      NULL,
    NewState        VARCHAR(50)      NULL,
    ActorUserId     UNIQUEIDENTIFIER NOT NULL,
    Reason          NVARCHAR(2000)   NULL,
    CorrelationId   UNIQUEIDENTIFIER NULL,
    OccurredAt      DATETIME2(3)     NOT NULL,
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_AuditLogs_Entity ON AuditLogs (EntityType, EntityId);
CREATE INDEX IX_AuditLogs_OccurredAt ON AuditLogs (OccurredAt);

/* Seed: master roles (stable reference data — allowed by docs/DATABASE.md §8) */
INSERT INTO Roles (Id, Code, Name) VALUES
 (NEWID(), 'CUSTOMER', N'Customer'),
 (NEWID(), 'LOGISTICS_MANAGER', N'Logistics Manager'),
 (NEWID(), 'TRANSPORT_SPECIALIST', N'Transport Specialist'),
 (NEWID(), 'FLEET_ROUTE_COORDINATOR', N'Fleet & Route Coordinator'),
 (NEWID(), 'DRIVER_ESCORT', N'Driver / Escort'),
 (NEWID(), 'ADMIN', N'Admin');
