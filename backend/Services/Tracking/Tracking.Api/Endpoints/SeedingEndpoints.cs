using Tracking.Application.DTOs;
using Tracking.Application.Services;

namespace Tracking.Api.Endpoints;

public static class SeedingEndpoints
{
    public static void MapSeedingEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        var seed = app.MapGroup("/api/v1/seed")
            .WithTags("Development - Seeding")
            .ExcludeFromDescription();

        seed.MapPost("/demo-trip", SeedDemoTrip)
            .WithName("SeedDemoTrip")
            .WithOpenApi();

        seed.MapPost("/gps-data/{tripId:guid}", SeedGpsData)
            .WithName("SeedGpsData")
            .WithOpenApi();

        seed.MapPost("/health-data/{tripId:guid}", SeedHealthData)
            .WithName("SeedHealthData")
            .WithOpenApi();
    }

    private static async Task<IResult> SeedDemoTrip(
        ITripExecutionService tripService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var tripId = Guid.NewGuid();
        var tripNo = $"DEMO-{DateTime.Now:yyyyMMddHHmmss}";

        var trip = await tripService.InitiateTripExecutionAsync(
            new InitiateTripExecutionInput(tripId, tripNo),
            cancellationToken);

        // Record departure
        var departure = await tripService.RecordTripDepartureAsync(
            tripId,
            37.7749m, // San Francisco latitude
            -122.4194m, // San Francisco longitude
            cancellationToken);

        // Create checkpoints
        var checkpoint1 = await tripService.RecordCheckpointEventAsync(
            tripId,
            new RecordCheckpointEventInput(
                LocationId: Guid.NewGuid(),
                CheckpointType: "CHECKPOINT",
                Status: "ARRIVED",
                OccurredAt: DateTime.UtcNow.AddHours(2),
                Latitude: 37.3382m,
                Longitude: -121.8863m,
                Notes: "Rest stop - Los Gatos"),
            cancellationToken);

        var checkpoint2 = await tripService.RecordCheckpointEventAsync(
            tripId,
            new RecordCheckpointEventInput(
                LocationId: Guid.NewGuid(),
                CheckpointType: "CUSTOMS_CLEARING",
                Status: "ARRIVED",
                OccurredAt: DateTime.UtcNow.AddHours(4),
                Latitude: 36.6002m,
                Longitude: -121.8863m,
                Notes: "Customs checkpoint - Salinas"),
            cancellationToken);

        return Results.Ok(new
        {
            TripId = tripId,
            TripNo = tripNo,
            Trip = trip,
            Departure = departure,
            Checkpoints = new[] { checkpoint1, checkpoint2 },
            Message = "Demo trip seeded successfully"
        });
    }

    private static async Task<IResult> SeedGpsData(
        Guid tripId,
        IGpsTrackingService gpsService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var gpsPoints = new List<object>();

        // Generate 10 GPS points simulating a journey
        var startLat = 37.7749m;
        var startLon = -122.4194m;
        var endLat = 34.0522m;
        var endLon = -118.2437m;

        for (int i = 0; i <= 10; i++)
        {
            var progress = i / 10.0;
            var lat = startLat + (decimal)(progress * (double)(endLat - startLat));
            var lon = startLon + (decimal)(progress * (double)(endLon - startLon));
            var speed = 80 + (decimal)(Math.Sin(i) * 20); // Vary speed 60-100 kph

            var point = await gpsService.IngestGpsPointAsync(
                tripId,
                new IngestGpsPointInput(
                    Latitude: lat,
                    Longitude: lon,
                    Altitude: 100 + (decimal)i,
                    Accuracy: 10,
                    Speed: speed,
                    Heading: 180 + (decimal)i,
                    RecordedAt: DateTime.UtcNow.AddMinutes(i * 30)),
                cancellationToken);

            gpsPoints.Add(point);
        }

        // Calculate ETA
        var eta = await gpsService.CalculateEtaAsync(
            tripId,
            new EtaCalculationInput(
                DestinationLatitude: endLat,
                DestinationLongitude: endLon,
                PlannedArrivalAtMinutes: 300),
            cancellationToken);

        return Results.Ok(new
        {
            GpsPointsCount = gpsPoints.Count,
            GpsPoints = gpsPoints,
            CurrentEta = eta,
            Message = "GPS data seeded successfully"
        });
    }

    private static async Task<IResult> SeedHealthData(
        Guid tripId,
        IHorseHealthService healthService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var horseId = Guid.NewGuid();
        var healthLogs = new List<object>();
        var alerts = new List<object>();

        // Record health logs
        for (int i = 0; i < 5; i++)
        {
            var log = await healthService.RecordHealthLogAsync(
                tripId,
                new RecordHealthLogInput(
                    HorseId: horseId,
                    RecordedAt: DateTime.UtcNow.AddHours(i),
                    ConditionType: "GOOD",
                    StressLevel: 30 + i * 10,
                    BodyTemperature: 37.5m + (decimal)i * 0.2m,
                    HeartRate: 45 + i * 2,
                    RespiratoryRate: 15,
                    Observation: $"Health check #{i + 1} - All vitals normal",
                    PhotoUrl: $"https://example.com/horse/{horseId}/photo{i}.jpg"),
                cancellationToken);

            healthLogs.Add(log);
        }

        // Create some alerts
        var alert1 = await healthService.CreateHealthAlertAsync(
            tripId,
            new CreateHealthAlertInput(
                HorseId: horseId,
                AlertType: "STRESS_HIGH",
                Severity: "MEDIUM",
                Message: "Stress level elevated during transport",
                MetricValue: 75,
                MetricThreshold: 70,
                Recommendation: "Consider rest break"),
            cancellationToken);

        alerts.Add(alert1);

        var alert2 = await healthService.CreateHealthAlertAsync(
            tripId,
            new CreateHealthAlertInput(
                HorseId: horseId,
                AlertType: "TEMPERATURE_ABNORMAL",
                Severity: "HIGH",
                Message: "Body temperature above normal range",
                MetricValue: 39,
                MetricThreshold: 38,
                Recommendation: "Veterinary check required"),
            cancellationToken);

        alerts.Add(alert2);

        // Acknowledge first alert
        var acknowledgedAlert = await healthService.AcknowledgeAlertAsync(
            tripId,
            alert1.Id,
            new AcknowledgeAlertInput(AcknowledgedByUserId: "USER123"),
            cancellationToken);

        var status = await healthService.GetHealthStatusAsync(tripId, horseId, cancellationToken);

        return Results.Ok(new
        {
            HorseId = horseId,
            HealthLogsCount = healthLogs.Count,
            HealthLogs = healthLogs,
            AlertsCount = alerts.Count,
            Alerts = alerts,
            AcknowledgedAlert = acknowledgedAlert,
            HealthStatus = status,
            Message = "Health data seeded successfully"
        });
    }
}
