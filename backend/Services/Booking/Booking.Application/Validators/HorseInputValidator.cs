using Booking.Application.Customers;
using FluentValidation;

namespace Booking.Application.Validators;

public class HorseInputValidator : AbstractValidator<HorseInput>
{
    public HorseInputValidator()
    {
        RuleFor(x => x.HorseName)
            .NotEmpty().WithMessage("Horse name is required.")
            .MaximumLength(200).WithMessage("Horse name must not exceed 200 characters.");

        RuleFor(x => x.DateOfBirth)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth cannot be in the future.")
            .When(x => x.DateOfBirth.HasValue);

        RuleFor(x => x.Sex)
            .Must(x => x is null or "STALLION" or "MARE" or "GELDING")
            .WithMessage("Horse sex must be one of: STALLION, MARE, GELDING.")
            .When(x => !string.IsNullOrEmpty(x.Sex));

        RuleFor(x => x.RegistrationNumber)
            .MaximumLength(100).WithMessage("Registration number must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.RegistrationNumber));

        RuleFor(x => x.PassportNumber)
            .MaximumLength(100).WithMessage("Passport number must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.PassportNumber));

        RuleFor(x => x.Breed)
            .MaximumLength(100).WithMessage("Breed must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Breed));

        RuleFor(x => x.Color)
            .MaximumLength(100).WithMessage("Color must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Color));

        RuleFor(x => x.SpecialRequirements)
            .MaximumLength(1000).WithMessage("Special requirements must not exceed 1000 characters.")
            .When(x => !string.IsNullOrEmpty(x.SpecialRequirements));
    }
}
