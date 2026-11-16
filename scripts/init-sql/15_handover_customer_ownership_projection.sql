USE [HandoverDb];
GO

IF COL_LENGTH(N'dbo.HandoverRecords', N'CustomerUserId') IS NULL
    ALTER TABLE dbo.HandoverRecords ADD CustomerUserId UNIQUEIDENTIFIER NULL;
GO

IF OBJECT_ID(N'dbo.HandoverOrderCustomerLinks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HandoverOrderCustomerLinks (
        OrderId UNIQUEIDENTIFIER NOT NULL,
        CustomerUserId UNIQUEIDENTIFIER NOT NULL,
        UpdatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_HandoverOrderCustomerLinks_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_HandoverOrderCustomerLinks PRIMARY KEY CLUSTERED (OrderId)
    );
END;
GO

IF OBJECT_ID(N'dbo.HandoverTripCustomerLinks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HandoverTripCustomerLinks (
        TripId UNIQUEIDENTIFIER NOT NULL,
        OrderId UNIQUEIDENTIFIER NOT NULL,
        CustomerUserId UNIQUEIDENTIFIER NULL,
        UpdatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_HandoverTripCustomerLinks_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_HandoverTripCustomerLinks PRIMARY KEY CLUSTERED (TripId)
    );
    CREATE INDEX IX_HandoverTripCustomerLinks_OrderId ON dbo.HandoverTripCustomerLinks (OrderId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.HandoverRecords') AND name = N'IX_HandoverRecords_CustomerUserId')
    CREATE INDEX IX_HandoverRecords_CustomerUserId ON dbo.HandoverRecords (CustomerUserId);
GO
