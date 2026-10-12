using Tracking.Application.Services;
using Tracking.Domain;

namespace Tracking.Infrastructure.Repositories;

public class InMemoryTripExecutionRepository : ITripExecutionRepository
{
    private static readonly Dictionary<Guid, TripExecution> _store = new();

    public Task AddAsync(TripExecution trip, CancellationToken cancellationToken)
    {
        _store[trip.TripId] = trip;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TripExecution trip, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(trip.TripId, out var existing))
        {
            _store[trip.TripId] = trip;
        }
        return Task.CompletedTask;
    }

    public Task<TripExecution?> GetByTripIdAsync(Guid tripId, CancellationToken cancellationToken)
    {
        _store.TryGetValue(tripId, out var trip);
        return Task.FromResult(trip);
    }
}

public class InMemoryCheckpointEventRepository : ICheckpointEventRepository
{
    private static readonly Dictionary<Guid, CheckpointEvent> _store = new();

    public Task AddAsync(CheckpointEvent checkpointEvent, CancellationToken cancellationToken)
    {
        _store[checkpointEvent.Id] = checkpointEvent;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CheckpointEvent checkpointEvent, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(checkpointEvent.Id, out var existing))
        {
            _store[checkpointEvent.Id] = checkpointEvent;
        }
        return Task.CompletedTask;
    }

    public Task<CheckpointEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        _store.TryGetValue(id, out var checkpointEvent);
        return Task.FromResult(checkpointEvent);
    }

    public Task<CheckpointEvent?> GetByIdAndTripExecutionIdAsync(Guid id, Guid tripExecutionId, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(id, out var checkpointEvent) && checkpointEvent.TripExecutionId == tripExecutionId)
        {
            return Task.FromResult<CheckpointEvent?>(checkpointEvent);
        }
        return Task.FromResult<CheckpointEvent?>(null);
    }

    public Task<IReadOnlyList<CheckpointEvent>> GetByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken)
    {
        var events = _store.Values
            .Where(e => e.TripExecutionId == tripExecutionId)
            .OrderBy(e => e.SequenceNo)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<CheckpointEvent>>(events);
    }
}

public class InMemoryOutboxMessageRepository : IOutboxMessageRepository
{
    private static readonly Dictionary<Guid, OutboxMessage> _store = new();

    public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        _store[message.Id] = message;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(message.Id, out var existing))
        {
            _store[message.Id] = message;
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int limit, CancellationToken cancellationToken)
    {
        var pending = _store.Values
            .Where(m => m.Status == "PENDING")
            .Take(limit)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<OutboxMessage>>(pending);
    }
}
