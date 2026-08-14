using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fundo.LoanApp.Infrastructure.Persistence;

/// <summary>
/// Applies migrations and seeds the blacklist at startup so a reviewer only has to run
/// docker compose up and dotnet run.
/// </summary>
public sealed class DatabaseInitializer(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoanAppDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<BlacklistSeeder>();

        await db.Database.MigrateAsync(ct);
        await seeder.SeedAsync(ct);

        logger.LogInformation("Database migrated and seeded.");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
