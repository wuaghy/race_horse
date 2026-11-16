USE IncidentDb;
GO
IF COL_LENGTH('dbo.Incidents', 'VeterinarianName') IS NULL ALTER TABLE dbo.Incidents ADD VeterinarianName NVARCHAR(200) NULL;
IF COL_LENGTH('dbo.Incidents', 'VeterinarianContact') IS NULL ALTER TABLE dbo.Incidents ADD VeterinarianContact NVARCHAR(200) NULL;
IF COL_LENGTH('dbo.Incidents', 'TransportDecision') IS NULL ALTER TABLE dbo.Incidents ADD TransportDecision VARCHAR(30) NULL;
GO
