namespace Fundo.LoanApp.Domain.Applications;

public interface ILoanApplicationRepository
{
    Task<LoanApplication?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<LoanApplication?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct);

    Task<IReadOnlyList<LoanApplication>> GetAllAsync(CancellationToken ct);

    void Add(LoanApplication application);
}
