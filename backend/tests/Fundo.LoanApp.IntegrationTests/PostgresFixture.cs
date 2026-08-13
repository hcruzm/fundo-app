using Fundo.LoanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Fundo.LoanApp.IntegrationTests;

/// <summary>
/// Starts a real PostgreSQL container per test class. Real transactions are a
/// requirement of this project, so the in-memory provider is never used.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await container.DisposeAsync();

    public LoanAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LoanAppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new LoanAppDbContext(options);
    }

    public async Task ResetAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE applications, customers, blacklisted_ssns RESTART IDENTITY CASCADE;");
    }
}
