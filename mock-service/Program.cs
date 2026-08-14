using System.Collections.Concurrent;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Stands in for a third-party system. State is in memory and resets on restart.
var customers = new ConcurrentDictionary<string, ReceivedCustomer>();

app.MapPost("/api/customers", (ReceivedCustomer customer, ILogger<Program> logger) =>
{
    if (!customers.TryAdd(customer.SsnHash, customer))
    {
        logger.LogWarning("Rejected a create for {SsnHash}: the customer already exists.", customer.SsnHash);
        return Results.Conflict(new { message = "This customer already exists." });
    }

    logger.LogInformation(
        "CREATE received for {FirstName} {LastName} ({SsnHash}) requesting {Amount}.",
        customer.FirstName, customer.LastName, customer.SsnHash, FormatUsd(customer.Application.RequestedAmount));

    return Results.Created($"/api/customers/{customer.SsnHash}", customer);
});

app.MapPut("/api/customers/{ssnHash}", (string ssnHash, ReceivedCustomer customer, ILogger<Program> logger) =>
{
    if (!customers.ContainsKey(ssnHash))
    {
        logger.LogWarning("Rejected an update for {SsnHash}: the customer is unknown.", ssnHash);
        return Results.NotFound(new { message = "This customer is unknown." });
    }

    customers[ssnHash] = customer;

    logger.LogInformation(
        "UPDATE received for {FirstName} {LastName} ({SsnHash}) requesting {Amount}.",
        customer.FirstName, customer.LastName, ssnHash, FormatUsd(customer.Application.RequestedAmount));

    return Results.Ok(customer);
});

// Lets the demo video show what the external service actually received.
app.MapGet("/api/customers", () => Results.Ok(customers.Values));

app.Run();

// The host runs under the invariant culture, where "C" prints the generic currency sign.
// The log lines are read by a person, so the culture is pinned to en-US.
static string FormatUsd(decimal amount) => amount.ToString("C", CultureInfo.GetCultureInfo("en-US"));

internal sealed record ReceivedCustomer(
    string SsnHash,
    string SsnLast4,
    string FirstName,
    string LastName,
    string CompanyName,
    ReceivedAddress Address,
    ReceivedApplication Application);

internal sealed record ReceivedAddress(string Street, string City, string State, string PostalCode);

internal sealed record ReceivedApplication(Guid Id, decimal RequestedAmount, string Status);
