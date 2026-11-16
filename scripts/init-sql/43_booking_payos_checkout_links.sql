USE [BookingDb];
GO

/* PayOS returns a checkout link for the invoice order code. Keep that link
   with the invoice so reopening a request does not try to create a duplicate
   PayOS payment order. */
IF COL_LENGTH(N'dbo.TransportPaymentInvoices', N'CheckoutUrl') IS NULL
    ALTER TABLE dbo.TransportPaymentInvoices ADD CheckoutUrl NVARCHAR(1000) NULL;
GO

IF COL_LENGTH(N'dbo.TransportPaymentInvoices', N'QrCode') IS NULL
    ALTER TABLE dbo.TransportPaymentInvoices ADD QrCode NVARCHAR(MAX) NULL;
GO

IF COL_LENGTH(N'dbo.TransportPaymentInvoices', N'PayOsPaymentLinkId') IS NULL
    ALTER TABLE dbo.TransportPaymentInvoices ADD PayOsPaymentLinkId VARCHAR(150) NULL;
GO

IF COL_LENGTH(N'dbo.TransportPaymentInvoices', N'CheckoutCreatedAt') IS NULL
    ALTER TABLE dbo.TransportPaymentInvoices ADD CheckoutCreatedAt DATETIME2(3) NULL;
GO
