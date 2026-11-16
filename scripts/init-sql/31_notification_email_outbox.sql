USE [NotificationDb];
GO

IF OBJECT_ID(N'dbo.EmailOutboxMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailOutboxMessages (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_EmailOutboxMessages_Id DEFAULT NEWSEQUENTIALID(),
        EventId         UNIQUEIDENTIFIER NOT NULL,
        ToEmail         VARCHAR(255) NOT NULL,
        Subject         NVARCHAR(255) NOT NULL,
        HtmlBody        NVARCHAR(MAX) NOT NULL,
        TextBody        NVARCHAR(MAX) NOT NULL,
        Status          VARCHAR(20) NOT NULL CONSTRAINT DF_EmailOutboxMessages_Status DEFAULT 'PENDING',
        RetryCount      INT NOT NULL CONSTRAINT DF_EmailOutboxMessages_RetryCount DEFAULT 0,
        LastError       NVARCHAR(2000) NULL,
        NextAttemptAt   DATETIME2(3) NOT NULL CONSTRAINT DF_EmailOutboxMessages_NextAttemptAt DEFAULT SYSUTCDATETIME(),
        CreatedAt       DATETIME2(3) NOT NULL CONSTRAINT DF_EmailOutboxMessages_CreatedAt DEFAULT SYSUTCDATETIME(),
        SentAt          DATETIME2(3) NULL,
        CONSTRAINT PK_EmailOutboxMessages PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_EmailOutboxMessages_EventId UNIQUE (EventId)
    );

    CREATE INDEX IX_EmailOutboxMessages_Status_NextAttemptAt_CreatedAt
        ON dbo.EmailOutboxMessages (Status, NextAttemptAt, CreatedAt);
END;
GO
