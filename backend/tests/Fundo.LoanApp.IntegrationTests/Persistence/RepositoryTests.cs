using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Infrastructure.Persistence;
using Fundo.LoanApp.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Fundo.LoanApp.IntegrationTests.Persistence;

public class RepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Address AnyAddress = new("1 Byron Street", "Austin", "TX", "78701");

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task FindBySsnHashAsync_returns_null_when_the_customer_is_unknown()
    {
        await using var db = fixture.CreateDbContext();
        var repository = new CustomerRepository(db);

        Assert.Null(await repository.FindBySsnHashAsync(new SsnHash("unknown"), CancellationToken.None));
    }

    [Fact]
    public async Task A_saved_customer_round_trips_with_its_owned_address()
    {
        await using (var writeDb = fixture.CreateDbContext())
        {
            new CustomerRepository(writeDb).Add(
                Customer.Create(new SsnHash("hash-1"), "6789", "Ada", "Lovelace", "Analytical Engines LLC", AnyAddress, 25_000m, Now));
            await writeDb.SaveChangesAsync();
        }

        await using var readDb = fixture.CreateDbContext();
        var found = await new CustomerRepository(readDb).FindBySsnHashAsync(new SsnHash("hash-1"), CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal("Ada", found.FirstName);
        Assert.Equal(AnyAddress, found.Address);
        Assert.Equal("6789", found.SsnLast4);
        Assert.Equal(25_000m, found.Application.RequestedAmount);
    }

    [Fact]
    public async Task The_ssn_hash_index_rejects_a_duplicate_customer()
    {
        await using var db = fixture.CreateDbContext();
        var repository = new CustomerRepository(db);

        repository.Add(Customer.Create(new SsnHash("hash-2"), "6789", "Ada", "Lovelace", "Engines LLC", AnyAddress, 25_000m, Now));
        await db.SaveChangesAsync();

        // A distinct Address instance: the shared static reference would make EF Core's
        // change tracker treat the owned Address as already attached to the first customer.
        repository.Add(Customer.Create(new SsnHash("hash-2"), "6789", "Augusta", "Byron", "Engines LLC", AnyAddress with { }, 25_000m, Now));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task FindByApplicationIdAsync_returns_the_customer_that_owns_the_application()
    {
        var customer = Customer.Create(new SsnHash("hash-3"), "6789", "Ada", "Lovelace", "Engines LLC", AnyAddress, 25_000m, Now);

        await using (var writeDb = fixture.CreateDbContext())
        {
            new CustomerRepository(writeDb).Add(customer);
            await writeDb.SaveChangesAsync();
        }

        await using var readDb = fixture.CreateDbContext();
        var found = await new CustomerRepository(readDb).FindByApplicationIdAsync(customer.Application.Id, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(customer.Id, found.Id);
        Assert.Equal(25_000m, found.Application.RequestedAmount);
        Assert.Equal(1, await readDb.Applications.CountAsync());
    }

    [Fact]
    public async Task ContainsAsync_matches_a_seeded_blacklist_entry()
    {
        await using (var writeDb = fixture.CreateDbContext())
        {
            writeDb.BlacklistedSsns.Add(new BlacklistedSsn("blocked-hash", "test fixture"));
            await writeDb.SaveChangesAsync();
        }

        await using var readDb = fixture.CreateDbContext();
        var repository = new BlacklistedSsnRepository(readDb);

        Assert.True(await repository.ContainsAsync(new SsnHash("blocked-hash"), CancellationToken.None));
        Assert.False(await repository.ContainsAsync(new SsnHash("other-hash"), CancellationToken.None));
    }
}
