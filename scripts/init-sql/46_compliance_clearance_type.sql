USE [ComplianceDb];
GO

IF COL_LENGTH(N'dbo.ClearanceCases', N'ClearanceType') IS NULL
    ALTER TABLE dbo.ClearanceCases ADD ClearanceType VARCHAR(30) NOT NULL
        CONSTRAINT DF_ClearanceCases_ClearanceType DEFAULT 'CUSTOMS' WITH VALUES;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.ClearanceCases') AND name = N'CK_ClearanceCases_ClearanceType')
    ALTER TABLE dbo.ClearanceCases WITH CHECK ADD CONSTRAINT CK_ClearanceCases_ClearanceType
        CHECK (ClearanceType IN ('CUSTOMS','QUARANTINE','INSPECTION'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ClearanceCases') AND name = N'IX_ClearanceCases_RouteRequirement')
    CREATE INDEX IX_ClearanceCases_RouteRequirement
        ON dbo.ClearanceCases(TripId, RouteLegId, CountryId, ClearanceType, Status);
GO
