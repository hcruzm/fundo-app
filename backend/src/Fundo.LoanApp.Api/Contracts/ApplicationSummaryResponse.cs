namespace Fundo.LoanApp.Api.Contracts;

public sealed record ApplicationSummaryResponse(
    Guid ApplicationId,
    Guid CustomerId,
    string FirstName,
    string LastName,
    string CompanyName,
    decimal RequestedAmount,
    string MaskedSsn,
    string City,
    string State,
    bool IsReturningCustomer,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
