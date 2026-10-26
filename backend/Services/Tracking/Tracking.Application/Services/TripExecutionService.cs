using Tracking.Application.DTOs;
using Tracking.Domain;

namespace Tracking.Application.Services;

public interface ITripExecutionService
{
    Task<TripExecutionDto> InitiateTripExecutionAsync(InitiateTripExecutionInput input, CancellationToken cancellationToken);
    Task<TripExecutionDto?> GetTripExecutionAsync(Guid tripId, CancellationToken cancellationToken);
    Task<TripExecutionDto> RecordTripDepartureAsync(Guid tripId, decimal latitude, decimal longitude, Guid actorUserId, CancellationToken cancellationToken);
    Task<TripExecutionDto> RecordTripArrivalAsync(Guid tripId, decimal latitude, decimal longitude, CancellationToken cancellationToken);
    Task<TripExecutionDto> UpdateTripLocationAsync(Guid tripId, UpdateTripLocationInput input, CancellationToken cancellationToken);
    Task<CheckpointEventDto> RecordCheckpointEventAsync(Guid tripId, RecordCheckpointEventInput input, CancellationToken cancellationToken);
    Task<IReadOnlyList<CheckpointEventDto>> GetCheckpointEventsAsync(Guid tripId, CancellationToken cancellationToken);
    Task<CheckpointEventDto?> GetCheckpointEventAsync(Guid tripId, Guid checkpointEventId, CancellationToken cancellationToken);
}

public class TripExecutionService : ITripExecutionService
{
    private readonly ITripExecutionRepository _tripRepository;
    private readonly ICheckpointEventRepository _checkpointRepository;
    private readonly IOutboxMessageRepository _outboxRepository;

    public TripExecutionService(
        ITripExecutionRepository tripRepository,
        ICheckpointEventRepository checkpointRepository,
        IOutboxMessageRepository outboxRepository)
    {
        _tripRepository = tripRepository;
        _checkpointRepository = checkpointRepository;
        _outboxRepository = outboxRepository;
    }

    public async Task<TripExecutionDto> InitiateTripExecutionAsync(InitiateTripExecutionInput input, CancellationToken cancellationToken)
    {
        var tripExecution = new TripExecution
        {
            TripId = input.TripId,
            TripNo = input.TripNo,
            Status = TrackingConstants.TripExecutionStatus.Planned,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _tripRepository.AddAsync(tripExecution, cancellationToken);
        return MapToDto(tripExecution);
    }

    public async Task<TripExecutionDto?> GetTripExecutionAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken);
        return trip == null ? null : MapToDto(trip);
    }

    public async Task<TripExecutionDto> RecordTripDepartureAsync(Guid tripId, decimal latitude, decimal longitude, Guid actorUserId, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        trip.Status = TrackingConstants.TripExecutionStatus.Departed;
        trip.DepartureAt = DateTime.UtcNow;
        trip.CurrentLatitude = latitude;
        trip.CurrentLongitude = longitude;
        trip.LastLocationUpdateAt = DateTime.UtcNow;
        trip.UpdatedAt = DateTime.UtcNow;
        trip.VersionNo++;

        var departureEvent = await AddMovementCheckpointAsync(
            trip,
            TrackingConstants.CheckpointType.Departure,
            TrackingConstants.CheckpointStatus.Departed,
            trip.DepartureAt.Value,
            latitude,
            longitude,
            "Trip departure recorded.",
            cancellationToken);
        trip.CurrentCheckpointId = departureEvent.Id;

        await _tripRepository.UpdateAsync(trip, cancellationToken);

        // Publish integration event
        var outboxMessage = new OutboxMessage
        {
            EventType = "Tracking.TripDeparted",
            AggregateType = nameof(TripExecution),
            AggregateId = trip.Id,
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                TripId = trip.TripId,
                TripNo = trip.TripNo,
                DepartedByUserId = actorUserId,
                DepartedAt = trip.DepartureAt,
                Latitude = latitude,
                Longitude = longitude
            }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Status = "PENDING"
        };

        await _outboxRepository.AddAsync(outboxMessage, cancellationToken);

        return MapToDto(trip);
    }

    public async Task<TripExecutionDto> RecordTripArrivalAsync(Guid tripId, decimal latitude, decimal longitude, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        trip.Status = TrackingConstants.TripExecutionStatus.Arrived;
        trip.ArrivalAt = DateTime.UtcNow;
        trip.CurrentLatitude = latitude;
        trip.CurrentLongitude = longitude;
        trip.LastLocationUpdateAt = DateTime.UtcNow;
        trip.UpdatedAt = DateTime.UtcNow;
        trip.VersionNo++;

        var arrivalEvent = await AddMovementCheckpointAsync(
            trip,
            TrackingConstants.CheckpointType.Destination,
            TrackingConstants.CheckpointStatus.Arrived,
            trip.ArrivalAt.Value,
            latitude,
            longitude,
            "Trip arrival recorded.",
            cancellationToken);
        trip.CurrentCheckpointId = arrivalEvent.Id;

        await _tripRepository.UpdateAsync(trip, cancellationToken);

        // Publish integration event
        var outboxMessage = new OutboxMessage
        {
            EventType = "Tracking.TripArrived",
            AggregateType = nameof(TripExecution),
            AggregateId = trip.Id,
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                TripId = trip.TripId,
                TripNo = trip.TripNo,
                ArrivedAt = trip.ArrivalAt,
                Latitude = latitude,
                Longitude = longitude
            }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Status = "PENDING"
        };

        await _outboxRepository.AddAsync(outboxMessage, cancellationToken);

        return MapToDto(trip);
    }

    public async Task<TripExecutionDto> UpdateTripLocationAsync(Guid tripId, UpdateTripLocationInput input, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        trip.CurrentLatitude = input.Latitude;
        trip.CurrentLongitude = input.Longitude;
        trip.LastLocationUpdateAt = input.UpdatedAt;
        trip.UpdatedAt = DateTime.UtcNow;

        await _tripRepository.UpdateAsync(trip, cancellationToken);
        return MapToDto(trip);
    }

    public async Task<CheckpointEventDto> RecordCheckpointEventAsync(Guid tripId, RecordCheckpointEventInput input, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var checkpointEvents = await _checkpointRepository.GetByTripExecutionIdAsync(trip.Id, cancellationToken);
        var sequenceNo = checkpointEvents.Count + 1;

        var checkpointEvent = new CheckpointEvent
        {
            TripExecutionId = trip.Id,
            LocationId = input.LocationId,
            SequenceNo = sequenceNo,
            CheckpointType = input.CheckpointType,
            Status = input.Status,
            ArrivalLatitude = input.Latitude,
            ArrivalLongitude = input.Longitude,
            Notes = input.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (input.Status == TrackingConstants.CheckpointStatus.Arrived)
        {
            checkpointEvent.ActualArrivalAt = input.OccurredAt;
        }
        else if (input.Status == TrackingConstants.CheckpointStatus.Departed)
        {
            checkpointEvent.ActualDepartureAt = input.OccurredAt;
        }

        await _checkpointRepository.AddAsync(checkpointEvent, cancellationToken);

        // Update trip's current checkpoint
        trip.CurrentCheckpointId = checkpointEvent.Id;
        trip.LastLocationUpdateAt = input.OccurredAt;
        trip.UpdatedAt = DateTime.UtcNow;
        await _tripRepository.UpdateAsync(trip, cancellationToken);

        // Publish integration event
        var outboxMessage = new OutboxMessage
        {
            EventType = "Tracking.CheckpointEventOccurred",
            AggregateType = nameof(CheckpointEvent),
            AggregateId = checkpointEvent.Id,
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                TripExecutionId = trip.Id,
                TripId = trip.TripId,
                SequenceNo = sequenceNo,
                CheckpointType = input.CheckpointType,
                Status = input.Status,
                LocationId = input.LocationId,
                EventAt = input.OccurredAt,
                Latitude = input.Latitude,
                Longitude = input.Longitude,
                Notes = input.Notes
            }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Status = "PENDING"
        };

        await _outboxRepository.AddAsync(outboxMessage, cancellationToken);

        return MapToDto(checkpointEvent);
    }

    private async Task<CheckpointEvent> AddMovementCheckpointAsync(
        TripExecution trip,
        string checkpointType,
        string status,
        DateTime occurredAt,
        decimal latitude,
        decimal longitude,
        string notes,
        CancellationToken cancellationToken)
    {
        var existing = await _checkpointRepository.GetByTripExecutionIdAsync(trip.Id, cancellationToken);
        var checkpointEvent = new CheckpointEvent
        {
            TripExecutionId = trip.Id,
            LocationId = null,
            SequenceNo = existing.Count + 1,
            CheckpointType = checkpointType,
            Status = status,
            ArrivalLatitude = latitude,
            ArrivalLongitude = longitude,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (status == TrackingConstants.CheckpointStatus.Arrived)
        {
            checkpointEvent.ActualArrivalAt = occurredAt;
        }
        else if (status == TrackingConstants.CheckpointStatus.Departed)
        {
            checkpointEvent.ActualDepartureAt = occurredAt;
        }

        await _checkpointRepository.AddAsync(checkpointEvent, cancellationToken);
        return checkpointEvent;
    }

    public async Task<IReadOnlyList<CheckpointEventDto>> GetCheckpointEventsAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return Array.Empty<CheckpointEventDto>();
        }

        var events = await _checkpointRepository.GetByTripExecutionIdAsync(trip.Id, cancellationToken);
        return events.Select(MapToDto).ToList().AsReadOnly();
    }

    public async Task<CheckpointEventDto?> GetCheckpointEventAsync(Guid tripId, Guid checkpointEventId, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var checkpointEvent = await _checkpointRepository.GetByIdAndTripExecutionIdAsync(checkpointEventId, trip.Id, cancellationToken);
        return checkpointEvent == null ? null : MapToDto(checkpointEvent);
    }

    private static TripExecutionDto MapToDto(TripExecution trip) => new(
        trip.Id,
        trip.TripId,
        trip.TripNo,
        trip.Status,
        trip.DepartureAt,
        trip.ArrivalAt,
        trip.CurrentCheckpointId,
        trip.CurrentLatitude,
        trip.CurrentLongitude,
        trip.LastLocationUpdateAt,
        trip.CreatedAt,
        trip.UpdatedAt,
        trip.VersionNo
    );

    private static CheckpointEventDto MapToDto(CheckpointEvent checkpointEvent) => new(
        checkpointEvent.Id,
        checkpointEvent.TripExecutionId,
        checkpointEvent.LocationId,
        checkpointEvent.SequenceNo,
        checkpointEvent.CheckpointType,
        checkpointEvent.Status,
        checkpointEvent.PlannedArrivalAt,
        checkpointEvent.ActualArrivalAt,
        checkpointEvent.PlannedDepartureAt,
        checkpointEvent.ActualDepartureAt,
        checkpointEvent.ArrivalLatitude,
        checkpointEvent.ArrivalLongitude,
        checkpointEvent.Notes,
        checkpointEvent.CreatedAt,
        checkpointEvent.UpdatedAt,
        checkpointEvent.VersionNo
    );
}

public interface ITripExecutionRepository
{
    Task AddAsync(TripExecution trip, CancellationToken cancellationToken);
    Task UpdateAsync(TripExecution trip, CancellationToken cancellationToken);
    Task<TripExecution?> GetByTripIdAsync(Guid tripId, CancellationToken cancellationToken);
}

public interface ICheckpointEventRepository
{
    Task AddAsync(CheckpointEvent checkpointEvent, CancellationToken cancellationToken);
    Task UpdateAsync(CheckpointEvent checkpointEvent, CancellationToken cancellationToken);
    Task<CheckpointEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<CheckpointEvent?> GetByIdAndTripExecutionIdAsync(Guid id, Guid tripExecutionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CheckpointEvent>> GetByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken);
}

public interface IOutboxMessageRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken);
    Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int limit, CancellationToken cancellationToken);
}
