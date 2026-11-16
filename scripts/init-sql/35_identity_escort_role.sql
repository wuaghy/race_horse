USE IdentityDb;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Code = 'ESCORT')
    INSERT INTO dbo.Roles (Id, Code, Name) VALUES (NEWID(), 'ESCORT', N'Horse Escort');
GO
