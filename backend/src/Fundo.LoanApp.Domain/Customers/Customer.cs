namespace Fundo.LoanApp.Domain.Customers;

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
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Customer Create(
        SsnHash ssnHash,
        string ssnLast4,
        string firstName,
        string lastName,
        string companyName,
        Address address,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ssnHash.Value);
        ArgumentException.ThrowIfNullOrWhiteSpace(ssnLast4);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        ArgumentNullException.ThrowIfNull(address);

        return new Customer
        {
            Id = Guid.CreateVersion7(),
            SsnHash = ssnHash,
            SsnLast4 = ssnLast4,
            FirstName = firstName,
            LastName = lastName,
            CompanyName = companyName,
            Address = address,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void UpdateDetails(
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
