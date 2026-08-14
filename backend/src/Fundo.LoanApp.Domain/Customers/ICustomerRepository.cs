namespace Fundo.LoanApp.Domain.Customers;

public interface ICustomerRepository
{
    Task<Customer?> FindBySsnHashAsync(SsnHash ssnHash, CancellationToken ct);

    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct);

    void Add(Customer customer);
}
