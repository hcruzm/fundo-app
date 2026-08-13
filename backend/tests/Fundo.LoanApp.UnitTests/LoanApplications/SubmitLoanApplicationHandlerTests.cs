using Fundo.LoanApp.Application.Abstractions;
using Fundo.LoanApp.Application.LoanApplications;
using Fundo.LoanApp.Domain.Applications;
using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Domain.Decisions;
using Fundo.LoanApp.Domain.Events;
using Microsoft.Extensions.Time.Testing;

namespace Fundo.LoanApp.UnitTests.LoanApplications;

public class SubmitLoanApplicationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private static SubmitLoanApplicationCommand CommandInState(string state = "TX", decimal amount = 25_000m) => new(
        "Ada", "Lovelace", "Analytical Engines LLC", amount, "123-45-6789",
        new AddressInput("1 Byron Street", "Austin", state, "78701"));

    private sealed class StubHasher : ISsnHasher
    {
        public SsnHash Hash(string ssn) => new($"hash:{new string([.. ssn.Where(char.IsAsciiDigit)])}");

        public string Last4(string ssn) => new string([.. ssn.Where(char.IsAsciiDigit)])[^4..];
    }

    private sealed class StubRule(bool denies) : IDenialRule
    {
        public Task<RuleOutcome> EvaluateAsync(LoanApplicationCandidate candidate, CancellationToken ct) =>
            Task.FromResult(denies ? RuleOutcome.Deny("denied by policy") : RuleOutcome.Pass());
    }

    private sealed class FakeCustomerRepository(Customer? existing = null) : ICustomerRepository
    {
        public List<Customer> Added { get; } = [];

        public Task<Customer?> FindBySsnHashAsync(SsnHash ssnHash, CancellationToken ct) => Task.FromResult(existing);

        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(existing);

        public void Add(Customer customer) => Added.Add(customer);
    }

    private sealed class FakeApplicationRepository(LoanApplication? existing = null) : ILoanApplicationRepository
    {
        public List<LoanApplication> Added { get; } = [];

        public Task<LoanApplication?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(existing);

        public Task<LoanApplication?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct) => Task.FromResult(existing);

        public void Add(LoanApplication application) => Added.Add(application);
    }

    private sealed class PassThroughUnitOfWork : IUnitOfWork
    {
        public int Invocations { get; private set; }

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
        {
            Invocations++;
            return operation(ct);
        }
    }

    private sealed class FailingUnitOfWork : IUnitOfWork
    {
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) =>
            throw new InvalidOperationException("commit failed");
    }

    private sealed class RecordingPublisher : IEventPublisher
    {
        public List<CustomerUpsertedEvent> Published { get; } = [];

        public void Publish(CustomerUpsertedEvent evt) => Published.Add(evt);
    }

    private static SubmitLoanApplicationHandler CreateHandler(
        bool denies,
        ICustomerRepository customers,
        ILoanApplicationRepository applications,
        IUnitOfWork unitOfWork,
        IEventPublisher publisher) =>
        new(new DecisionEngine([new StubRule(denies)]),
            new StubHasher(),
            customers,
            applications,
            unitOfWork,
            publisher,
            new FakeTimeProvider(Now));

    [Fact]
    public async Task A_new_applicant_is_approved_and_both_records_are_added()
    {
        var customers = new FakeCustomerRepository();
        var applications = new FakeApplicationRepository();
        var publisher = new RecordingPublisher();
        var handler = CreateHandler(denies: false, customers, applications, new PassThroughUnitOfWork(), publisher);

        var result = await handler.HandleAsync(CommandInState(), CancellationToken.None);

        var approved = Assert.IsType<SubmitLoanApplicationResult.Approved>(result);
        Assert.False(approved.IsReturningCustomer);
        Assert.Single(customers.Added);
        Assert.Single(applications.Added);
        Assert.Equal("6789", customers.Added[0].SsnLast4);
        Assert.Equal(new SsnHash("hash:123456789"), customers.Added[0].SsnHash);
    }

    [Fact]
    public async Task A_denied_applicant_persists_nothing_and_publishes_nothing()
    {
        var customers = new FakeCustomerRepository();
        var applications = new FakeApplicationRepository();
        var unitOfWork = new PassThroughUnitOfWork();
        var publisher = new RecordingPublisher();
        var handler = CreateHandler(denies: true, customers, applications, unitOfWork, publisher);

        var result = await handler.HandleAsync(CommandInState("NY"), CancellationToken.None);

        var denied = Assert.IsType<SubmitLoanApplicationResult.Denied>(result);
        Assert.Equal("denied by policy", denied.Reason);
        Assert.Empty(customers.Added);
        Assert.Empty(applications.Added);
        Assert.Empty(publisher.Published);
        Assert.Equal(0, unitOfWork.Invocations);
    }

    [Fact]
    public async Task A_returning_customer_is_updated_instead_of_duplicated()
    {
        var existingCustomer = Customer.Create(
            new SsnHash("hash:123456789"), "6789", "Ada", "Lovelace", "Old Company LLC",
            new Address("1 Byron Street", "Austin", "TX", "78701"), Now.AddYears(-1));
        var existingApplication = LoanApplication.Create(existingCustomer.Id, 10_000m, Now.AddYears(-1));

        var customers = new FakeCustomerRepository(existingCustomer);
        var applications = new FakeApplicationRepository(existingApplication);
        var publisher = new RecordingPublisher();
        var handler = CreateHandler(denies: false, customers, applications, new PassThroughUnitOfWork(), publisher);

        var command = CommandInState(amount: 40_000m) with { CompanyName = "New Company LLC" };
        var result = await handler.HandleAsync(command, CancellationToken.None);

        var approved = Assert.IsType<SubmitLoanApplicationResult.Approved>(result);
        Assert.True(approved.IsReturningCustomer);
        Assert.Equal(existingCustomer.Id, approved.CustomerId);
        Assert.Equal(existingApplication.Id, approved.ApplicationId);
        Assert.Empty(customers.Added);
        Assert.Empty(applications.Added);
        Assert.Equal("New Company LLC", existingCustomer.CompanyName);
        Assert.Equal(40_000m, existingApplication.RequestedAmount);
    }

    [Fact]
    public async Task An_approval_publishes_exactly_one_event_marked_as_a_create()
    {
        var publisher = new RecordingPublisher();
        var handler = CreateHandler(denies: false, new FakeCustomerRepository(), new FakeApplicationRepository(),
            new PassThroughUnitOfWork(), publisher);

        await handler.HandleAsync(CommandInState(), CancellationToken.None);

        var evt = Assert.Single(publisher.Published);
        Assert.False(evt.IsUpdate);
    }

    [Fact]
    public async Task A_returning_customer_publishes_an_event_marked_as_an_update()
    {
        var existingCustomer = Customer.Create(
            new SsnHash("hash:123456789"), "6789", "Ada", "Lovelace", "Old Company LLC",
            new Address("1 Byron Street", "Austin", "TX", "78701"), Now.AddYears(-1));
        var publisher = new RecordingPublisher();
        var handler = CreateHandler(denies: false,
            new FakeCustomerRepository(existingCustomer),
            new FakeApplicationRepository(LoanApplication.Create(existingCustomer.Id, 10_000m, Now.AddYears(-1))),
            new PassThroughUnitOfWork(), publisher);

        await handler.HandleAsync(CommandInState(), CancellationToken.None);

        var evt = Assert.Single(publisher.Published);
        Assert.True(evt.IsUpdate);
    }

    [Fact]
    public async Task No_event_is_published_when_the_transaction_fails()
    {
        var publisher = new RecordingPublisher();
        var handler = CreateHandler(denies: false, new FakeCustomerRepository(), new FakeApplicationRepository(),
            new FailingUnitOfWork(), publisher);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(CommandInState(), CancellationToken.None));

        Assert.Empty(publisher.Published);
    }
}
