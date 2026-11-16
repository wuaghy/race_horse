USE [BookingDb];
GO

/* Pricing rules are owned by Booking because they determine the customer quote
   created together with a transport order. Rules are intentionally broad and
   can be narrowed by mode, priority, or exact locations. */
IF OBJECT_ID(N'dbo.TransportPricingRules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TransportPricingRules (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Name                NVARCHAR(200)    NOT NULL,
        TransportMode       VARCHAR(30)      NULL,
        DeliveryPriority    VARCHAR(30)      NULL,
        OriginLocationId    UNIQUEIDENTIFIER NULL,
        DestinationLocationId UNIQUEIDENTIFIER NULL,
        BaseAmount          DECIMAL(18,2)    NOT NULL,
        PerHorseAmount      DECIMAL(18,2)    NOT NULL DEFAULT 0,
        CurrencyCode        VARCHAR(10)      NOT NULL DEFAULT 'VND',
        IsActive            BIT              NOT NULL DEFAULT 1,
        CreatedByUserId     UNIQUEIDENTIFIER NOT NULL,
        CreatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TransportPricingRules PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_TransportPricingRules_Amounts CHECK (BaseAmount >= 0 AND PerHorseAmount >= 0),
        CONSTRAINT CK_TransportPricingRules_Currency CHECK (LEN(CurrencyCode) BETWEEN 3 AND 10)
    );
    CREATE INDEX IX_TransportPricingRules_Match ON dbo.TransportPricingRules
        (IsActive, TransportMode, DeliveryPriority, OriginLocationId, DestinationLocationId);
END;
GO

IF OBJECT_ID(N'dbo.TransportPaymentInvoices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TransportPaymentInvoices (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        OrderId             UNIQUEIDENTIFIER NOT NULL,
        InvoiceNo           VARCHAR(80)      NOT NULL,
        Amount              DECIMAL(18,2)    NOT NULL,
        CurrencyCode        VARCHAR(10)      NOT NULL,
        Status              VARCHAR(30)      NOT NULL DEFAULT 'PENDING_PAYMENT',
        ProviderTransactionId VARCHAR(150)   NULL,
        PayOsOrderCode      BIGINT           NULL,
        PaidAt              DATETIME2(3)     NULL,
        FailureReason       NVARCHAR(500)    NULL,
        CreatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2(3)     NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TransportPaymentInvoices PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_TransportPaymentInvoices_OrderId UNIQUE (OrderId),
        CONSTRAINT UQ_TransportPaymentInvoices_InvoiceNo UNIQUE (InvoiceNo),
        CONSTRAINT FK_TransportPaymentInvoices_Order FOREIGN KEY (OrderId) REFERENCES dbo.TransportOrders(Id),
        CONSTRAINT CK_TransportPaymentInvoices_Amount CHECK (Amount > 0),
        CONSTRAINT CK_TransportPaymentInvoices_Status CHECK (Status IN ('PENDING_PAYMENT','PAID','FAILED','EXPIRED','REFUNDED'))
    );
    CREATE INDEX IX_TransportPaymentInvoices_Status ON dbo.TransportPaymentInvoices(Status, UpdatedAt);
END;
GO

IF COL_LENGTH(N'dbo.TransportPaymentInvoices', N'ProviderTransactionId') IS NULL
    ALTER TABLE dbo.TransportPaymentInvoices ADD ProviderTransactionId VARCHAR(150) NULL;
GO

IF COL_LENGTH(N'dbo.TransportPaymentInvoices', N'SePayTransactionId') IS NOT NULL
    EXEC sys.sp_executesql N'UPDATE dbo.TransportPaymentInvoices SET ProviderTransactionId = COALESCE(ProviderTransactionId, SePayTransactionId) WHERE SePayTransactionId IS NOT NULL;';
GO

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_TransportPaymentInvoices_SePayTransactionId' AND parent_object_id = OBJECT_ID(N'dbo.TransportPaymentInvoices'))
    ALTER TABLE dbo.TransportPaymentInvoices DROP CONSTRAINT UQ_TransportPaymentInvoices_SePayTransactionId;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_TransportPaymentInvoices_ProviderTransactionId' AND object_id = OBJECT_ID(N'dbo.TransportPaymentInvoices'))
    CREATE UNIQUE INDEX UX_TransportPaymentInvoices_ProviderTransactionId ON dbo.TransportPaymentInvoices(ProviderTransactionId) WHERE ProviderTransactionId IS NOT NULL;
GO

IF COL_LENGTH(N'dbo.TransportPaymentInvoices', N'PayOsOrderCode') IS NULL
    ALTER TABLE dbo.TransportPaymentInvoices ADD PayOsOrderCode BIGINT NULL;
GO

WHILE EXISTS (SELECT 1 FROM dbo.TransportPaymentInvoices WHERE PayOsOrderCode IS NULL)
BEGIN
    UPDATE TOP (1) dbo.TransportPaymentInvoices
    SET PayOsOrderCode = ABS(CONVERT(BIGINT, CHECKSUM(NEWID()))) + 1
    WHERE PayOsOrderCode IS NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_TransportPaymentInvoices_PayOsOrderCode' AND object_id = OBJECT_ID(N'dbo.TransportPaymentInvoices'))
    CREATE UNIQUE INDEX UX_TransportPaymentInvoices_PayOsOrderCode ON dbo.TransportPaymentInvoices(PayOsOrderCode) WHERE PayOsOrderCode IS NOT NULL;
GO
