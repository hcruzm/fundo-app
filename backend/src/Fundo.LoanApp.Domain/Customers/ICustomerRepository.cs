namespace Fundo.LoanApp.Domain.Customers;

/// <summary>Loads and stores the customer aggregate, always together with its application.</summary>
public interface ICustomerRepository
{
    Task<Customer?> FindBySsnHashAsync(SsnHash ssnHash, CancellationToken ct);

    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<Customer?> FindByApplicationIdAsync(Guid applicationId, CancellationToken ct);

    void Add(Customer customer);
}
