namespace Fundo.LoanApp.Application.LoanApplications;

public sealed record SubmitLoanApplicationCommand(
    string FirstName,
    string LastName,
    string CompanyName,
    decimal RequestedAmount,
    string Ssn,
    AddressDto Address);

public sealed record AddressDto(string Street, string City, string State, string PostalCode);
