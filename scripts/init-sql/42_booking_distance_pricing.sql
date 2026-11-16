USE [BookingDb];
GO

IF COL_LENGTH(N'dbo.TransportPricingRules', N'PerKmAmount') IS NULL
    ALTER TABLE dbo.TransportPricingRules ADD PerKmAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_TransportPricingRules_PerKmAmount DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_TransportPricingRules_PerKmAmount' AND parent_object_id = OBJECT_ID(N'dbo.TransportPricingRules'))
    ALTER TABLE dbo.TransportPricingRules ADD CONSTRAINT CK_TransportPricingRules_PerKmAmount CHECK (PerKmAmount >= 0);
GO
