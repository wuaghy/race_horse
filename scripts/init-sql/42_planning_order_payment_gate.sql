USE [PlanningDb];
GO

IF COL_LENGTH(N'dbo.TripOrders', N'PaymentStatus') IS NULL
    ALTER TABLE dbo.TripOrders ADD PaymentStatus VARCHAR(30) NOT NULL CONSTRAINT DF_TripOrders_PaymentStatus DEFAULT 'PAID';
GO

UPDATE dbo.TripOrders SET PaymentStatus = 'PAID' WHERE PaymentStatus IS NULL OR LTRIM(RTRIM(PaymentStatus)) = '';
GO

UPDATE planningOrder
SET PaymentStatus = invoice.Status
FROM dbo.TripOrders planningOrder
INNER JOIN BookingDb.dbo.TransportPaymentInvoices invoice ON invoice.OrderId = planningOrder.OrderId
WHERE invoice.Status <> 'PAID';
GO
