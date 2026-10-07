using System.Net;
using Fundo.LoanApp.Domain.Applications;
using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Domain.Events;
using Fundo.LoanApp.Infrastructure.ExternalService;
using Fundo.LoanApp.Infrastructure.Messaging;
using Fundo.LoanApp.Infrastructure.Persistence;
using Fundo.LoanApp.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fundo.LoanApp.IntegrationTests.Messaging;

public class OutboxProcessorTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_delivered_message_is_sent_once_and_marked_processed()
    {
        await SeedApprovedApplicationWithPendingEventAsync();
        var externalService = new StubExternalService(HttpStatusCode.OK);
        var processor = CreateProcessor(externalService);

        await processor.ProcessPendingAsync(CancellationToken.None);
        await processor.ProcessPendingAsync(CancellationToken.None);

        var request = Assert.Single(externalService.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);

        await using var db = fixture.CreateDbContext();
        var message = await db.OutboxMessages.SingleAsync();
        Assert.NotNull(message.ProcessedAt);
    }

    [Fact]
    public async Task A_failed_delivery_leaves_the_message_pending_for_the_next_poll()
    {
        await SeedApprovedApplicationWithPendingEventAsync();
        var processor = CreateProcessor(new StubExternalService(HttpStatusCode.ServiceUnavailable));

        await processor.ProcessPendingAsync(CancellationToken.None);

        await using var db = fixture.CreateDbContext();
        var message = await db.OutboxMessages.SingleAsync();
        Assert.Null(message.ProcessedAt);
    }

    private async Task SeedApprovedApplicationWithPendingEventAsync()
    {
        await using var db = fixture.CreateDbContext();
        var unitOfWork = new EfUnitOfWork(db);
        var publisher = new OutboxEventPublisher(db, TimeProvider.System);

        await unitOfWork.ExecuteInTransactionAsync(_ =>
        {
            var customer = Customer.Create(
                new SsnHash("hash-outbox"), "6789", "Ada", "Lovelace", "Engines LLC",
                new Address("1 Byron Street", "Austin", "TX", "78701"), Now);
            var application = LoanApplication.Create(customer.Id, 25_000m, Now);
            db.Customers.Add(customer);
            db.Applications.Add(application);
            publisher.Publish(new CustomerUpsertedEvent(customer.Id, application.Id));
            return Task.FromResult(0);
        }, CancellationToken.None);
    }

    // Wired by hand rather than through AddInfrastructure so the external service is a stub
    // and the resilience pipeline's retries do not slow the failure case down.
    private OutboxProcessor CreateProcessor(StubExternalService externalService)
    {
        var services = new ServiceCollection();
        services.AddDbContext<LoanAppDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ILoanApplicationRepository, LoanApplicationRepository>();
        services.AddScoped<CustomerUpsertedDispatcher>();
        services.AddScoped(_ => new ExternalServiceClient(
            new HttpClient(externalService) { BaseAddress = new Uri("http://external.test") }));

        return new OutboxProcessor(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            NullLogger<OutboxProcessor>.Instance);
    }

    private sealed class StubExternalService(HttpStatusCode status) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
