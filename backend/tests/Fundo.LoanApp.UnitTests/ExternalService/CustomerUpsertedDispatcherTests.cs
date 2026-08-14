using System.Text.Json;
using Fundo.LoanApp.Domain.Applications;
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

        public void Add(Customer customer) => throw new NotSupportedException();
    }

    private sealed class FakeApplicationRepository(LoanApplication application) : ILoanApplicationRepository
    {
        public Task<LoanApplication?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == application.Id ? application : null);

        public Task<LoanApplication?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct) =>
            Task.FromResult(customerId == application.CustomerId ? application : null);

        public void Add(LoanApplication application) => throw new NotSupportedException();
    }

    private static (Customer customer, LoanApplication application) SampleRecords()
    {
        var customer = Customer.Create(
            new SsnHash("a1b2c3d4e5f60789fedcba9876543210"), "6789", "Ada", "Lovelace", "Analytical Engines LLC",
            new Address("1 Byron Street", "Austin", "TX", "78701"), Now);
        var application = LoanApplication.Create(customer.Id, 25_000m, Now);
        return (customer, application);
    }

    private static CustomerUpsertedDispatcher CreateDispatcher(
        Customer customer, LoanApplication application, RecordingHandler handler, out ExternalServiceClient client)
    {
        client = new ExternalServiceClient(new HttpClient(handler) { BaseAddress = new Uri("http://external.test") });
        return new CustomerUpsertedDispatcher(
            new FakeCustomerRepository(customer),
            new FakeApplicationRepository(application),
            client);
    }

    [Fact]
    public async Task A_new_customer_event_posts_to_the_collection_route()
    {
        var (customer, application) = SampleRecords();
        var handler = new RecordingHandler();
        var dispatcher = CreateDispatcher(customer, application, handler, out _);
        var evt = new CustomerUpsertedEvent(customer.Id, application.Id, IsUpdate: false);

        await dispatcher.DispatchAsync(evt, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/customers", request.RequestUri?.AbsolutePath);
        await AssertPayloadIsSafe(request);
    }

    [Fact]
    public async Task A_returning_customer_event_puts_to_the_item_route_keyed_by_the_ssn_hash()
    {
        var (customer, application) = SampleRecords();
        var handler = new RecordingHandler();
        var dispatcher = CreateDispatcher(customer, application, handler, out _);
        var evt = new CustomerUpsertedEvent(customer.Id, application.Id, IsUpdate: true);

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
