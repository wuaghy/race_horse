USE [HandoverDb];
GO

IF COL_LENGTH(N'dbo.HandoverOrderCustomerLinks', N'ReceiverName') IS NULL
    ALTER TABLE dbo.HandoverOrderCustomerLinks ADD ReceiverName NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.HandoverOrderCustomerLinks', N'ReceiverPhone') IS NULL
    ALTER TABLE dbo.HandoverOrderCustomerLinks ADD ReceiverPhone VARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.HandoverOrderCustomerLinks', N'ReceiverEmail') IS NULL
    ALTER TABLE dbo.HandoverOrderCustomerLinks ADD ReceiverEmail VARCHAR(255) NULL;

IF COL_LENGTH(N'dbo.HandoverTripCustomerLinks', N'ReceiverName') IS NULL
    ALTER TABLE dbo.HandoverTripCustomerLinks ADD ReceiverName NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.HandoverTripCustomerLinks', N'ReceiverPhone') IS NULL
    ALTER TABLE dbo.HandoverTripCustomerLinks ADD ReceiverPhone VARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.HandoverTripCustomerLinks', N'ReceiverEmail') IS NULL
    ALTER TABLE dbo.HandoverTripCustomerLinks ADD ReceiverEmail VARCHAR(255) NULL;
GO
