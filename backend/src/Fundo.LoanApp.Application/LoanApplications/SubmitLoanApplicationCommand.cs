namespace Fundo.LoanApp.Application.LoanApplications;

public sealed record SubmitLoanApplicationCommand(
    string FirstName,
    string LastName,
    string CompanyName,
    decimal RequestedAmount,
    string Ssn,
    AddressInput Address);

public sealed record AddressInput(string Street, string City, string State, string PostalCode);
