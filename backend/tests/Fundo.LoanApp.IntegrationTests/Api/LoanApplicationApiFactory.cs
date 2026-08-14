using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Fundo.LoanApp.IntegrationTests.Api;

public sealed class LoanApplicationApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = connectionString,
                ["Security:SsnHashKey"] = "ZGV2ZWxvcG1lbnQtb25seS1zc24ta2V5LTMyLWJ5dGVzIQ==",
                ["ExternalService:BaseUrl"] = "http://localhost:59999"
            }));
    }
}
