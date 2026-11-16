USE [BookingDb];
GO

IF OBJECT_ID(N'dbo.TransportRequestIdempotencyKeys', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TransportRequestIdempotencyKeys (
        IdempotencyKey UNIQUEIDENTIFIER NOT NULL,
        CustomerUserId UNIQUEIDENTIFIER NOT NULL, -- logical ref -> IdentityDb.Users
        RequestId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_TransportRequestIdempotencyKeys_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TransportRequestIdempotencyKeys PRIMARY KEY CLUSTERED (IdempotencyKey),
        CONSTRAINT FK_TransportRequestIdempotencyKeys_Request FOREIGN KEY (RequestId) REFERENCES dbo.TransportRequests (Id)
    );
    CREATE INDEX IX_TransportRequestIdempotencyKeys_RequestId ON dbo.TransportRequestIdempotencyKeys (RequestId);
END;
GO
