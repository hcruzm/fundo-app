namespace Fundo.LoanApp.Infrastructure.Persistence;

/// <summary>
/// A persistence-only row. The blacklist has no behaviour, so it stays out of the Domain
/// project and is reached through <see cref="Domain.Decisions.IBlacklistedSsnRepository"/>.
/// </summary>
public sealed record BlacklistedSsn(string SsnHash, string Note);
