using Fundo.LoanApp.Application.Abstractions;

namespace Fundo.LoanApp.Infrastructure.Persistence;

public sealed class EfUnitOfWork(LoanAppDbContext db) : IUnitOfWork
{
    /// <summary>
    /// Runs <paramref name="operation"/> inside a database transaction, saves the tracked
    /// changes, and commits. On failure the transaction is rolled back automatically when
    /// <c>await using</c> disposes it, so no explicit catch/rollback is needed here — and one
    /// would risk swallowing the original exception if <paramref name="ct"/> is already
    /// cancelled.
    /// </summary>
    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var result = await operation(ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
