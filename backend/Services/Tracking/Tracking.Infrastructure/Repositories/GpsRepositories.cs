using Tracking.Application.Services;
using Tracking.Domain;

namespace Tracking.Infrastructure.Repositories;

public class InMemoryGpsPointRepository : IGpsPointRepository
{
    private static readonly Dictionary<Guid, GpsPoint> _store = new();

    public Task AddAsync(GpsPoint point, CancellationToken cancellationToken)
    {
        _store[point.Id] = point;
        return Task.CompletedTask;
    }

    public Task<GpsPoint?> GetLatestByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken)
    {
        var point = _store.Values
            .Where(p => p.TripExecutionId == tripExecutionId)
            .OrderByDescending(p => p.RecordedAt)
            .FirstOrDefault();
        return Task.FromResult(point);
    }

    public Task<IReadOnlyList<GpsPoint>> GetByTripExecutionIdAsync(Guid tripExecutionId, int limit, CancellationToken cancellationToken)
    {
        var points = _store.Values
            .Where(p => p.TripExecutionId == tripExecutionId)
            .OrderByDescending(p => p.RecordedAt)
            .Take(limit)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<GpsPoint>>(points);
    }

    public Task<IReadOnlyList<GpsPoint>> GetRecentPointsAsync(Guid tripExecutionId, int count, CancellationToken cancellationToken)
    {
        var points = _store.Values
            .Where(p => p.TripExecutionId == tripExecutionId)
            .OrderByDescending(p => p.RecordedAt)
            .Take(count)
            .OrderBy(p => p.RecordedAt)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<GpsPoint>>(points);
    }
}

public class InMemoryLocationHistoryRepository : ILocationHistoryRepository
{
    private static readonly Dictionary<Guid, LocationHistory> _store = new();

    public Task AddAsync(LocationHistory history, CancellationToken cancellationToken)
    {
        _store[history.Id] = history;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(LocationHistory history, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(history.Id, out var existing))
        {
            _store[history.Id] = history;
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LocationHistory>> GetByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken)
    {
        var history = _store.Values
            .Where(h => h.TripExecutionId == tripExecutionId)
            .OrderBy(h => h.SequenceNo)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<LocationHistory>>(history);
    }
}

public class InMemoryEtaRepository : IEtaRepository
{
    private static readonly Dictionary<Guid, EstimatedTimeOfArrival> _store = new();

    public Task AddAsync(EstimatedTimeOfArrival eta, CancellationToken cancellationToken)
    {
        _store[eta.Id] = eta;
        return Task.CompletedTask;
    }

    public Task<EstimatedTimeOfArrival?> GetLatestByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken)
    {
        var eta = _store.Values
            .Where(e => e.TripExecutionId == tripExecutionId)
            .OrderByDescending(e => e.CalculatedAt)
            .FirstOrDefault();
        return Task.FromResult(eta);
    }
}
