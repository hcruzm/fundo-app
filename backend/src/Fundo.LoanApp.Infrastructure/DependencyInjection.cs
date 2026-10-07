using Fundo.LoanApp.Application.Abstractions;
using Fundo.LoanApp.Application.LoanApplications;
using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Domain.Decisions;
using Fundo.LoanApp.Domain.Decisions.Rules;
using Fundo.LoanApp.Infrastructure.ExternalService;
using Fundo.LoanApp.Infrastructure.Messaging;
using Fundo.LoanApp.Infrastructure.Persistence;
using Fundo.LoanApp.Infrastructure.Persistence.Repositories;
using Fundo.LoanApp.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fundo.LoanApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LoanAppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Database")));

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IBlacklistedSsnRepository, BlacklistedSsnRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<BlacklistSeeder>();

        services.AddSingleton<ISsnHasher>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            return new HmacSsnHasher(
                configuration["Security:SsnHashKey"]
                ?? throw new InvalidOperationException("Security:SsnHashKey is not configured."));
        });

        var restrictedStates = configuration.GetSection("Decision:RestrictedStates").Get<string[]>() ?? ["NY"];
        services.AddSingleton(new RestrictedStates(
            new HashSet<string>(restrictedStates, StringComparer.OrdinalIgnoreCase)));

        // Registration order is evaluation order: the in-memory check runs before the query.
        services.AddScoped<IDenialRule, RestrictedStateRule>();
        services.AddScoped<IDenialRule, BlacklistedSsnRule>();
        services.AddScoped<DecisionEngine>();

        services.AddScoped<SubmitLoanApplicationHandler>();
        services.AddScoped<GetLoanApplicationHandler>();

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IEventPublisher, OutboxEventPublisher>();
        services.AddHostedService<DatabaseInitializer>();

        services.AddScoped<CustomerUpsertedDispatcher>();

        services.AddHttpClient<ExternalServiceClient>((provider, client) =>
            {
                var configuration = provider.GetRequiredService<IConfiguration>();
                client.BaseAddress = new Uri(
                    configuration["ExternalService:BaseUrl"]
                    ?? throw new InvalidOperationException("ExternalService:BaseUrl is not configured."));
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddStandardResilienceHandler();

        services.AddHostedService<OutboxProcessor>();

        return services;
    }
}
