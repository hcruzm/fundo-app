using Fundo.LoanApp.Domain.Applications;
using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Domain.Events;

namespace Fundo.LoanApp.Infrastructure.ExternalService;

/// <summary>
/// Turns a committed event into the external service call. It re-reads from the database
/// rather than trusting data carried on the event, so it always sends the committed state.
/// </summary>
public sealed class CustomerUpsertedDispatcher(
    ICustomerRepository customers,
    ILoanApplicationRepository applications,
    ExternalServiceClient client)
{
    public async Task DispatchAsync(CustomerUpsertedEvent evt, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(evt.CustomerId, ct)
            ?? throw new InvalidOperationException($"Customer {evt.CustomerId} was not found.");

        var application = await applications.GetByIdAsync(evt.ApplicationId, ct)
            ?? throw new InvalidOperationException($"Application {evt.ApplicationId} was not found.");

        var payload = new CustomerPayload(
            customer.SsnHash.Value,
            customer.SsnLast4,
            customer.FirstName,
            customer.LastName,
            customer.CompanyName,
            new AddressPayload(
                customer.Address.Street,
                customer.Address.City,
                customer.Address.State,
                customer.Address.PostalCode),
            new ApplicationPayload(application.Id, application.RequestedAmount, application.Status.ToString()));

        if (evt.IsUpdate)
        {
            await client.UpdateAsync(payload, ct);
        }
        else
        {
            await client.CreateAsync(payload, ct);
        }
    }
}
