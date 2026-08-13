using Fundo.LoanApp.Domain.Customers;

namespace Fundo.LoanApp.Domain.Decisions;

public interface IBlacklistedSsnRepository
{
    Task<bool> ContainsAsync(SsnHash ssnHash, CancellationToken ct);
}
