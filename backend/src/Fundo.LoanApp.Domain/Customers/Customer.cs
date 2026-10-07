using Fundo.LoanApp.Domain.Applications;

namespace Fundo.LoanApp.Domain.Customers;

/// <summary>
/// The aggregate root. A customer always owns exactly one loan application, created with it
/// and updated through it, so the one-customer-to-one-application rule cannot be broken from
/// outside the aggregate.
/// </summary>
public sealed class Customer
{
    private Customer()
    {
        // Required by EF Core.
    }

    public Guid Id { get; private set; }
    public SsnHash SsnHash { get; private set; }
    public string SsnLast4 { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string CompanyName { get; private set; } = null!;
    public Address Address { get; private set; } = null!;
    public LoanApplication Application { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>A first-time applicant: the customer and their application are created together.</summary>
    public static Customer Create(
        SsnHash ssnHash,
        string ssnLast4,
        string firstName,
        string lastName,
        string companyName,
        Address address,
        decimal requestedAmount,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ssnHash.Value);
        ArgumentException.ThrowIfNullOrWhiteSpace(ssnLast4);

        var customer = new Customer
        {
            Id = Guid.CreateVersion7(),
            SsnHash = ssnHash,
            SsnLast4 = ssnLast4,
            CreatedAt = now
        };

        customer.SetDetails(firstName, lastName, companyName, address, now);
        customer.Application = LoanApplication.Create(customer.Id, requestedAmount, now);

        return customer;
    }

    /// <summary>
    /// A returning applicant: their details and their existing application are updated in
    /// place. The SSN is the customer's identity and never changes.
    /// </summary>
    public void Reapply(
        string firstName,
        string lastName,
        string companyName,
        Address address,
        decimal requestedAmount,
        DateTimeOffset now)
    {
        SetDetails(firstName, lastName, companyName, address, now);
        Application.UpdateRequestedAmount(requestedAmount, now);
    }

    private void SetDetails(
        string firstName,
        string lastName,
        string companyName,
        Address address,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        ArgumentNullException.ThrowIfNull(address);

        FirstName = firstName;
        LastName = lastName;
        CompanyName = companyName;
        Address = address;
        UpdatedAt = now;
    }
}
