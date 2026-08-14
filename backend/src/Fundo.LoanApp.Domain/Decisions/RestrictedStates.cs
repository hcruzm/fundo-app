namespace Fundo.LoanApp.Domain.Decisions;

/// <summary>
/// The two-letter state codes that are denied. Bound from configuration in
/// Fundo.LoanApp.Infrastructure's composition root so the Domain project stays free of
/// Microsoft.Extensions.Options.
/// </summary>
public sealed record RestrictedStates(IReadOnlySet<string> Codes);
