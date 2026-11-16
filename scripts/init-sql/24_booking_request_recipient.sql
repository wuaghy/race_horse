/* Select the person who will receive the horses separately from the account owner. */
USE [BookingDb];
GO

IF COL_LENGTH('dbo.TransportRequests', 'RecipientIsRequester') IS NULL
BEGIN
    ALTER TABLE dbo.TransportRequests
        ADD RecipientIsRequester BIT NOT NULL
            CONSTRAINT DF_TransportRequests_RecipientIsRequester DEFAULT (1) WITH VALUES;
END;

IF COL_LENGTH('dbo.TransportRequests', 'RecipientName') IS NULL
    ALTER TABLE dbo.TransportRequests ADD RecipientName NVARCHAR(200) NULL;

IF COL_LENGTH('dbo.TransportRequests', 'RecipientPhone') IS NULL
    ALTER TABLE dbo.TransportRequests ADD RecipientPhone VARCHAR(50) NULL;

IF COL_LENGTH('dbo.TransportRequests', 'RecipientEmail') IS NULL
    ALTER TABLE dbo.TransportRequests ADD RecipientEmail VARCHAR(255) NULL;
GO
