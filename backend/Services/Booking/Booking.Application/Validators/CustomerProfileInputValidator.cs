using Booking.Application.Customers;
using FluentValidation;

namespace Booking.Application.Validators;

public class CustomerProfileInputValidator : AbstractValidator<CustomerProfileInput>
{
    public CustomerProfileInputValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Customer name is required.")
            .MaximumLength(255).WithMessage("Customer name must not exceed 255 characters.");

        RuleFor(x => x.CustomerType)
            .NotEmpty().WithMessage("Customer type is required.")
            .Must(x => x is "CLUB" or "INDIVIDUAL_OWNER" or "BREEDER" or "AGENT")
            .WithMessage("Customer type must be one of: CLUB, INDIVIDUAL_OWNER, BREEDER, AGENT.");

        RuleFor(x => x.ContactPerson)
            .MaximumLength(200).WithMessage("Contact person must not exceed 200 characters.")
            .When(x => !string.IsNullOrEmpty(x.ContactPerson));

        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("Phone must not exceed 50 characters.")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Address must not exceed 500 characters.")
            .When(x => !string.IsNullOrEmpty(x.Address));
    }
}
