USE [BookingDb];
GO

IF COL_LENGTH(N'dbo.TransportRequests', N'DeliveryPriority') IS NULL
    ALTER TABLE dbo.TransportRequests ADD DeliveryPriority VARCHAR(20) NOT NULL
        CONSTRAINT DF_TransportRequests_DeliveryPriority DEFAULT 'STANDARD';
GO
