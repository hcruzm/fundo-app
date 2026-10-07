namespace Fundo.LoanApp.Application.LoanApplications;

public sealed record LoanApplicationDetail(
    Guid ApplicationId,
    Guid CustomerId,
    decimal RequestedAmount,
    string FirstName,
    string LastName,
    string CompanyName,
    string MaskedSsn,
    AddressDto Address,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
