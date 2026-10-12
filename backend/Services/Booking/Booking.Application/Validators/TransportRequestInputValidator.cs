using Booking.Application.TransportRequests;
using FluentValidation;

namespace Booking.Application.Validators;

public class TransportRequestInputValidator : AbstractValidator<TransportRequestInput>
{
    public TransportRequestInputValidator()
    {
        RuleFor(x => x.OriginLocationId)
            .NotEqual(Guid.Empty).WithMessage("Origin location is required.");

        RuleFor(x => x.DestinationLocationId)
            .NotEqual(Guid.Empty).WithMessage("Destination location is required.");

        RuleFor(x => x.RequestedDepartureAt)
            .NotEmpty().WithMessage("Requested departure date/time is required.");

        RuleFor(x => x.PreferredTransportMode)
            .MaximumLength(30).WithMessage("Preferred transport mode must not exceed 30 characters.")
            .When(x => !string.IsNullOrEmpty(x.PreferredTransportMode));

        RuleFor(x => x.SpecialRequirements)
            .MaximumLength(2000).WithMessage("Special requirements must not exceed 2000 characters.")
            .When(x => !string.IsNullOrEmpty(x.SpecialRequirements));

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Notes));

        RuleFor(x => x.HorseIds)
            .NotEmpty().WithMessage("At least one horse must be specified.")
            .When(x => x.HorseIds != null);
    }
}
