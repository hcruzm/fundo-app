using Fundo.LoanApp.Domain.Applications;
using Fundo.LoanApp.Domain.Customers;

namespace Fundo.LoanApp.Application.LoanApplications;

public sealed class ListLoanApplicationsHandler(
    ILoanApplicationRepository applications,
    ICustomerRepository customers)
{
    public async Task<IReadOnlyList<LoanApplicationSummary>> HandleAsync(CancellationToken ct)
    {
        var allApplications = await applications.GetAllAsync(ct);
        var allCustomers = await customers.GetAllAsync(ct);
        var customersById = allCustomers.ToDictionary(c => c.Id);

        return [.. allApplications
            .Select(application =>
            {
                var customer = customersById.TryGetValue(application.CustomerId, out var found)
                    ? found
                    : throw new InvalidOperationException(
                        $"Application {application.Id} references a missing customer {application.CustomerId}.");

                return new LoanApplicationSummary(
                    application.Id,
                    customer.Id,
                    customer.FirstName,
                    customer.LastName,
                    customer.CompanyName,
                    application.RequestedAmount,
                    $"•••-••-{customer.SsnLast4}",
                    customer.Address.City,
                    customer.Address.State,
                    application.UpdatedAt > application.CreatedAt,
                    application.CreatedAt,
                    application.UpdatedAt);
            })
            .OrderByDescending(summary => summary.UpdatedAt)];
    }
}
