using System.Text.Json;
using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Domain.Events;
using Fundo.LoanApp.Infrastructure.ExternalService;

namespace Fundo.LoanApp.UnitTests.ExternalService;

public class CustomerUpsertedDispatcherTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeCustomerRepository(Customer customer) : ICustomerRepository
    {
        public Task<Customer?> FindBySsnHashAsync(SsnHash ssnHash, CancellationToken ct) =>
            Task.FromResult<Customer?>(customer);

        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == customer.Id ? customer : null);

        public Task<Customer?> FindByApplicationIdAsync(Guid applicationId, CancellationToken ct) =>
            Task.FromResult(applicationId == customer.Application.Id ? customer : null);

        public void Add(Customer customer) => throw new NotSupportedException();
    }

    private static Customer SampleCustomer() =>
        Customer.Create(
            new SsnHash("a1b2c3d4e5f60789fedcba9876543210"), "6789", "Ada", "Lovelace", "Analytical Engines LLC",
            new Address("1 Byron Street", "Austin", "TX", "78701"), 25_000m, Now);

    [Fact]
    public async Task An_event_puts_the_committed_records_to_the_item_route_keyed_by_the_ssn_hash()
    {
        var customer = SampleCustomer();
        var handler = new RecordingHandler();
        var client = new ExternalServiceClient(new HttpClient(handler) { BaseAddress = new Uri("http://external.test") });
        var dispatcher = new CustomerUpsertedDispatcher(new FakeCustomerRepository(customer), client);
        var evt = new CustomerUpsertedEvent(customer.Id, customer.Application.Id);

        await dispatcher.DispatchAsync(evt, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal($"/api/customers/{customer.SsnHash.Value}", request.RequestUri?.AbsolutePath);
        await AssertPayloadIsSafe(request);
    }

    private static async Task AssertPayloadIsSafe(HttpRequestMessage request)
    {
        var raw = await request.Content!.ReadAsStringAsync();

        using var document = JsonDocument.Parse(raw);
        Assert.Equal("6789", document.RootElement.GetProperty("ssnLast4").GetString());

        Assert.DoesNotContain("123-45-6789", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("123456789", raw, StringComparison.Ordinal);
    }
}
