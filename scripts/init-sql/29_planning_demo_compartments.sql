USE [PlanningDb];
GO

/* Synthetic compartment inventory for local Logistics planning demos. */
;WITH DemoCompartments(StallCode, StallType, Capacity, Notes) AS
(
    SELECT * FROM (VALUES
        ('DEMO-CMP-RS-001', N'Road · Single horse ventilated', 1, N'Synthetic demo compartment; individual horse space.'),
        ('DEMO-CMP-RS-002', N'Road · Single horse ventilated', 1, N'Synthetic demo compartment; individual horse space.'),
        ('DEMO-CMP-RS-003', N'Road · Single horse climate controlled', 1, N'Synthetic demo compartment; temperature controlled.'),
        ('DEMO-CMP-RD-001', N'Road · Divider pair', 2, N'Synthetic demo compartment; divider installed for two compatible horses.'),
        ('DEMO-CMP-RD-002', N'Road · Divider pair', 2, N'Synthetic demo compartment; divider installed for two compatible horses.'),
        ('DEMO-CMP-RD-003', N'Road · Shared pair', 2, N'Synthetic demo compartment; shared space for compatible horses.'),
        ('DEMO-CMP-RQ-001', N'Road · Four-horse group', 4, N'Synthetic demo compartment; group space, subject to compatibility checks.'),
        ('DEMO-CMP-RQ-002', N'Road · Four-horse group', 4, N'Synthetic demo compartment; group space, subject to compatibility checks.'),
        ('DEMO-CMP-AS-001', N'Air · Individual crate', 1, N'Synthetic demo air-freight compartment.'),
        ('DEMO-CMP-AS-002', N'Air · Individual crate', 1, N'Synthetic demo air-freight compartment.'),
        ('DEMO-CMP-AD-001', N'Air · Paired crate', 2, N'Synthetic demo air-freight compartment; airline acceptance is manual.'),
        ('DEMO-CMP-AD-002', N'Air · Paired crate', 2, N'Synthetic demo air-freight compartment; airline acceptance is manual.'),
        ('DEMO-CMP-TR-001', N'Transfer · Loading bay', 2, N'Synthetic transfer compartment for short loading and unloading stages.'),
        ('DEMO-CMP-TR-002', N'Transfer · Loading bay', 4, N'Synthetic transfer compartment for multi-horse transfer stages.')
    ) AS source(StallCode, StallType, Capacity, Notes)
)
INSERT INTO dbo.Stalls (StallCode, StallType, Capacity, Active, Notes)
SELECT demo.StallCode, demo.StallType, demo.Capacity, 1, demo.Notes
FROM DemoCompartments AS demo
WHERE NOT EXISTS (SELECT 1 FROM dbo.Stalls AS existing WHERE existing.StallCode = demo.StallCode);
GO
