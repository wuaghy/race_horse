/* =========================================================
   Racehorse Transport System — Database Initialization
   Creates all 8 logical databases if they do not exist
   ========================================================= */

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'IdentityDb')
BEGIN
    CREATE DATABASE [IdentityDb];
END
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'BookingDb')
BEGIN
    CREATE DATABASE [BookingDb];
END
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'ComplianceDb')
BEGIN
    CREATE DATABASE [ComplianceDb];
END
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'PlanningDb')
BEGIN
    CREATE DATABASE [PlanningDb];
END
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'TrackingDb')
BEGIN
    CREATE DATABASE [TrackingDb];
END
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'IncidentDb')
BEGIN
    CREATE DATABASE [IncidentDb];
END
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'HandoverDb')
BEGIN
    CREATE DATABASE [HandoverDb];
END
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'NotificationDb')
BEGIN
    CREATE DATABASE [NotificationDb];
END
GO
