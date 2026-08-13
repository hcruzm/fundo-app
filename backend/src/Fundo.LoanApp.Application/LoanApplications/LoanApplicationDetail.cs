namespace Fundo.LoanApp.Application.LoanApplications;

public sealed record LoanApplicationDetail(
    Guid ApplicationId,
    Guid CustomerId,
    decimal RequestedAmount,
    string Status,
    string FirstName,
    string LastName,
    string CompanyName,
    string MaskedSsn,
    AddressInput Address,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
