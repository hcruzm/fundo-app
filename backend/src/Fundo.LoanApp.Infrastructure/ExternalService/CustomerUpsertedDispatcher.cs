using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Domain.Events;

namespace Fundo.LoanApp.Infrastructure.ExternalService;

/// <summary>
/// Turns a delivered outbox event into the external service call. It re-reads from the database
/// rather than trusting data carried on the event, so it always sends the committed state.
/// </summary>
public sealed class CustomerUpsertedDispatcher(
    ICustomerRepository customers,
    ExternalServiceClient client)
{
    public async Task DispatchAsync(CustomerUpsertedEvent evt, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(evt.CustomerId, ct)
            ?? throw new InvalidOperationException($"Customer {evt.CustomerId} was not found.");
        var application = customer.Application;

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
            new ApplicationPayload(application.Id, application.RequestedAmount));

        await client.UpsertAsync(payload, ct);
    }
}
