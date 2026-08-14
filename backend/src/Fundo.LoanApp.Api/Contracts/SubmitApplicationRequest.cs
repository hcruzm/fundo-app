using Fundo.LoanApp.Application.LoanApplications;

namespace Fundo.LoanApp.Api.Contracts;

public sealed record SubmitApplicationRequest(
    string FirstName,
    string LastName,
    string CompanyName,
    decimal RequestedAmount,
    string Ssn,
    AddressRequest Address)
{
    public SubmitLoanApplicationCommand ToCommand() => new(
        FirstName.Trim(),
        LastName.Trim(),
        CompanyName.Trim(),
        RequestedAmount,
        Ssn,
        new AddressInput(
            Address.Street.Trim(),
            Address.City.Trim(),
            Address.State.Trim().ToUpperInvariant(),
            Address.PostalCode.Trim()));
}

public sealed record AddressRequest(string Street, string City, string State, string PostalCode);
