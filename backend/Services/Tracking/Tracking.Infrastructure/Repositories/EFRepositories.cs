using Microsoft.EntityFrameworkCore;
using Tracking.Application.Services;
using Tracking.Domain;
using Tracking.Infrastructure.Persistence;

namespace Tracking.Infrastructure.Repositories;

public class EFTripExecutionRepository : ITripExecutionRepository
{
    private readonly TrackingDbContext _context;

    public EFTripExecutionRepository(TrackingDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(TripExecution trip, CancellationToken cancellationToken)
    {
        _context.TripExecutions.Add(trip);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TripExecution trip, CancellationToken cancellationToken)
    {
        _context.TripExecutions.Update(trip);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TripExecution?> GetByTripIdAsync(Guid tripId, CancellationToken cancellationToken)
    {
        return await _context.TripExecutions
            .FirstOrDefaultAsync(t => t.TripId == tripId, cancellationToken);
    }
}

public class EFCheckpointEventRepository : ICheckpointEventRepository
{
    private readonly TrackingDbContext _context;

    public EFCheckpointEventRepository(TrackingDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CheckpointEvent checkpointEvent, CancellationToken cancellationToken)
    {
        _context.CheckpointEvents.Add(checkpointEvent);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CheckpointEvent checkpointEvent, CancellationToken cancellationToken)
    {
        _context.CheckpointEvents.Update(checkpointEvent);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<CheckpointEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.CheckpointEvents
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<CheckpointEvent?> GetByIdAndTripExecutionIdAsync(Guid id, Guid tripExecutionId, CancellationToken cancellationToken)
    {
        return await _context.CheckpointEvents
            .FirstOrDefaultAsync(c => c.Id == id && c.TripExecutionId == tripExecutionId, cancellationToken);
    }

    public async Task<IReadOnlyList<CheckpointEvent>> GetByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken)
    {
        return await _context.CheckpointEvents
            .Where(c => c.TripExecutionId == tripExecutionId)
            .OrderBy(c => c.SequenceNo)
            .ToListAsync(cancellationToken);
    }
}

public class EFOutboxMessageRepository : IOutboxMessageRepository
{
    private readonly TrackingDbContext _context;

    public EFOutboxMessageRepository(TrackingDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        _context.OutboxMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        _context.OutboxMessages.Update(message);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int limit, CancellationToken cancellationToken)
    {
        return await _context.OutboxMessages
            .Where(m => m.Status == "PENDING")
            .OrderBy(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}

public class EFGpsPointRepository : IGpsPointRepository
{
    private readonly TrackingDbContext _context;

    public EFGpsPointRepository(TrackingDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(GpsPoint point, CancellationToken cancellationToken)
    {
        _context.GpsPoints.Add(point);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<GpsPoint?> GetLatestByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken)
    {
        return await _context.GpsPoints
            .Where(g => g.TripExecutionId == tripExecutionId)
            .OrderByDescending(g => g.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GpsPoint>> GetByTripExecutionIdAsync(Guid tripExecutionId, int limit, CancellationToken cancellationToken)
    {
        return await _context.GpsPoints
            .Where(g => g.TripExecutionId == tripExecutionId)
            .OrderByDescending(g => g.RecordedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GpsPoint>> GetRecentPointsAsync(Guid tripExecutionId, int count, CancellationToken cancellationToken)
    {
        return await _context.GpsPoints
            .Where(g => g.TripExecutionId == tripExecutionId)
            .OrderByDescending(g => g.RecordedAt)
            .Take(count)
            .OrderBy(g => g.RecordedAt)
            .ToListAsync(cancellationToken);
    }
}

public class EFLocationHistoryRepository : ILocationHistoryRepository
{
    private readonly TrackingDbContext _context;

    public EFLocationHistoryRepository(TrackingDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(LocationHistory history, CancellationToken cancellationToken)
    {
        _context.LocationHistories.Add(history);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(LocationHistory history, CancellationToken cancellationToken)
    {
        _context.LocationHistories.Update(history);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LocationHistory>> GetByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken)
    {
        return await _context.LocationHistories
            .Where(l => l.TripExecutionId == tripExecutionId)
            .OrderBy(l => l.SequenceNo)
            .ToListAsync(cancellationToken);
    }
}

public class EFEtaRepository : IEtaRepository
{
    private readonly TrackingDbContext _context;

    public EFEtaRepository(TrackingDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(EstimatedTimeOfArrival eta, CancellationToken cancellationToken)
    {
        _context.EstimatedTimesOfArrival.Add(eta);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<EstimatedTimeOfArrival?> GetLatestByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken)
    {
        return await _context.EstimatedTimesOfArrival
            .Where(e => e.TripExecutionId == tripExecutionId)
            .OrderByDescending(e => e.CalculatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public class EFHealthLogRepository : IHealthLogRepository
{
    private readonly TrackingDbContext _context;

    public EFHealthLogRepository(TrackingDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(HorseHealthLog log, CancellationToken cancellationToken)
    {
        _context.HealthLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<HorseHealthLog?> GetByIdAndTripExecutionIdAsync(Guid id, Guid tripExecutionId, CancellationToken cancellationToken)
    {
        return await _context.HealthLogs
            .FirstOrDefaultAsync(h => h.Id == id && h.TripExecutionId == tripExecutionId, cancellationToken);
    }

    public async Task<IReadOnlyList<HorseHealthLog>> GetByTripExecutionIdAsync(Guid tripExecutionId, Guid? horseId, CancellationToken cancellationToken)
    {
        var query = _context.HealthLogs
            .Where(h => h.TripExecutionId == tripExecutionId);

        if (horseId.HasValue)
        {
            query = query.Where(h => h.HorseId == horseId.Value);
        }

        return await query
            .OrderByDescending(h => h.RecordedAt)
            .ToListAsync(cancellationToken);
    }
}

public class EFHealthAlertRepository : IHealthAlertRepository
{
    private readonly TrackingDbContext _context;

    public EFHealthAlertRepository(TrackingDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(HealthAlert alert, CancellationToken cancellationToken)
    {
        _context.HealthAlerts.Add(alert);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(HealthAlert alert, CancellationToken cancellationToken)
    {
        _context.HealthAlerts.Update(alert);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<HealthAlert?> GetByIdAndTripExecutionIdAsync(Guid id, Guid tripExecutionId, CancellationToken cancellationToken)
    {
        return await _context.HealthAlerts
            .FirstOrDefaultAsync(a => a.Id == id && a.TripExecutionId == tripExecutionId, cancellationToken);
    }

    public async Task<IReadOnlyList<HealthAlert>> GetByTripExecutionIdAsync(Guid tripExecutionId, Guid? horseId, bool? unacknowledgedOnly, CancellationToken cancellationToken)
    {
        var query = _context.HealthAlerts
            .Where(a => a.TripExecutionId == tripExecutionId);

        if (horseId.HasValue)
        {
            query = query.Where(a => a.HorseId == horseId.Value);
        }

        if (unacknowledgedOnly == true)
        {
            query = query.Where(a => !a.Acknowledged);
        }

        return await query
            .OrderByDescending(a => a.AlertedAt)
            .ToListAsync(cancellationToken);
    }
}

public class EFHealthStatusRepository : IHealthStatusRepository
{
    private readonly TrackingDbContext _context;

    public EFHealthStatusRepository(TrackingDbContext context)
    {
        _context = context;
    }

    public async Task UpsertAsync(HorseHealthStatus status, CancellationToken cancellationToken)
    {
        var existing = await _context.HealthStatuses
            .FirstOrDefaultAsync(s => s.TripExecutionId == status.TripExecutionId && s.HorseId == status.HorseId, cancellationToken);

        if (existing is null)
        {
            _context.HealthStatuses.Add(status);
        }
        else
        {
            _context.HealthStatuses.Update(status);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<HorseHealthStatus?> GetByTripExecutionIdAndHorseIdAsync(Guid tripExecutionId, Guid? horseId, CancellationToken cancellationToken)
    {
        return await _context.HealthStatuses
            .FirstOrDefaultAsync(s => s.TripExecutionId == tripExecutionId && s.HorseId == horseId, cancellationToken);
    }
}
