namespace Fundo.LoanApp.Domain.Customers;

public interface ICustomerRepository
{
    Task<Customer?> FindBySsnHashAsync(SsnHash ssnHash, CancellationToken ct);

    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);

    void Add(Customer customer);
}
