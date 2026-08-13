using Fundo.LoanApp.Domain.Applications;
using Fundo.LoanApp.Domain.Customers;
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
    public async Task A_committed_transaction_persists_both_records()
    {
        await using var db = fixture.CreateDbContext();
        var unitOfWork = new EfUnitOfWork(db);

        await unitOfWork.ExecuteInTransactionAsync(_ =>
        {
            var customer = Customer.Create(new SsnHash("hash-ok"), "6789", "Ada", "Lovelace", "Engines LLC", AnyAddress, Now);
            db.Customers.Add(customer);
            db.Applications.Add(LoanApplication.Create(customer.Id, 25_000m, Now));
            return Task.FromResult(0);
        }, CancellationToken.None);

        await using var verifyDb = fixture.CreateDbContext();
        Assert.Equal(1, await verifyDb.Customers.CountAsync());
        Assert.Equal(1, await verifyDb.Applications.CountAsync());
    }

    [Fact]
    public async Task A_failure_after_a_partial_write_rolls_the_whole_transaction_back()
    {
        await using var db = fixture.CreateDbContext();
        var unitOfWork = new EfUnitOfWork(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unitOfWork.ExecuteInTransactionAsync<int>(async ct =>
            {
                var customer = Customer.Create(new SsnHash("hash-rollback"), "6789", "Ada", "Lovelace", "Engines LLC", AnyAddress, Now);
                db.Customers.Add(customer);

                // The row exists inside the transaction at this point.
                await db.SaveChangesAsync(ct);
                Assert.Equal(1, await db.Customers.CountAsync(ct));

                throw new InvalidOperationException("simulated failure while writing the application");
            }, CancellationToken.None));

        // A second connection proves the row never became visible outside the transaction.
        await using var verifyDb = fixture.CreateDbContext();
        Assert.Equal(0, await verifyDb.Customers.CountAsync());
        Assert.Equal(0, await verifyDb.Applications.CountAsync());
    }
}
