using System.Net;
using System.Net.Http.Json;
using Fundo.LoanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fundo.LoanApp.IntegrationTests.Api;

public class SubmitApplicationEndpointTests(PostgresFixture fixture)
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
        string state = "TX",
        string ssn = "123-45-6789",
        decimal amount = 25_000m,
        string company = "Analytical Engines LLC") => new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            companyName = company,
            requestedAmount = amount,
            ssn,
            address = new { street = "1 Byron Street", city = "Austin", state, postalCode = "78701" }
        };

    [Fact]
    public async Task An_approved_application_returns_201_and_stores_one_customer_and_one_application()
    {
        var response = await client.PostAsJsonAsync("/api/applications", Payload());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SubmitApplicationResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Approved", body.Decision);
        Assert.False(body.IsReturningCustomer);
        Assert.Equal($"/api/applications/{body.ApplicationId}", response.Headers.Location?.OriginalString);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Equal(1, await db.Applications.CountAsync());
    }

    [Fact]
    public async Task An_applicant_in_NY_is_denied_and_nothing_is_stored()
    {
        var response = await client.PostAsJsonAsync("/api/applications", Payload(state: "NY"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SubmitApplicationResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Denied", body.Decision);
        Assert.Equal("We do not currently accept applications from NY.", body.Reason);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(0, await db.Customers.CountAsync());
        Assert.Equal(0, await db.Applications.CountAsync());
    }

    [Fact]
    public async Task A_blacklisted_ssn_is_denied_and_nothing_is_stored()
    {
        var response = await client.PostAsJsonAsync("/api/applications", Payload(ssn: "111-11-1111"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SubmitApplicationResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Denied", body.Decision);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(0, await db.Customers.CountAsync());
        Assert.Equal(0, await db.Applications.CountAsync());
    }

    [Fact]
    public async Task The_same_ssn_submitted_twice_updates_instead_of_duplicating()
    {
        var first = await client.PostAsJsonAsync("/api/applications", Payload(amount: 25_000m));
        var firstBody = await first.Content.ReadFromJsonAsync<SubmitApplicationResponseDto>();

        var second = await client.PostAsJsonAsync(
            "/api/applications", Payload(amount: 40_000m, company: "Difference Engines LLC"));
        var secondBody = await second.Content.ReadFromJsonAsync<SubmitApplicationResponseDto>();

        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.True(secondBody.IsReturningCustomer);
        Assert.Equal(firstBody.CustomerId, secondBody.CustomerId);
        Assert.Equal(firstBody.ApplicationId, secondBody.ApplicationId);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Equal(1, await db.Applications.CountAsync());
        Assert.Equal(40_000m, (await db.Applications.SingleAsync()).RequestedAmount);
        Assert.Equal("Difference Engines LLC", (await db.Customers.SingleAsync()).CompanyName);
    }

    [Fact]
    public async Task An_invalid_payload_returns_400_with_validation_problem_details()
    {
        var response = await client.PostAsJsonAsync("/api/applications", Payload(ssn: "12345", amount: -5m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync();
        Assert.Contains("nine digits", problem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_payload_with_no_ssn_field_returns_400_not_500()
    {
        var response = await client.PostAsJsonAsync("/api/applications", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            companyName = "Analytical Engines LLC",
            requestedAmount = 25_000m,
            address = new { street = "1 Byron Street", city = "Austin", state = "TX", postalCode = "78701" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_empty_json_object_returns_400_not_500()
    {
        var response = await client.PostAsJsonAsync("/api/applications", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Getting_a_stored_application_returns_the_masked_ssn_and_never_the_full_value()
    {
        var created = await client.PostAsJsonAsync("/api/applications", Payload());
        var body = await created.Content.ReadFromJsonAsync<SubmitApplicationResponseDto>();
        Assert.NotNull(body);

        var response = await client.GetAsync($"/api/applications/{body.ApplicationId}");
        var detail = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("6789", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("123456789", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("123-45-6789", detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Getting_an_unknown_application_returns_404()
    {
        var response = await client.GetAsync($"/api/applications/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record SubmitApplicationResponseDto(
        string Decision,
        Guid? ApplicationId,
        Guid? CustomerId,
        bool IsReturningCustomer,
        string? Reason);
}
