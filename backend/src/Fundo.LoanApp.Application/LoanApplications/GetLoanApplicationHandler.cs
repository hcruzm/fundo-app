using Fundo.LoanApp.Domain.Applications;
using Fundo.LoanApp.Domain.Customers;

namespace Fundo.LoanApp.Application.LoanApplications;

public sealed class GetLoanApplicationHandler(
    ILoanApplicationRepository applications,
    ICustomerRepository customers)
{
    public async Task<LoanApplicationDetail?> HandleAsync(Guid applicationId, CancellationToken ct)
    {
        var application = await applications.GetByIdAsync(applicationId, ct);
        if (application is null)
        {
            return null;
        }

        var customer = await customers.GetByIdAsync(application.CustomerId, ct)
            ?? throw new InvalidOperationException($"Application {applicationId} references a missing customer.");

        return new LoanApplicationDetail(
            application.Id,
            customer.Id,
            application.RequestedAmount,
            application.Status.ToString(),
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
