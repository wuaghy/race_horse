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

    public async Task<IReadOnlyList<GpsPoint>> GetByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken)
    {
        return await _context.GpsPoints
            .Where(g => g.TripExecutionId == tripExecutionId)
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

    public async Task AddOrUpdateAsync(EstimatedTimeOfArrival eta, CancellationToken cancellationToken)
    {
        var existing = await _context.EstimatedTimesOfArrival
            .FirstOrDefaultAsync(e => e.TripExecutionId == eta.TripExecutionId, cancellationToken);

        if (existing is null)
        {
            _context.EstimatedTimesOfArrival.Add(eta);
        }
        else
        {
            _context.EstimatedTimesOfArrival.Update(eta);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<EstimatedTimeOfArrival?> GetByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken)
    {
        return await _context.EstimatedTimesOfArrival
            .FirstOrDefaultAsync(e => e.TripExecutionId == tripExecutionId, cancellationToken);
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

    public async Task<IReadOnlyList<HorseHealthLog>> GetByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken)
    {
        return await _context.HealthLogs
            .Where(h => h.TripExecutionId == tripExecutionId)
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

    public async Task<IReadOnlyList<HealthAlert>> GetByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken)
    {
        return await _context.HealthAlerts
            .Where(a => a.TripExecutionId == tripExecutionId)
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

    public async Task AddOrUpdateAsync(HorseHealthStatus status, CancellationToken cancellationToken)
    {
        var existing = await _context.HealthStatuses
            .FirstOrDefaultAsync(s => s.TripExecutionId == status.TripExecutionId, cancellationToken);

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

    public async Task<HorseHealthStatus?> GetByTripExecutionIdAsync(Guid tripExecutionId, CancellationToken cancellationToken)
    {
        return await _context.HealthStatuses
            .FirstOrDefaultAsync(s => s.TripExecutionId == tripExecutionId, cancellationToken);
    }
}
