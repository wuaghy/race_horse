USE [HandoverDb];
GO

IF OBJECT_ID(N'dbo.HandoverRecords', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.HandoverRecords', N'TripArrivedAt') IS NULL
BEGIN
    ALTER TABLE dbo.HandoverRecords ADD TripArrivedAt DATETIME2(3) NULL;
END;
GO

IF OBJECT_ID(N'TrackingDb.dbo.TripExecutions', N'U') IS NOT NULL
BEGIN
    UPDATE handover
       SET TripArrivedAt = execution.ArrivalAt
      FROM HandoverDb.dbo.HandoverRecords AS handover
      JOIN TrackingDb.dbo.TripExecutions AS execution ON execution.TripId = handover.TripId
     WHERE handover.TripArrivedAt IS NULL
       AND execution.ArrivalAt IS NOT NULL;
END;
GO
