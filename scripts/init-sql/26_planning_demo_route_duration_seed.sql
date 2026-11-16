USE [PlanningDb];
GO

-- Local demo estimates only. These are planning placeholders, not live
-- carrier/airline schedules or operational commitments.
;WITH DemoLocationIds AS
(
    SELECT CONVERT(UNIQUEIDENTIFIER, '66666666-6666-4666-8666-666666666661') AS LocationId
    UNION ALL SELECT CONVERT(UNIQUEIDENTIFIER, '66666666-6666-4666-8666-666666666662')
    UNION ALL SELECT CONVERT(UNIQUEIDENTIFIER, '66666666-6666-4666-8666-666666666663')
    UNION ALL SELECT CONVERT(UNIQUEIDENTIFIER, '66666666-6666-4666-8666-666666666664')
), DemoRouteModes AS
(
    SELECT DISTINCT capability.Id AS RouteTransportCapabilityId, UPPER(mode.value) AS Mode
    FROM dbo.RouteTransportCapabilities AS capability
    INNER JOIN DemoLocationIds AS origin ON origin.LocationId = capability.OriginLocationId
    INNER JOIN DemoLocationIds AS destination ON destination.LocationId = capability.DestinationLocationId
    CROSS APPLY OPENJSON(capability.AllowedModesJson) AS mode
    WHERE capability.OriginLocationId <> capability.DestinationLocationId
)
INSERT INTO dbo.RouteTransportCapabilityModes
    (RouteTransportCapabilityId, Mode, MinDurationMinutes, MaxDurationMinutes)
SELECT routeMode.RouteTransportCapabilityId,
       routeMode.Mode,
       CASE routeMode.Mode WHEN 'ROAD' THEN 720 WHEN 'AIR' THEN 240 ELSE 960 END,
       CASE routeMode.Mode WHEN 'ROAD' THEN 2880 WHEN 'AIR' THEN 720 ELSE 3600 END
FROM DemoRouteModes AS routeMode
WHERE routeMode.Mode IN ('ROAD', 'AIR', 'MIXED')
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.RouteTransportCapabilityModes AS existing
      WHERE existing.RouteTransportCapabilityId = routeMode.RouteTransportCapabilityId
        AND existing.Mode = routeMode.Mode
  );
GO
