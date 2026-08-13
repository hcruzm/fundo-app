using Fundo.LoanApp.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Fundo.LoanApp.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository(LoanAppDbContext db) : ICustomerRepository
{
    public Task<Customer?> FindBySsnHashAsync(SsnHash ssnHash, CancellationToken ct) =>
        db.Customers.SingleOrDefaultAsync(c => c.SsnHash == ssnHash, ct);

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct);

    public void Add(Customer customer) => db.Customers.Add(customer);
}
