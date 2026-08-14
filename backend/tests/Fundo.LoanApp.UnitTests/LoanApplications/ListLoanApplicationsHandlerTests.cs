using Fundo.LoanApp.Application.LoanApplications;
using Fundo.LoanApp.Domain.Applications;
using Fundo.LoanApp.Domain.Customers;

namespace Fundo.LoanApp.UnitTests.LoanApplications;

public class ListLoanApplicationsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeCustomerRepository(params Customer[] customers) : ICustomerRepository
    {
        public Task<Customer?> FindBySsnHashAsync(SsnHash ssnHash, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Customer>>(customers);

        public void Add(Customer customer) => throw new NotSupportedException();
    }

    private sealed class FakeApplicationRepository(params LoanApplication[] applications) : ILoanApplicationRepository
    {
        public Task<LoanApplication?> GetByIdAsync(Guid id, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<LoanApplication?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<LoanApplication>> GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LoanApplication>>(applications);

        public void Add(LoanApplication application) => throw new NotSupportedException();
    }

    private static Customer MakeCustomer(
        string ssnLast4 = "6789", string firstName = "Ada", string lastName = "Lovelace") =>
        Customer.Create(
            new SsnHash($"hash:{ssnLast4}"), ssnLast4, firstName, lastName, "Analytical Engines LLC",
            new Address("1 Byron Street", "Austin", "TX", "78701"), Now);

    [Fact]
    public async Task An_empty_repository_yields_an_empty_list()
    {
        var handler = new ListLoanApplicationsHandler(new FakeApplicationRepository(), new FakeCustomerRepository());

        var result = await handler.HandleAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Results_are_ordered_by_updated_at_descending()
    {
        var oldest = MakeCustomer("1111", "Ada", "First");
        var middle = MakeCustomer("2222", "Ada", "Second");
        var newest = MakeCustomer("3333", "Ada", "Third");

        var oldestApp = LoanApplication.Create(oldest.Id, 10_000m, Now.AddDays(-2));
        var middleApp = LoanApplication.Create(middle.Id, 20_000m, Now.AddDays(-3));
        middleApp.UpdateRequestedAmount(21_000m, Now.AddDays(-1));
        var newestApp = LoanApplication.Create(newest.Id, 30_000m, Now);

        var handler = new ListLoanApplicationsHandler(
            new FakeApplicationRepository(oldestApp, middleApp, newestApp),
            new FakeCustomerRepository(oldest, middle, newest));

        var result = await handler.HandleAsync(CancellationToken.None);

        Assert.Equal(["Third", "Second", "First"], result.Select(r => r.LastName));
    }

    [Fact]
    public async Task Is_returning_customer_is_true_only_when_updated_after_created()
    {
        var untouched = MakeCustomer("1111", "Ada", "Untouched");
        var updated = MakeCustomer("2222", "Ada", "Updated");

        var untouchedApp = LoanApplication.Create(untouched.Id, 10_000m, Now);
        var updatedApp = LoanApplication.Create(updated.Id, 20_000m, Now.AddDays(-1));
        updatedApp.UpdateRequestedAmount(25_000m, Now);

        var handler = new ListLoanApplicationsHandler(
            new FakeApplicationRepository(untouchedApp, updatedApp),
            new FakeCustomerRepository(untouched, updated));

        var result = await handler.HandleAsync(CancellationToken.None);

        Assert.False(result.Single(r => r.LastName == "Untouched").IsReturningCustomer);
        Assert.True(result.Single(r => r.LastName == "Updated").IsReturningCustomer);
    }

    [Fact]
    public async Task The_summary_carries_the_masked_ssn_and_never_the_full_value()
    {
        var customer = MakeCustomer("6789");
        var application = LoanApplication.Create(customer.Id, 25_000m, Now);

        var handler = new ListLoanApplicationsHandler(
            new FakeApplicationRepository(application), new FakeCustomerRepository(customer));

        var result = await handler.HandleAsync(CancellationToken.None);

        var summary = Assert.Single(result);
        Assert.Equal("•••-••-6789", summary.MaskedSsn);
    }

    [Fact]
    public async Task An_application_referencing_a_missing_customer_throws()
    {
        var customer = MakeCustomer();
        var orphanApplication = LoanApplication.Create(Guid.NewGuid(), 25_000m, Now);

        var handler = new ListLoanApplicationsHandler(
            new FakeApplicationRepository(orphanApplication), new FakeCustomerRepository(customer));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(CancellationToken.None));
    }
}
