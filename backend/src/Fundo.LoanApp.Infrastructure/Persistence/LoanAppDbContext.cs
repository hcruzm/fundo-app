using Fundo.LoanApp.Domain.Applications;
using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Fundo.LoanApp.Infrastructure.Persistence;

public sealed class LoanAppDbContext(DbContextOptions<LoanAppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LoanApplication> Applications => Set<LoanApplication>();
    public DbSet<BlacklistedSsn> BlacklistedSsns => Set<BlacklistedSsn>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoanAppDbContext).Assembly);
    }
}
