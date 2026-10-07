using System.Collections.Concurrent;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Stands in for a third-party system. State is in memory and resets on restart.
var customers = new ConcurrentDictionary<string, ReceivedCustomer>();

// Upsert keyed by the SSN hash: always 200, whether the customer is new or known. Being
// idempotent is what lets the sender deliver the same event more than once safely.
app.MapPut("/api/customers/{ssnHash}", (string ssnHash, ReceivedCustomer customer, ILogger<Program> logger) =>
{
    var operation = customers.ContainsKey(ssnHash) ? "UPDATE" : "CREATE";
    customers[ssnHash] = customer;

    logger.LogInformation(
        "{Operation} received for {FirstName} {LastName} ({SsnHash}) requesting {Amount}.",
        operation, customer.FirstName, customer.LastName, ssnHash, FormatUsd(customer.Application.RequestedAmount));

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
