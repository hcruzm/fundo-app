using System.Threading.Channels;
using Fundo.LoanApp.Application.Abstractions;
using Fundo.LoanApp.Application.LoanApplications;
using Fundo.LoanApp.Domain.Applications;
using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Domain.Decisions;
using Fundo.LoanApp.Domain.Decisions.Rules;
using Fundo.LoanApp.Domain.Events;
using Fundo.LoanApp.Infrastructure.ExternalService;
using Fundo.LoanApp.Infrastructure.Messaging;
using Fundo.LoanApp.Infrastructure.Persistence;
using Fundo.LoanApp.Infrastructure.Persistence.Repositories;
using Fundo.LoanApp.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fundo.LoanApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LoanAppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Database")));

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ILoanApplicationRepository, LoanApplicationRepository>();
        services.AddScoped<IBlacklistedSsnRepository, BlacklistedSsnRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<BlacklistSeeder>();

        var hashKey = configuration["Security:SsnHashKey"]
            ?? throw new InvalidOperationException("Security:SsnHashKey is not configured.");
        services.AddSingleton<ISsnHasher>(new HmacSsnHasher(hashKey));

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
        services.AddSingleton(Channel.CreateUnbounded<CustomerUpsertedEvent>());
        services.AddScoped<IEventPublisher, ChannelEventPublisher>();
        services.AddHostedService<DatabaseInitializer>();

        services.AddScoped<CustomerUpsertedDispatcher>();

        services.Configure<ExternalServiceOptions>(configuration.GetSection(ExternalServiceOptions.SectionName));

        services.AddHttpClient<ExternalServiceClient>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<ExternalServiceOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddStandardResilienceHandler();

        services.AddHostedService<ExternalServiceWorker>();

        return services;
    }
}
