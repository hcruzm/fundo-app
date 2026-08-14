using FluentValidation;
using Fundo.LoanApp.Api.Contracts;

namespace Fundo.LoanApp.Api.Validation;

public sealed class SubmitApplicationRequestValidator : AbstractValidator<SubmitApplicationRequest>
{
    public SubmitApplicationRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.LastName).NotEmpty().MaximumLength(100);
        RuleFor(r => r.CompanyName).NotEmpty().MaximumLength(200);

        RuleFor(r => r.RequestedAmount)
            .GreaterThan(0m).WithMessage("The requested amount must be greater than zero.")
            .LessThanOrEqualTo(10_000_000m);

        RuleFor(r => r.Ssn)
            .NotEmpty()
            .Must(ssn => ssn.Count(char.IsAsciiDigit) == 9)
            .WithMessage("An SSN must contain exactly nine digits.");

        RuleFor(r => r.Address).NotNull();

        When(r => r.Address is not null, () =>
        {
            RuleFor(r => r.Address.Street).NotEmpty().MaximumLength(200);
            RuleFor(r => r.Address.City).NotEmpty().MaximumLength(100);
            RuleFor(r => r.Address.State).NotEmpty().Length(2).WithMessage("Use the two-letter state code.");
            RuleFor(r => r.Address.PostalCode).NotEmpty().MaximumLength(10);
        });
    }
}
