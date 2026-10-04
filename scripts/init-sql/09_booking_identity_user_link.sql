USE [BookingDb];
GO

IF COL_LENGTH(N'dbo.Customers', N'IdentityUserId') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD IdentityUserId UNIQUEIDENTIFIER NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Customers')
      AND name = N'UX_Customers_IdentityUserId'
)
BEGIN
    CREATE UNIQUE INDEX UX_Customers_IdentityUserId
        ON dbo.Customers (IdentityUserId)
        WHERE IdentityUserId IS NOT NULL;
END
GO