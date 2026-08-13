using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Domain.Decisions;
using Microsoft.EntityFrameworkCore;

namespace Fundo.LoanApp.Infrastructure.Persistence.Repositories;

public sealed class BlacklistedSsnRepository(LoanAppDbContext db) : IBlacklistedSsnRepository
{
    public Task<bool> ContainsAsync(SsnHash ssnHash, CancellationToken ct) =>
        db.BlacklistedSsns.AnyAsync(b => b.SsnHash == ssnHash.Value, ct);
}
