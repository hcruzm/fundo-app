using System.Collections.Concurrent;

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
        "CREATE received for {FirstName} {LastName} ({SsnHash}) requesting {Amount:C}.",
        customer.FirstName, customer.LastName, customer.SsnHash, customer.Application.RequestedAmount);

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
        "UPDATE received for {FirstName} {LastName} ({SsnHash}) requesting {Amount:C}.",
        customer.FirstName, customer.LastName, ssnHash, customer.Application.RequestedAmount);

    return Results.Ok(customer);
});

// Lets the demo video show what the external service actually received.
app.MapGet("/api/customers", () => Results.Ok(customers.Values));

app.MapDelete("/api/customers", () =>
{
    customers.Clear();
    return Results.NoContent();
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

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
