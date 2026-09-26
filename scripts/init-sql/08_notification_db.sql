USE [NotificationDb];
GO

/* =========================================================
   NotificationDb — owned by Notification Service
   Owns: Notifications, DeviceTokens
   Pure consumer service: no OutboxMessages (does not publish
   domain events onward), no AuditLogs (no business state machine).
   ========================================================= */

CREATE TABLE Notifications (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    RecipientUserId UNIQUEIDENTIFIER NOT NULL,   -- logical ref -> IdentityDb.Users
    Type            VARCHAR(50)      NOT NULL,   -- REQUEST_APPROVED, DOCUMENT_NEED_INFO, TRIP_DEPARTED,
                                                  -- HEALTH_ALERT, INCIDENT, ROUTE_CHANGED, HANDOVER_READY
    Title           NVARCHAR(255)    NOT NULL,
    Message         NVARCHAR(2000)   NOT NULL,
    ReferenceType   VARCHAR(100)     NULL,       -- 'TransportRequest','Trip','Incident', ...
    ReferenceId     UNIQUEIDENTIFIER NULL,
    Status          VARCHAR(30)      NOT NULL,   -- PENDING, SENT, READ, FAILED
    ReadAt          DATETIME2(3)     NULL,
    CreatedAt       DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Notifications PRIMARY KEY CLUSTERED (Id)
);
CREATE INDEX IX_Notifications_RecipientUserId_Status ON Notifications (RecipientUserId, Status);
CREATE INDEX IX_Notifications_ReferenceType_ReferenceId ON Notifications (ReferenceType, ReferenceId);

CREATE TABLE DeviceTokens (
    Id          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    UserId      UNIQUEIDENTIFIER NOT NULL,
    Platform    VARCHAR(30)      NOT NULL,   -- IOS, ANDROID
    Token       VARCHAR(1000)    NOT NULL,
    Active      BIT              NOT NULL DEFAULT 1,
    CreatedAt   DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt   DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_DeviceTokens PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_DeviceTokens_Token UNIQUE (Token)
);
CREATE INDEX IX_DeviceTokens_UserId_Active ON DeviceTokens (UserId) WHERE Active = 1;

/* ---------------------------------------------------------
   InboxMessages — idempotent consumer log (docs/ARCHITECTURE.md §8:
   "idempotent event handlers"; docs/INTEGRATION_EVENTS.md §3).
   Notification consumes almost every event in the system, so its
   consumer idempotency table is included here explicitly.
   --------------------------------------------------------- */
CREATE TABLE InboxMessages (
    Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
    EventId         UNIQUEIDENTIFIER NOT NULL,   -- envelope eventId from the producer
    EventType       VARCHAR(150)     NOT NULL,
    ProcessedAt     DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_InboxMessages PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_InboxMessages_EventId UNIQUE (EventId)
);
-- Consumer checks UQ_InboxMessages_EventId before acting on an event; a duplicate delivery
-- (RabbitMQ at-least-once) hits the unique constraint and is discarded as already-processed.
