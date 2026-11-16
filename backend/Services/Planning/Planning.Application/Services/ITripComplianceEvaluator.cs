namespace Planning.Application.Services;

public sealed record TripComplianceEvaluation(bool IsReady, IReadOnlyList<string> Blockers);

public interface ITripComplianceEvaluator
{
    Task<TripComplianceEvaluation> EvaluateAsync(Guid tripId, CancellationToken cancellationToken = default);
}
