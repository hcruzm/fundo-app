using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Domain.Events;
using Fundo.LoanApp.Infrastructure.Messaging;
using Fundo.LoanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fundo.LoanApp.IntegrationTests.Persistence;

public class TransactionTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Address AnyAddress = new("1 Byron Street", "Austin", "TX", "78701");

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_committed_transaction_persists_both_records_and_the_event()
    {
        await using var db = fixture.CreateDbContext();
        var unitOfWork = new EfUnitOfWork(db);
        var publisher = new OutboxEventPublisher(db, TimeProvider.System);

        await unitOfWork.ExecuteInTransactionAsync(_ =>
        {
            var customer = Customer.Create(new SsnHash("hash-ok"), "6789", "Ada", "Lovelace", "Engines LLC", AnyAddress, 25_000m, Now);
            db.Customers.Add(customer);
            publisher.Publish(new CustomerUpsertedEvent(customer.Id, customer.Application.Id));
            return Task.FromResult(0);
        }, CancellationToken.None);

        await using var verifyDb = fixture.CreateDbContext();
        Assert.Equal(1, await verifyDb.Customers.CountAsync());
        Assert.Equal(1, await verifyDb.Applications.CountAsync());
        Assert.Equal(1, await verifyDb.OutboxMessages.CountAsync(m => m.ProcessedAt == null));
    }

    [Fact]
    public async Task A_failure_after_a_partial_write_rolls_the_whole_transaction_back()
    {
        await using var db = fixture.CreateDbContext();
        var unitOfWork = new EfUnitOfWork(db);
        var publisher = new OutboxEventPublisher(db, TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteInTransactionAsync<int>(async ct =>
            {
                var customer = Customer.Create(new SsnHash("hash-rollback"), "6789", "Ada", "Lovelace", "Engines LLC", AnyAddress, 25_000m, Now);
                db.Customers.Add(customer);
                publisher.Publish(new CustomerUpsertedEvent(customer.Id, customer.Application.Id));

                // The rows exist inside the transaction at this point.
                await db.SaveChangesAsync(ct);
                Assert.Equal(1, await db.Customers.CountAsync(ct));
                Assert.Equal(1, await db.Applications.CountAsync(ct));
                Assert.Equal(1, await db.OutboxMessages.CountAsync(ct));

                throw new InvalidOperationException("simulated failure after the writes");
            }, CancellationToken.None));

        // A second connection proves the rows never became visible outside the transaction:
        // no partial customer, no orphan application, and no event to deliver.
        await using var verifyDb = fixture.CreateDbContext();
        Assert.Equal(0, await verifyDb.Customers.CountAsync());
        Assert.Equal(0, await verifyDb.Applications.CountAsync());
        Assert.Equal(0, await verifyDb.OutboxMessages.CountAsync());
    }
}
