using Fundo.LoanApp.Domain.Customers;

namespace Fundo.LoanApp.Domain.Decisions;

/// <summary>
/// The data a rule is allowed to see. It carries the hashed SSN, never the plaintext.
/// </summary>
public sealed record LoanApplicationCandidate(
    string FirstName,
    string LastName,
    Address Address,
    string CompanyName,
    decimal RequestedAmount,
    SsnHash SsnHash);
