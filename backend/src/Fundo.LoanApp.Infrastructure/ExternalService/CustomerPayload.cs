namespace Fundo.LoanApp.Infrastructure.ExternalService;

/// <summary>The wire contract between this system and the external service.</summary>
public sealed record CustomerPayload(
    string SsnHash,
    string SsnLast4,
    string FirstName,
    string LastName,
    string CompanyName,
    AddressPayload Address,
    ApplicationPayload Application);

public sealed record AddressPayload(string Street, string City, string State, string PostalCode);

public sealed record ApplicationPayload(Guid Id, decimal RequestedAmount, string Status);
