USE PlanningDb;
GO
IF COL_LENGTH(N'dbo.RouteLegs', N'RouteShapeJson') IS NULL
    ALTER TABLE dbo.RouteLegs ADD RouteShapeJson NVARCHAR(MAX) NULL;
GO
