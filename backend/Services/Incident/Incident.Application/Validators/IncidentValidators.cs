using FluentValidation;
using Incident.Application.DTOs;

namespace Incident.Application.Validators;

public class CreateIncidentRequestValidator : AbstractValidator<CreateIncidentRequest>
{
    private static readonly string[] AllowedTypes =
    [
        "VEHICLE_BREAKDOWN", "TRAFFIC_DELAY", "WEATHER",
        "HORSE_MEDICAL", "BORDER_ISSUE", "OTHER"
    ];

    private static readonly string[] AllowedSeverities =
    [
        "LOW", "MEDIUM", "HIGH", "CRITICAL"
    ];

    public CreateIncidentRequestValidator()
    {
        RuleFor(x => x.TripId)
            .NotEmpty().WithMessage("TripId is required.");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required.")
            .Must(t => AllowedTypes.Contains(t))
            .WithMessage($"Type must be one of: {string.Join(", ", AllowedTypes)}.");

        RuleFor(x => x.Severity)
            .NotEmpty().WithMessage("Severity is required.")
            .Must(s => AllowedSeverities.Contains(s))
            .WithMessage($"Severity must be one of: {string.Join(", ", AllowedSeverities)}.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(255).WithMessage("Title cannot exceed 255 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(4000).WithMessage("Description cannot exceed 4000 characters.");

        RuleFor(x => x.OccurredAt)
            .NotEmpty().WithMessage("OccurredAt timestamp is required.")
            .LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(5))
            .WithMessage("OccurredAt cannot be in the future.");

        When(x => x.Latitude.HasValue, () =>
        {
            RuleFor(x => x.Latitude!.Value)
                .InclusiveBetween(-90m, 90m).WithMessage("Latitude must be between -90 and 90.");
        });

        When(x => x.Longitude.HasValue, () =>
        {
            RuleFor(x => x.Longitude!.Value)
                .InclusiveBetween(-180m, 180m).WithMessage("Longitude must be between -180 and 180.");
        });
    }
}

public class UpdateIncidentStatusRequestValidator : AbstractValidator<UpdateIncidentStatusRequest>
{
    private static readonly string[] AllowedStatuses =
    [
        "OPEN", "INVESTIGATING", "RESOLVED", "CANCELLED", "CLOSED"
    ];

    public UpdateIncidentStatusRequestValidator()
    {
        RuleFor(x => x.NewStatus)
            .NotEmpty().WithMessage("NewStatus is required.")
            .Must(s => AllowedStatuses.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", AllowedStatuses)}.");

        RuleFor(x => x.Reason)
            .MaximumLength(2000).WithMessage("Reason cannot exceed 2000 characters.");
    }
}

public class ResolveIncidentRequestValidator : AbstractValidator<ResolveIncidentRequest>
{
    public ResolveIncidentRequestValidator()
    {
        RuleFor(x => x.ResolutionNote)
            .NotEmpty().WithMessage("ResolutionNote is required when resolving an incident.")
            .MaximumLength(4000).WithMessage("ResolutionNote cannot exceed 4000 characters.");
    }
}
