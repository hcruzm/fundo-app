using Fundo.LoanApp.Domain.Customers;

namespace Fundo.LoanApp.Application.LoanApplications;

public sealed class GetLoanApplicationHandler(ICustomerRepository customers)
{
    public async Task<LoanApplicationDetail?> HandleAsync(Guid applicationId, CancellationToken ct)
    {
        var customer = await customers.FindByApplicationIdAsync(applicationId, ct);
        if (customer is null)
        {
            return null;
        }

        var application = customer.Application;

        return new LoanApplicationDetail(
            application.Id,
            customer.Id,
            application.RequestedAmount,
            customer.FirstName,
            customer.LastName,
            customer.CompanyName,
            $"•••-••-{customer.SsnLast4}",
            new AddressDto(
                customer.Address.Street,
                customer.Address.City,
                customer.Address.State,
                customer.Address.PostalCode),
            application.CreatedAt,
            application.UpdatedAt);
    }
}
