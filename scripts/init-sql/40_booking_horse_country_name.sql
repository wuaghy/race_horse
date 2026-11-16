USE BookingDb;
GO
IF COL_LENGTH('dbo.Horses', 'CountryOfOriginName') IS NULL
    ALTER TABLE dbo.Horses ADD CountryOfOriginName NVARCHAR(120) NULL;
GO
