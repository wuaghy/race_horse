USE [BookingDb];
GO

IF COL_LENGTH(N'dbo.TransportPaymentInvoices', N'SettlementMethod') IS NULL
    ALTER TABLE dbo.TransportPaymentInvoices ADD SettlementMethod VARCHAR(30) NULL;
GO
IF COL_LENGTH(N'dbo.TransportPaymentInvoices', N'SettlementReference') IS NULL
    ALTER TABLE dbo.TransportPaymentInvoices ADD SettlementReference NVARCHAR(150) NULL;
GO
IF COL_LENGTH(N'dbo.TransportPaymentInvoices', N'SettlementEvidenceReference') IS NULL
    ALTER TABLE dbo.TransportPaymentInvoices ADD SettlementEvidenceReference NVARCHAR(500) NULL;
GO
IF COL_LENGTH(N'dbo.TransportPaymentInvoices', N'SettledByUserId') IS NULL
    ALTER TABLE dbo.TransportPaymentInvoices ADD SettledByUserId UNIQUEIDENTIFIER NULL;
GO
IF COL_LENGTH(N'dbo.TransportPaymentInvoices', N'SettlementNotes') IS NULL
    ALTER TABLE dbo.TransportPaymentInvoices ADD SettlementNotes NVARCHAR(1000) NULL;
GO
IF COL_LENGTH(N'dbo.TransportPaymentInvoices', N'PaymentMethod') IS NULL
    ALTER TABLE dbo.TransportPaymentInvoices ADD PaymentMethod VARCHAR(30) NULL;
GO
IF COL_LENGTH(N'dbo.TransportPaymentInvoices', N'PaymentReference') IS NULL
    ALTER TABLE dbo.TransportPaymentInvoices ADD PaymentReference NVARCHAR(150) NULL;
GO

IF OBJECT_ID(N'dbo.TransportPaymentAuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TransportPaymentAuditLogs (
        Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        InvoiceId UNIQUEIDENTIFIER NOT NULL,
        OrderId UNIQUEIDENTIFIER NOT NULL,
        Action VARCHAR(80) NOT NULL,
        OldStatus VARCHAR(30) NOT NULL,
        NewStatus VARCHAR(30) NOT NULL,
        SettlementMethod VARCHAR(30) NOT NULL,
        SettlementReference NVARCHAR(150) NOT NULL,
        EvidenceReference NVARCHAR(500) NULL,
        Notes NVARCHAR(1000) NULL,
        ActorUserId UNIQUEIDENTIFIER NOT NULL,
        OccurredAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TransportPaymentAuditLogs PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_TransportPaymentAuditLogs_Invoice FOREIGN KEY (InvoiceId) REFERENCES dbo.TransportPaymentInvoices(Id)
    );
    CREATE INDEX IX_TransportPaymentAuditLogs_OrderId_OccurredAt ON dbo.TransportPaymentAuditLogs(OrderId, OccurredAt DESC);
END;
GO
