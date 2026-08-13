using Fundo.LoanApp.Domain.Customers;

namespace Fundo.LoanApp.UnitTests.Domain;

public class CustomerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Address AnyAddress = new("1 Byron Street", "Austin", "TX", "78701");

    [Fact]
    public void Create_assigns_an_identifier_and_stamps_both_timestamps()
    {
        var customer = Customer.Create(new SsnHash("abc"), "6789", "Ada", "Lovelace", "Analytical Engines LLC", AnyAddress, Now);

        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal(Now, customer.CreatedAt);
        Assert.Equal(Now, customer.UpdatedAt);
        Assert.Equal("6789", customer.SsnLast4);
    }

    [Fact]
    public void UpdateDetails_replaces_the_mutable_fields_and_moves_only_UpdatedAt()
    {
        var customer = Customer.Create(new SsnHash("abc"), "6789", "Ada", "Lovelace", "Analytical Engines LLC", AnyAddress, Now);
        var later = Now.AddDays(1);
        var newAddress = new Address("2 Main Street", "Dallas", "TX", "75201");

        customer.UpdateDetails("Augusta", "Byron", "Difference Engines LLC", newAddress, later);

        Assert.Equal("Augusta", customer.FirstName);
        Assert.Equal("Byron", customer.LastName);
        Assert.Equal("Difference Engines LLC", customer.CompanyName);
        Assert.Equal(newAddress, customer.Address);
        Assert.Equal(Now, customer.CreatedAt);
        Assert.Equal(later, customer.UpdatedAt);
    }

    [Fact]
    public void UpdateDetails_never_changes_the_ssn_hash()
    {
        var customer = Customer.Create(new SsnHash("abc"), "6789", "Ada", "Lovelace", "Analytical Engines LLC", AnyAddress, Now);

        customer.UpdateDetails("Augusta", "Byron", "Difference Engines LLC", AnyAddress, Now.AddDays(1));

        Assert.Equal(new SsnHash("abc"), customer.SsnHash);
    }
}
