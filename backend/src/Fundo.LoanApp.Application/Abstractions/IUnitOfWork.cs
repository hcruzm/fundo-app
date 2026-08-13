namespace Fundo.LoanApp.Application.Abstractions;

/// <summary>
/// Owns the transaction boundary. Repositories never commit; the use case wraps its
/// writes in one call so a partial failure rolls everything back.
/// </summary>
public interface IUnitOfWork
{
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct);
}
