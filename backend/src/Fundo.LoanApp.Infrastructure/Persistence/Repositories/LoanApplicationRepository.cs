using Fundo.LoanApp.Domain.Applications;
using Microsoft.EntityFrameworkCore;

namespace Fundo.LoanApp.Infrastructure.Persistence.Repositories;

public sealed class LoanApplicationRepository(LoanAppDbContext db) : ILoanApplicationRepository
{
    public Task<LoanApplication?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Applications.SingleOrDefaultAsync(a => a.Id == id, ct);

    public Task<LoanApplication?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct) =>
        db.Applications.SingleOrDefaultAsync(a => a.CustomerId == customerId, ct);

    public void Add(LoanApplication application) => db.Applications.Add(application);
}
