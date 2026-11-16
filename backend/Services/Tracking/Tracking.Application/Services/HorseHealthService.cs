using Tracking.Application.DTOs;
using Tracking.Domain;

namespace Tracking.Application.Services;

public interface IHorseHealthService
{
    Task<HorseHealthLogDto> RecordHealthLogAsync(Guid tripId, RecordHealthLogInput input, CancellationToken cancellationToken);
    Task<IReadOnlyList<HorseHealthLogDto>> GetHealthLogsAsync(Guid tripId, Guid? horseId, CancellationToken cancellationToken);
    Task<HorseHealthLogDto?> GetHealthLogAsync(Guid tripId, Guid healthLogId, CancellationToken cancellationToken);
    Task<HealthAlertDto> CreateHealthAlertAsync(Guid tripId, CreateHealthAlertInput input, CancellationToken cancellationToken);
    Task<IReadOnlyList<HealthAlertDto>> GetHealthAlertsAsync(Guid tripId, Guid? horseId, bool? unacknowledgedOnly, CancellationToken cancellationToken);
    Task<HealthAlertDto> AcknowledgeAlertAsync(Guid tripId, Guid alertId, AcknowledgeAlertInput input, CancellationToken cancellationToken);
    Task<HorseHealthStatusDto> GetHealthStatusAsync(Guid tripId, Guid? horseId, CancellationToken cancellationToken);
}

public class HorseHealthService : IHorseHealthService
{
    private readonly IHealthLogRepository _healthLogRepository;
    private readonly IHealthAlertRepository _alertRepository;
    private readonly IHealthStatusRepository _statusRepository;
    private readonly ITripExecutionRepository _tripRepository;
    private readonly IOutboxMessageRepository _outboxRepository;

    public HorseHealthService(
        IHealthLogRepository healthLogRepository,
        IHealthAlertRepository alertRepository,
        IHealthStatusRepository statusRepository,
        ITripExecutionRepository tripRepository,
        IOutboxMessageRepository outboxRepository)
    {
        _healthLogRepository = healthLogRepository;
        _alertRepository = alertRepository;
        _statusRepository = statusRepository;
        _tripRepository = tripRepository;
        _outboxRepository = outboxRepository;
    }

    public async Task<HorseHealthLogDto> RecordHealthLogAsync(Guid tripId, RecordHealthLogInput input, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var healthLog = new HorseHealthLog
        {
            TripExecutionId = trip.Id,
            HorseId = input.HorseId,
            RecordedAt = input.RecordedAt ?? DateTime.UtcNow,
            ConditionType = input.ConditionType ?? HealthConstants.ConditionType.Good,
            StressLevel = input.StressLevel,
            BodyTemperature = input.BodyTemperature,
            HeartRate = input.HeartRate,
            RespiratoryRate = input.RespiratoryRate,
            Observation = input.Observation,
            PhotoUrl = input.PhotoUrl,
            RecordedByUserId = input.RecordedByUserId,
            CreatedAt = DateTime.UtcNow
        };

        await _healthLogRepository.AddAsync(healthLog, cancellationToken);

        // Update health status
        var status = await _statusRepository.GetByTripExecutionIdAndHorseIdAsync(trip.Id, input.HorseId, cancellationToken)
            ?? new HorseHealthStatus { TripExecutionId = trip.Id, HorseId = input.HorseId };

        status.CurrentStressLevel = input.StressLevel;
        status.LastHealthCheckAt = healthLog.RecordedAt;
        status.UpdatedAt = DateTime.UtcNow;

        if (input.StressLevel.HasValue && input.StressLevel > 75)
        {
            status.CurrentStatus = HealthConstants.HealthStatus.Warning;
        }

        await _statusRepository.UpsertAsync(status, cancellationToken);

        return MapToDto(healthLog);
    }

    public async Task<IReadOnlyList<HorseHealthLogDto>> GetHealthLogsAsync(Guid tripId, Guid? horseId, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var logs = await _healthLogRepository.GetByTripExecutionIdAsync(trip.Id, horseId, cancellationToken);
        return logs.Select(MapToDto).ToList().AsReadOnly();
    }

    public async Task<HorseHealthLogDto?> GetHealthLogAsync(Guid tripId, Guid healthLogId, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var log = await _healthLogRepository.GetByIdAndTripExecutionIdAsync(healthLogId, trip.Id, cancellationToken);
        return log == null ? null : MapToDto(log);
    }

    public async Task<HealthAlertDto> CreateHealthAlertAsync(Guid tripId, CreateHealthAlertInput input, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var alert = new HealthAlert
        {
            TripExecutionId = trip.Id,
            HorseId = input.HorseId,
            AlertType = input.AlertType ?? HealthConstants.AlertType.Other,
            Severity = input.Severity ?? HealthConstants.AlertSeverity.Medium,
            Message = input.Message ?? "Health alert",
            MetricValue = input.MetricValue,
            MetricThreshold = input.MetricThreshold,
            Recommendation = input.Recommendation,
            Acknowledged = false,
            AlertedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _alertRepository.AddAsync(alert, cancellationToken);

        // Update health status
        var status = await _statusRepository.GetByTripExecutionIdAndHorseIdAsync(trip.Id, input.HorseId, cancellationToken)
            ?? new HorseHealthStatus { TripExecutionId = trip.Id, HorseId = input.HorseId };

        status.AlertCount = (status.AlertCount ?? 0) + 1;
        status.LastAlertAt = DateTime.UtcNow;

        if (input.Severity == HealthConstants.AlertSeverity.Critical || input.Severity == HealthConstants.AlertSeverity.High)
        {
            status.CurrentStatus = HealthConstants.HealthStatus.Critical;
        }

        status.UpdatedAt = DateTime.UtcNow;
        await _statusRepository.UpsertAsync(status, cancellationToken);

        // Publish health alert raised event
        var outboxMessage = new OutboxMessage
        {
            EventType = "Tracking.HealthAlertRaised",
            AggregateType = nameof(HealthAlert),
            AggregateId = alert.Id,
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                TripExecutionId = trip.Id,
                HorseId = input.HorseId,
                AlertType = alert.AlertType,
                Severity = alert.Severity,
                Message = alert.Message,
                AlertAt = alert.AlertedAt
            }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Status = "PENDING"
        };

        await _outboxRepository.AddAsync(outboxMessage, cancellationToken);

        return MapToDto(alert);
    }

    public async Task<IReadOnlyList<HealthAlertDto>> GetHealthAlertsAsync(Guid tripId, Guid? horseId, bool? unacknowledgedOnly, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var alerts = await _alertRepository.GetByTripExecutionIdAsync(trip.Id, horseId, unacknowledgedOnly, cancellationToken);
        return alerts.Select(MapToDto).ToList().AsReadOnly();
    }

    public async Task<HealthAlertDto> AcknowledgeAlertAsync(Guid tripId, Guid alertId, AcknowledgeAlertInput input, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var alert = await _alertRepository.GetByIdAndTripExecutionIdAsync(alertId, trip.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Alert {alertId} not found");

        alert.Acknowledged = true;
        alert.AcknowledgedAt = DateTime.UtcNow;
        alert.AcknowledgedByUserId = input.AcknowledgedByUserId;
        alert.UpdatedAt = DateTime.UtcNow;

        await _alertRepository.UpdateAsync(alert, cancellationToken);

        // Update health status
        var status = await _statusRepository.GetByTripExecutionIdAndHorseIdAsync(trip.Id, alert.HorseId, cancellationToken);
        if (status != null)
        {
            status.AcknowledgedAlertCount = (status.AcknowledgedAlertCount ?? 0) + 1;
            status.UpdatedAt = DateTime.UtcNow;
            await _statusRepository.UpsertAsync(status, cancellationToken);
        }

        return MapToDto(alert);
    }

    public async Task<HorseHealthStatusDto> GetHealthStatusAsync(Guid tripId, Guid? horseId, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByTripIdAsync(tripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");

        var status = await _statusRepository.GetByTripExecutionIdAndHorseIdAsync(trip.Id, horseId, cancellationToken)
            ?? new HorseHealthStatus { TripExecutionId = trip.Id, HorseId = horseId };

        return MapToDto(status);
    }

    private static HorseHealthLogDto MapToDto(HorseHealthLog log) => new(
        log.Id,
        log.TripExecutionId,
        log.HorseId,
        log.RecordedAt,
        log.ConditionType,
        log.StressLevel,
        log.BodyTemperature,
        log.HeartRate,
        log.RespiratoryRate,
        log.Observation,
        log.PhotoUrl,
        log.RecordedByUserId,
        log.CreatedAt
    );

    private static HealthAlertDto MapToDto(HealthAlert alert) => new(
        alert.Id,
        alert.TripExecutionId,
        alert.HorseId,
        alert.AlertType,
        alert.Severity,
        alert.Message,
        alert.MetricValue,
        alert.MetricThreshold,
        alert.Recommendation,
        alert.Acknowledged,
        alert.AcknowledgedAt,
        alert.AcknowledgedByUserId,
        alert.AlertedAt,
        alert.CreatedAt,
        alert.UpdatedAt
    );

    private static HorseHealthStatusDto MapToDto(HorseHealthStatus status) => new(
        status.Id,
        status.TripExecutionId,
        status.HorseId,
        status.CurrentStatus,
        status.CurrentStressLevel,
        status.AlertCount,
        status.AcknowledgedAlertCount,
        status.LastHealthCheckAt,
        status.LastAlertAt,
        status.UpdatedAt
    );
}

public interface IHealthLogRepository
{
    Task AddAsync(HorseHealthLog log, CancellationToken cancellationToken);
    Task<HorseHealthLog?> GetByIdAndTripExecutionIdAsync(Guid id, Guid tripExecutionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<HorseHealthLog>> GetByTripExecutionIdAsync(Guid tripExecutionId, Guid? horseId, CancellationToken cancellationToken);
}

public interface IHealthAlertRepository
{
    Task AddAsync(HealthAlert alert, CancellationToken cancellationToken);
    Task UpdateAsync(HealthAlert alert, CancellationToken cancellationToken);
    Task<HealthAlert?> GetByIdAndTripExecutionIdAsync(Guid id, Guid tripExecutionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<HealthAlert>> GetByTripExecutionIdAsync(Guid tripExecutionId, Guid? horseId, bool? unacknowledgedOnly, CancellationToken cancellationToken);
}

public interface IHealthStatusRepository
{
    Task UpsertAsync(HorseHealthStatus status, CancellationToken cancellationToken);
    Task<HorseHealthStatus?> GetByTripExecutionIdAndHorseIdAsync(Guid tripExecutionId, Guid? horseId, CancellationToken cancellationToken);
}
