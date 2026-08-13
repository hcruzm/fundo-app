using Fundo.LoanApp.Domain.Applications;

namespace Fundo.LoanApp.UnitTests.Domain;

public class LoanApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_links_the_customer_and_starts_approved()
    {
        var customerId = Guid.NewGuid();

        var application = LoanApplication.Create(customerId, 25_000m, Now);

        Assert.NotEqual(Guid.Empty, application.Id);
        Assert.Equal(customerId, application.CustomerId);
        Assert.Equal(25_000m, application.RequestedAmount);
        Assert.Equal(ApplicationStatus.Approved, application.Status);
        Assert.Equal(Now, application.CreatedAt);
    }

    [Fact]
    public void UpdateRequestedAmount_changes_the_amount_and_moves_only_UpdatedAt()
    {
        var application = LoanApplication.Create(Guid.NewGuid(), 25_000m, Now);
        var later = Now.AddDays(1);

        application.UpdateRequestedAmount(40_000m, later);

        Assert.Equal(40_000m, application.RequestedAmount);
        Assert.Equal(Now, application.CreatedAt);
        Assert.Equal(later, application.UpdatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_a_non_positive_amount(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LoanApplication.Create(Guid.NewGuid(), amount, Now));
    }
}
