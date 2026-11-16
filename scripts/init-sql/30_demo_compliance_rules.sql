USE [ComplianceDb];
GO

-- Seed the complete document-type catalog because this script can run before
-- Compliance.Api starts its EF initializer.
IF NOT EXISTS (SELECT 1 FROM dbo.DocumentTypes WHERE Code = 'VACCINATION_CERT')
    INSERT INTO dbo.DocumentTypes (Id, Code, Name, Scope, Description, DefaultValidityDays, Active)
    VALUES ('11111111-1111-4111-8111-111111111101', 'VACCINATION_CERT', N'Vaccination Certificate (Equine Influenza / EHV)', 'HORSE', N'Mandatory equine vaccination record covering influenza and herpesvirus.', 180, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.DocumentTypes WHERE Code = 'HORSE_PASSPORT')
    INSERT INTO dbo.DocumentTypes (Id, Code, Name, Scope, Description, DefaultValidityDays, Active)
    VALUES ('11111111-1111-4111-8111-111111111102', 'HORSE_PASSPORT', N'FEI Horse Passport & Registration', 'HORSE', N'Official FEI passport containing identification, microchip, and pedigree.', 365, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.DocumentTypes WHERE Code = 'HEALTH_CERTIFICATE')
    INSERT INTO dbo.DocumentTypes (Id, Code, Name, Scope, Description, DefaultValidityDays, Active)
    VALUES ('11111111-1111-4111-8111-111111111103', 'HEALTH_CERTIFICATE', N'Veterinary Health Certificate', 'HORSE', N'Pre-export clinical examination certificate issued within 48h of departure.', 30, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.DocumentTypes WHERE Code = 'EXPORT_PERMIT')
    INSERT INTO dbo.DocumentTypes (Id, Code, Name, Scope, Description, DefaultValidityDays, Active)
    VALUES ('11111111-1111-4111-8111-111111111104', 'EXPORT_PERMIT', N'Government Export Quarantine Permit', 'TRIP', N'Official clearance permit from Department of Animal Health / Agriculture.', 30, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.DocumentTypes WHERE Code = 'IMPORT_PERMIT')
    INSERT INTO dbo.DocumentTypes (Id, Code, Name, Scope, Description, DefaultValidityDays, Active)
    VALUES ('11111111-1111-4111-8111-111111111105', 'IMPORT_PERMIT', N'Destination Country Import Permit', 'TRIP', N'Approval issued by importing quarantine authorities.', 60, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.DocumentTypes WHERE Code = 'CUSTOMS_DECLARATION')
    INSERT INTO dbo.DocumentTypes (Id, Code, Name, Scope, Description, DefaultValidityDays, Active)
    VALUES ('11111111-1111-4111-8111-111111111106', 'CUSTOMS_DECLARATION', N'ATA Carnet / Customs Declaration', 'TRIP', N'Temporary cross-border admission carnets and customs documentation.', 90, 1);

DECLARE @RuleCountryId UNIQUEIDENTIFIER;
DECLARE @RuleCode VARCHAR(50);
DECLARE demo_countries CURSOR LOCAL FAST_FORWARD FOR
    SELECT Id FROM PlanningDb.dbo.Countries
    WHERE Active = 1 AND Name LIKE N'Demo Country%';

OPEN demo_countries;
FETCH NEXT FROM demo_countries INTO @RuleCountryId;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @RuleCode = CONCAT('DEMO-DOMESTIC-', CONVERT(VARCHAR(36), @RuleCountryId));

    IF NOT EXISTS (SELECT 1 FROM dbo.RegulationRules WHERE RuleCode = @RuleCode)
    BEGIN
        INSERT INTO dbo.RegulationRules
            (RuleCode, OriginCountryId, DestinationCountryId, Title, Description, QuarantineRequired, CustomsRequired, InspectionRequired, EffectiveFrom, Active)
        VALUES
            (@RuleCode, @RuleCountryId, @RuleCountryId, N'Demo domestic horse transport requirements', N'Synthetic local-demo rule. Requires current horse passport and vaccination records; not a real-world regulatory rule.', 0, 0, 0, '2000-01-01', 1);
    END;

    INSERT INTO dbo.RegulationDocumentRequirements (RegulationRuleId, DocumentTypeId, Required, MinValidityDays, Notes)
    SELECT regulation.Id, docType.Id, 1, 0, N'Synthetic local demo requirement.'
    FROM dbo.RegulationRules AS regulation
    CROSS JOIN dbo.DocumentTypes AS docType
    WHERE regulation.RuleCode = @RuleCode
      AND docType.Code IN ('VACCINATION_CERT', 'HORSE_PASSPORT')
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.RegulationDocumentRequirements AS existing
          WHERE existing.RegulationRuleId = regulation.Id AND existing.DocumentTypeId = docType.Id
      );

    FETCH NEXT FROM demo_countries INTO @RuleCountryId;
END;
CLOSE demo_countries;
DEALLOCATE demo_countries;
GO
