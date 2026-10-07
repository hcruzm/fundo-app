using Fundo.LoanApp.Domain.Customers;

namespace Fundo.LoanApp.UnitTests.Domain;

public class CustomerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Address AnyAddress = new("1 Byron Street", "Austin", "TX", "78701");

    private static Customer NewCustomer(decimal amount = 25_000m) =>
        Customer.Create(new SsnHash("abc"), "6789", "Ada", "Lovelace", "Analytical Engines LLC", AnyAddress, amount, Now);

    [Fact]
    public void Create_assigns_an_identifier_and_stamps_both_timestamps()
    {
        var customer = NewCustomer();

        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal(Now, customer.CreatedAt);
        Assert.Equal(Now, customer.UpdatedAt);
        Assert.Equal("6789", customer.SsnLast4);
    }

    [Fact]
    public void Create_opens_the_customers_application_with_the_requested_amount()
    {
        var customer = NewCustomer(25_000m);

        Assert.NotEqual(Guid.Empty, customer.Application.Id);
        Assert.Equal(customer.Id, customer.Application.CustomerId);
        Assert.Equal(25_000m, customer.Application.RequestedAmount);
        Assert.Equal(Now, customer.Application.CreatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_a_non_positive_amount(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewCustomer(amount));
    }

    [Fact]
    public void Reapply_updates_the_details_and_the_same_application_in_place()
    {
        var customer = NewCustomer(25_000m);
        var applicationId = customer.Application.Id;
        var later = Now.AddDays(1);
        var newAddress = new Address("2 Main Street", "Dallas", "TX", "75201");

        customer.Reapply("Augusta", "Byron", "Difference Engines LLC", newAddress, 40_000m, later);

        Assert.Equal("Augusta", customer.FirstName);
        Assert.Equal("Byron", customer.LastName);
        Assert.Equal("Difference Engines LLC", customer.CompanyName);
        Assert.Equal(newAddress, customer.Address);
        Assert.Equal(Now, customer.CreatedAt);
        Assert.Equal(later, customer.UpdatedAt);

        Assert.Equal(applicationId, customer.Application.Id);
        Assert.Equal(40_000m, customer.Application.RequestedAmount);
        Assert.Equal(Now, customer.Application.CreatedAt);
        Assert.Equal(later, customer.Application.UpdatedAt);
    }

    [Fact]
    public void Reapply_never_changes_the_ssn_hash()
    {
        var customer = NewCustomer();

        customer.Reapply("Augusta", "Byron", "Difference Engines LLC", AnyAddress, 40_000m, Now.AddDays(1));

        Assert.Equal(new SsnHash("abc"), customer.SsnHash);
    }

    [Fact]
    public void Reapply_rejects_a_non_positive_amount()
    {
        var customer = NewCustomer();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => customer.Reapply("Ada", "Lovelace", "Analytical Engines LLC", AnyAddress, 0m, Now.AddDays(1)));
    }
}
