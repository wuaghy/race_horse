using Tracking.Application.Services;
using Tracking.Domain;

namespace Tracking.Infrastructure.Repositories;

public class InMemoryHealthLogRepository : IHealthLogRepository
{
    private static readonly Dictionary<Guid, HorseHealthLog> _store = new();

    public Task AddAsync(HorseHealthLog log, CancellationToken cancellationToken)
    {
        _store[log.Id] = log;
        return Task.CompletedTask;
    }

    public Task<HorseHealthLog?> GetByIdAndTripExecutionIdAsync(Guid id, Guid tripExecutionId, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(id, out var log) && log.TripExecutionId == tripExecutionId)
        {
            return Task.FromResult<HorseHealthLog?>(log);
        }
        return Task.FromResult<HorseHealthLog?>(null);
    }

    public Task<IReadOnlyList<HorseHealthLog>> GetByTripExecutionIdAsync(Guid tripExecutionId, Guid? horseId, CancellationToken cancellationToken)
    {
        var logs = _store.Values
            .Where(l => l.TripExecutionId == tripExecutionId && (horseId == null || l.HorseId == horseId))
            .OrderByDescending(l => l.RecordedAt)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<HorseHealthLog>>(logs);
    }
}

public class InMemoryHealthAlertRepository : IHealthAlertRepository
{
    private static readonly Dictionary<Guid, HealthAlert> _store = new();

    public Task AddAsync(HealthAlert alert, CancellationToken cancellationToken)
    {
        _store[alert.Id] = alert;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(HealthAlert alert, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(alert.Id, out var existing))
        {
            _store[alert.Id] = alert;
        }
        return Task.CompletedTask;
    }

    public Task<HealthAlert?> GetByIdAndTripExecutionIdAsync(Guid id, Guid tripExecutionId, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(id, out var alert) && alert.TripExecutionId == tripExecutionId)
        {
            return Task.FromResult<HealthAlert?>(alert);
        }
        return Task.FromResult<HealthAlert?>(null);
    }

    public Task<IReadOnlyList<HealthAlert>> GetByTripExecutionIdAsync(Guid tripExecutionId, Guid? horseId, bool? unacknowledgedOnly, CancellationToken cancellationToken)
    {
        var alerts = _store.Values
            .Where(a => a.TripExecutionId == tripExecutionId &&
                       (horseId == null || a.HorseId == horseId) &&
                       (unacknowledgedOnly == null || (unacknowledgedOnly.Value ? !a.Acknowledged : true)))
            .OrderByDescending(a => a.AlertedAt)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<HealthAlert>>(alerts);
    }
}

public class InMemoryHealthStatusRepository : IHealthStatusRepository
{
    private static readonly Dictionary<string, HorseHealthStatus> _store = new();

    public Task UpsertAsync(HorseHealthStatus status, CancellationToken cancellationToken)
    {
        var key = GetKey(status.TripExecutionId, status.HorseId);
        if (_store.TryGetValue(key, out var existing))
        {
            status.Id = existing.Id;
        }
        else
        {
            status.Id = Guid.NewGuid();
        }
        _store[key] = status;
        return Task.CompletedTask;
    }

    public Task<HorseHealthStatus?> GetByTripExecutionIdAndHorseIdAsync(Guid tripExecutionId, Guid? horseId, CancellationToken cancellationToken)
    {
        var key = GetKey(tripExecutionId, horseId);
        _store.TryGetValue(key, out var status);
        return Task.FromResult(status);
    }

    private static string GetKey(Guid tripExecutionId, Guid? horseId) =>
        $"{tripExecutionId}:{horseId}";
}
