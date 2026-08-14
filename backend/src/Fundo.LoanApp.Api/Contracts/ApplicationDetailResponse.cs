namespace Fundo.LoanApp.Api.Contracts;

public sealed record ApplicationDetailResponse(
    Guid ApplicationId,
    Guid CustomerId,
    decimal RequestedAmount,
    string Status,
    string FirstName,
    string LastName,
    string CompanyName,
    string MaskedSsn,
    AddressResponse Address,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AddressResponse(string Street, string City, string State, string PostalCode);
