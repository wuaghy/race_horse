USE PlanningDb;
GO

IF OBJECT_ID('dbo.StaffOperatingAreas', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StaffOperatingAreas
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StaffOperatingAreas PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        LocationId UNIQUEIDENTIFIER NOT NULL,
        RadiusKm DECIMAL(10, 2) NOT NULL,
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_StaffOperatingAreas_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_StaffOperatingAreas_Locations FOREIGN KEY (LocationId) REFERENCES dbo.Locations(Id),
        CONSTRAINT CK_StaffOperatingAreas_RadiusKm CHECK (RadiusKm > 0 AND RadiusKm <= 1000)
    );

    CREATE UNIQUE INDEX UX_StaffOperatingAreas_User_Location ON dbo.StaffOperatingAreas(UserId, LocationId);
    CREATE INDEX IX_StaffOperatingAreas_LocationId ON dbo.StaffOperatingAreas(LocationId);
END;
GO
