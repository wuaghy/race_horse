using Tracking.Application.DTOs;
using Tracking.Domain;

namespace Tracking.Application.Services;

public interface IGpsTrackingService
{
    Task<GpsPointDto> IngestGpsPointAsync(Guid tripId, IngestGpsPointInput input, CancellationToken cancellationToken);
    Task<IReadOnlyList<GpsPointDto>> GetGpsPointsAsync(Guid tripId, int? limit, CancellationToken cancellationToken);
    Task<GpsPointDto?> GetLatestGpsPointAsync(Guid tripId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LocationHistoryDto>> GetLocationHistoryAsync(Guid tripId, CancellationToken cancellationToken);
    Task<LocationHistoryDto> RecordLocationHistoryAsync(Guid tripId, decimal latitude, decimal longitude, Guid? locationId, string? locationName, CancellationToken cancellationToken);
    Task<EstimatedTimeOfArrivalDto> CalculateEtaAsync(Guid tripId, EtaCalculationInput input, CancellationToken cancellationToken);
    Task<EstimatedTimeOfArrivalDto?> GetCurrentEtaAsync(Guid tripId, CancellationToken cancellationToken);
}

public class GpsTrackingService : IGpsTrackingService
{
    private readonly IGpsPointRepository _gpsPointRepository;
    private readonly ILocationHistoryRepository _locationHistoryRepository;
    private readonly IEtaRepository _etaRepository;
    private readonly ITripExecutionRepository _tripRepository;
    private readonly IOutboxMessageRepository _outboxRepository;

    public GpsTrackingService(
        IGpsPointRepository gpsPointRepository,
        ILocationHistoryRepository locationHistoryRepository,
        IEtaRepository etaRepository,
        ITripExecutionRepository tripRepository,
        IOutboxMessageRepository outboxRepository)
    {
        _gpsPointRepository = gpsPointRepository;
        _locationHistoryRepository = locationHistoryRepository;
        _etaRepository = etaRepository;
        _tripRepository = tripRepository;
        _outboxRepository = outboxRepository;
    }

    public async Task<GpsPointDto> IngestGpsPointAsync(Guid tripId, IngestGpsPointInput input, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var gpsPoint = new GpsPoint
        {
            TripExecutionId = trip.Id,
            Latitude = input.Latitude,
            Longitude = input.Longitude,
            Altitude = input.Altitude,
            Accuracy = input.Accuracy,
            Speed = input.Speed,
            Heading = input.Heading,
            RecordedAt = input.RecordedAt ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _gpsPointRepository.AddAsync(gpsPoint, cancellationToken);

        // Update trip's current location
        trip.CurrentLatitude = input.Latitude;
        trip.CurrentLongitude = input.Longitude;
        trip.LastLocationUpdateAt = gpsPoint.RecordedAt;
        trip.UpdatedAt = DateTime.UtcNow;
        await _tripRepository.UpdateAsync(trip, cancellationToken);

        // Publish GPS point event
        var outboxMessage = new OutboxMessage
        {
            EventType = "Tracking.GpsPointRecorded",
            AggregateType = nameof(GpsPoint),
            AggregateId = gpsPoint.Id,
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                TripExecutionId = trip.Id,
                TripId = trip.TripId,
                Latitude = input.Latitude,
                Longitude = input.Longitude,
                Altitude = input.Altitude,
                Accuracy = input.Accuracy,
                Speed = input.Speed,
                Heading = input.Heading,
                RecordedAt = gpsPoint.RecordedAt
            }),
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Status = "PENDING"
        };

        await _outboxRepository.AddAsync(outboxMessage, cancellationToken);

        return MapToDto(gpsPoint);
    }

    public async Task<IReadOnlyList<GpsPointDto>> GetGpsPointsAsync(Guid tripId, int? limit, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var points = await _gpsPointRepository.GetByTripExecutionIdAsync(trip.Id, limit ?? 100, cancellationToken);
        return points.Select(MapToDto).ToList().AsReadOnly();
    }

    public async Task<GpsPointDto?> GetLatestGpsPointAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var point = await _gpsPointRepository.GetLatestByTripExecutionIdAsync(trip.Id, cancellationToken);
        return point == null ? null : MapToDto(point);
    }

    public async Task<IReadOnlyList<LocationHistoryDto>> GetLocationHistoryAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var history = await _locationHistoryRepository.GetByTripExecutionIdAsync(trip.Id, cancellationToken);
        return history.Select(MapToDto).ToList().AsReadOnly();
    }

    public async Task<LocationHistoryDto> RecordLocationHistoryAsync(
        Guid tripId,
        decimal latitude,
        decimal longitude,
        Guid? locationId,
        string? locationName,
        CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var history = await _locationHistoryRepository.GetByTripExecutionIdAsync(trip.Id, cancellationToken);
        var sequenceNo = history.Count + 1;

        var locationHistory = new LocationHistory
        {
            TripExecutionId = trip.Id,
            Latitude = latitude,
            Longitude = longitude,
            LocationId = locationId,
            LocationName = locationName,
            ArrivedAt = DateTime.UtcNow,
            SequenceNo = sequenceNo,
            CreatedAt = DateTime.UtcNow
        };

        await _locationHistoryRepository.AddAsync(locationHistory, cancellationToken);
        return MapToDto(locationHistory);
    }

    public async Task<EstimatedTimeOfArrivalDto> CalculateEtaAsync(
        Guid tripId,
        EtaCalculationInput input,
        CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var latestPoint = await _gpsPointRepository.GetLatestByTripExecutionIdAsync(trip.Id, cancellationToken);
        if (latestPoint == null)
            throw new InvalidOperationException("No GPS data available for ETA calculation");

        // Haversine formula to calculate distance
        var distanceKm = CalculateDistanceKm(
            latestPoint.Latitude,
            latestPoint.Longitude,
            input.DestinationLatitude,
            input.DestinationLongitude);

        // Get average speed from recent GPS points
        var recentPoints = await _gpsPointRepository.GetRecentPointsAsync(trip.Id, 10, cancellationToken);
        var averageSpeedKph = CalculateAverageSpeed(recentPoints);

        // Calculate ETA
        int? estimatedMinutesRemaining = averageSpeedKph > 0
            ? (int)Math.Round(((decimal)distanceKm / averageSpeedKph) * 60)
            : null;

        var currentEta = estimatedMinutesRemaining.HasValue
            ? DateTime.UtcNow.AddMinutes((double)estimatedMinutesRemaining.Value)
            : DateTime.UtcNow.AddMinutes((double)(input.PlannedArrivalAtMinutes ?? 60));

        var previousEta = (await _etaRepository.GetLatestByTripExecutionIdAsync(trip.Id, cancellationToken))?.CurrentEta;

        bool? onTrack = previousEta.HasValue
            ? (currentEta <= previousEta.Value)
            : null;

        var eta = new EstimatedTimeOfArrival
        {
            TripExecutionId = trip.Id,
            CurrentEta = currentEta,
            PreviousEta = previousEta,
            DistanceRemainingKm = (decimal)distanceKm,
            EstimatedMinutesRemaining = estimatedMinutesRemaining,
            AverageSpeedKph = averageSpeedKph,
            OnTrack = onTrack,
            CalculatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _etaRepository.AddAsync(eta, cancellationToken);

        // Update trip's ETA
        trip.CurrentEta = currentEta;
        trip.UpdatedAt = DateTime.UtcNow;
        await _tripRepository.UpdateAsync(trip, cancellationToken);

        // Publish ETA updated event
        var outboxMessage = new OutboxMessage
        {
            EventType = "Tracking.EtaUpdated",
            AggregateType = nameof(EstimatedTimeOfArrival),
            AggregateId = eta.Id,
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                TripExecutionId = trip.Id,
                TripId = trip.TripId,
                CurrentEta = currentEta,
                PreviousEta = previousEta,
                DistanceRemainingKm = distanceKm,
                EstimatedMinutesRemaining = estimatedMinutesRemaining,
                AverageSpeedKph = averageSpeedKph,
                OnTrack = onTrack,
                DelayReason = (string?)null
            }),
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Status = "PENDING"
        };

        await _outboxRepository.AddAsync(outboxMessage, cancellationToken);

        return MapToDto(eta);
    }

    public async Task<EstimatedTimeOfArrivalDto?> GetCurrentEtaAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var eta = await _etaRepository.GetLatestByTripExecutionIdAsync(trip.Id, cancellationToken);
        return eta == null ? null : MapToDto(eta);
    }

    private static double CalculateDistanceKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        const double earthRadiusKm = 6371;

        var lat1Rad = Math.PI * (double)lat1 / 180;
        var lat2Rad = Math.PI * (double)lat2 / 180;
        var deltaLatRad = Math.PI * ((double)lat2 - (double)lat1) / 180;
        var deltaLonRad = Math.PI * ((double)lon2 - (double)lon1) / 180;

        var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLonRad / 2) * Math.Sin(deltaLonRad / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static decimal CalculateAverageSpeed(IReadOnlyList<GpsPoint> points)
    {
        if (points.Count < 2)
            return 0;

        var totalDistance = 0d;
        for (int i = 1; i < points.Count; i++)
        {
            totalDistance += CalculateDistanceKm(
                points[i - 1].Latitude,
                points[i - 1].Longitude,
                points[i].Latitude,
                points[i].Longitude);
        }

        var timeDiffMinutes = (points[^1].RecordedAt - points[0].RecordedAt).TotalMinutes;
        if (timeDiffMinutes == 0)
            return 0;

        return (decimal)(totalDistance / (timeDiffMinutes / 60));
    }

    private static GpsPointDto MapToDto(GpsPoint point) => new(
        point.Id,
        point.TripExecutionId,
        point.Latitude,
        point.Longitude,
        point.Altitude,
        point.Accuracy,
        point.Speed,
        point.Heading,
        point.RecordedAt,
        point.CreatedAt
    );

    private static LocationHistoryDto MapToDto(LocationHistory history) => new(
        history.Id,
        history.TripExecutionId,
        history.Latitude,
        history.Longitude,
        history.LocationId,
        history.LocationName,
        history.ArrivedAt,
        history.DepartedAt,
        history.SequenceNo,
        history.CreatedAt
    );

    private static EstimatedTimeOfArrivalDto MapToDto(EstimatedTimeOfArrival eta) => new(
        eta.Id,
        eta.TripExecutionId,
        eta.CurrentEta,
        eta.PreviousEta,
        eta.DistanceRemainingKm,
        eta.EstimatedMinutesRemaining,
        eta.AverageSpeedKph,
        eta.DelayReason,
        eta.OnTrack,
        eta.CalculatedAt,
        eta.UpdatedAt
    );
}

public interface IGpsPointRepository
{
    Task AddAsync(GpsPoint point, CancellationToken cancellationToken);
    Task<GpsPoint?> GetLatestByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GpsPoint>> GetByTripExecutionIdAsync(Guid tripExecutionId, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<GpsPoint>> GetRecentPointsAsync(Guid tripExecutionId, int count, CancellationToken cancellationToken);
}

public interface ILocationHistoryRepository
{
    Task AddAsync(LocationHistory history, CancellationToken cancellationToken);
    Task UpdateAsync(LocationHistory history, CancellationToken cancellationToken);
    Task<IReadOnlyList<LocationHistory>> GetByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken);
}

public interface IEtaRepository
{
    Task AddAsync(EstimatedTimeOfArrival eta, CancellationToken cancellationToken);
    Task<EstimatedTimeOfArrival?> GetLatestByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken);
}
