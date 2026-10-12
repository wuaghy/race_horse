namespace Tracking.Domain;

public static class HealthConstants
{
    public static class HealthStatus
    {
        public const string Healthy = "HEALTHY";
        public const string Warning = "WARNING";
        public const string Critical = "CRITICAL";
    }

    public static class AlertSeverity
    {
        public const string Low = "LOW";
        public const string Medium = "MEDIUM";
        public const string High = "HIGH";
        public const string Critical = "CRITICAL";
    }

    public static class AlertType
    {
        public const string TemperatureAbnormal = "TEMPERATURE_ABNORMAL";
        public const string HeartRateAbnormal = "HEART_RATE_ABNORMAL";
        public const string StressHigh = "STRESS_HIGH";
        public const string InjuryDetected = "INJURY_DETECTED";
        public const string FeedingIssue = "FEEDING_ISSUE";
        public const string HydrationLow = "HYDRATION_LOW";
        public const string BehaviorAbnormal = "BEHAVIOR_ABNORMAL";
        public const string Other = "OTHER";
    }

    public static class ConditionType
    {
        public const string Excellent = "EXCELLENT";
        public const string Good = "GOOD";
        public const string Fair = "FAIR";
        public const string Poor = "POOR";
    }
}

public sealed class HorseHealthLog : Entity
{
    public Guid TripExecutionId { get; set; }
    public Guid? HorseId { get; set; }
    public DateTime RecordedAt { get; set; }
    public string ConditionType { get; set; } = null!;
    public int? StressLevel { get; set; }
    public decimal? BodyTemperature { get; set; }
    public int? HeartRate { get; set; }
    public int? RespiratoryRate { get; set; }
    public string? Observation { get; set; }
    public string? PhotoUrl { get; set; }
    public string? RecordedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class HealthAlert : Entity
{
    public Guid TripExecutionId { get; set; }
    public Guid? HorseId { get; set; }
    public string AlertType { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string Message { get; set; } = null!;
    public int? MetricValue { get; set; }
    public int? MetricThreshold { get; set; }
    public string? Recommendation { get; set; }
    public bool Acknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedByUserId { get; set; }
    public DateTime AlertedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class HorseHealthStatus : Entity
{
    public Guid TripExecutionId { get; set; }
    public Guid? HorseId { get; set; }
    public string CurrentStatus { get; set; } = HealthConstants.HealthStatus.Healthy;
    public int? CurrentStressLevel { get; set; }
    public int? AlertCount { get; set; }
    public int? AcknowledgedAlertCount { get; set; }
    public DateTime? LastHealthCheckAt { get; set; }
    public DateTime? LastAlertAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
