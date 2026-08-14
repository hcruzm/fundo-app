using System.Net;
using System.Net.Http.Json;

namespace Fundo.LoanApp.IntegrationTests.Api;

public class ListApplicationsEndpointTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private LoanApplicationApiFactory factory = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        factory = new LoanApplicationApiFactory(fixture.ConnectionString);
        client = factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    private static object Payload(
        string ssn = "123-45-6789",
        decimal amount = 25_000m,
        string company = "Analytical Engines LLC") => new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            companyName = company,
            requestedAmount = amount,
            ssn,
            address = new { street = "1 Byron Street", city = "Austin", state = "TX", postalCode = "78701" }
        };

    [Fact]
    public async Task With_nothing_stored_it_returns_200_and_an_empty_array()
    {
        var response = await client.GetAsync("/api/applications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<ApplicationSummaryDto>>();
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task After_one_approved_submission_it_returns_exactly_one_entry()
    {
        await client.PostAsJsonAsync("/api/applications", Payload());

        var response = await client.GetAsync("/api/applications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<ApplicationSummaryDto>>();
        Assert.NotNull(body);
        var entry = Assert.Single(body);
        Assert.Equal("Ada", entry.FirstName);
        Assert.Equal("Lovelace", entry.LastName);
        Assert.Equal(25_000m, entry.RequestedAmount);
        Assert.False(entry.IsReturningCustomer);
    }

    [Fact]
    public async Task The_same_ssn_submitted_twice_yields_one_entry_with_the_second_amount()
    {
        await client.PostAsJsonAsync("/api/applications", Payload(amount: 25_000m));
        await client.PostAsJsonAsync("/api/applications", Payload(amount: 40_000m, company: "Difference Engines LLC"));

        var response = await client.GetAsync("/api/applications");

        var body = await response.Content.ReadFromJsonAsync<List<ApplicationSummaryDto>>();
        Assert.NotNull(body);
        var entry = Assert.Single(body);
        Assert.Equal(40_000m, entry.RequestedAmount);
        Assert.True(entry.IsReturningCustomer);
        Assert.Equal("Difference Engines LLC", entry.CompanyName);
    }

    [Fact]
    public async Task The_response_never_contains_the_full_ssn()
    {
        await client.PostAsJsonAsync("/api/applications", Payload());

        var response = await client.GetAsync("/api/applications");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Contains("6789", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("123456789", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("123-45-6789", raw, StringComparison.Ordinal);
    }

    private sealed record ApplicationSummaryDto(
        Guid ApplicationId,
        Guid CustomerId,
        string FirstName,
        string LastName,
        string CompanyName,
        decimal RequestedAmount,
        string MaskedSsn,
        string City,
        string State,
        bool IsReturningCustomer,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
