USE PlanningDb;
GO

IF OBJECT_ID('dbo.StaffRouteCountryAuthorizations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StaffRouteCountryAuthorizations
    (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StaffRouteCountryAuthorizations PRIMARY KEY,
        UserId UNIQUEIDENTIFIER NOT NULL,
        CountryId UNIQUEIDENTIFIER NOT NULL,
        ValidUntil DATE NULL,
        CONSTRAINT FK_StaffRouteCountryAuthorizations_Countries FOREIGN KEY (CountryId) REFERENCES dbo.Countries(Id)
    );
    CREATE UNIQUE INDEX UX_StaffRouteCountryAuthorizations_User_Country
        ON dbo.StaffRouteCountryAuthorizations(UserId, CountryId);
END;
GO
