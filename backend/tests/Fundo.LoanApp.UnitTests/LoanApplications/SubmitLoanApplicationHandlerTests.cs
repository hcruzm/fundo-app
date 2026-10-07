using Fundo.LoanApp.Application.Abstractions;
using Fundo.LoanApp.Application.LoanApplications;
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
        new AddressDto("1 Byron Street", "Austin", state, "78701"));

    private static Customer ExistingCustomer() => Customer.Create(
        new SsnHash("hash:123456789"), "6789", "Ada", "Lovelace", "Old Company LLC",
        new Address("1 Byron Street", "Austin", "TX", "78701"), 10_000m, Now.AddYears(-1));

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

        public Task<Customer?> FindByApplicationIdAsync(Guid applicationId, CancellationToken ct) =>
            Task.FromResult(existing);

        public void Add(Customer customer) => Added.Add(customer);
    }

    private sealed class PassThroughUnitOfWork : IUnitOfWork
    {
        public int Invocations { get; private set; }
        public bool InTransaction { get; private set; }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
        {
            Invocations++;
            InTransaction = true;
            try
            {
                return await operation(ct);
            }
            finally
            {
                InTransaction = false;
            }
        }
    }

    private sealed class FailingUnitOfWork : IUnitOfWork
    {
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) =>
            throw new InvalidOperationException("commit failed");
    }

    private sealed class RecordingPublisher(PassThroughUnitOfWork? unitOfWork = null) : IEventPublisher
    {
        public List<CustomerUpsertedEvent> Published { get; } = [];
        public List<bool> PublishedInsideTransaction { get; } = [];

        public void Publish(CustomerUpsertedEvent evt)
        {
            Published.Add(evt);
            PublishedInsideTransaction.Add(unitOfWork?.InTransaction ?? false);
        }
    }

    private static SubmitLoanApplicationHandler CreateHandler(
        bool denies,
        ICustomerRepository customers,
        IUnitOfWork unitOfWork,
        IEventPublisher publisher) =>
        new(new DecisionEngine([new StubRule(denies)]),
            new StubHasher(),
            customers,
            unitOfWork,
            publisher,
            new FakeTimeProvider(Now));

    [Fact]
    public async Task A_new_applicant_is_approved_and_one_customer_with_its_application_is_added()
    {
        var customers = new FakeCustomerRepository();
        var handler = CreateHandler(denies: false, customers, new PassThroughUnitOfWork(), new RecordingPublisher());

        var result = await handler.HandleAsync(CommandInState(), CancellationToken.None);

        var approved = Assert.IsType<SubmitLoanApplicationResult.Approved>(result);
        Assert.False(approved.IsReturningCustomer);
        var added = Assert.Single(customers.Added);
        Assert.Equal("6789", added.SsnLast4);
        Assert.Equal(new SsnHash("hash:123456789"), added.SsnHash);
        Assert.Equal(25_000m, added.Application.RequestedAmount);
        Assert.Equal(added.Id, approved.CustomerId);
        Assert.Equal(added.Application.Id, approved.ApplicationId);
    }

    [Fact]
    public async Task A_denied_applicant_persists_nothing_and_publishes_nothing()
    {
        var customers = new FakeCustomerRepository();
        var unitOfWork = new PassThroughUnitOfWork();
        var publisher = new RecordingPublisher();
        var handler = CreateHandler(denies: true, customers, unitOfWork, publisher);

        var result = await handler.HandleAsync(CommandInState("NY"), CancellationToken.None);

        var denied = Assert.IsType<SubmitLoanApplicationResult.Denied>(result);
        Assert.Equal("denied by policy", denied.Reason);
        Assert.Empty(customers.Added);
        Assert.Empty(publisher.Published);
        Assert.Equal(0, unitOfWork.Invocations);
    }

    [Fact]
    public async Task A_returning_customer_is_updated_instead_of_duplicated()
    {
        var existing = ExistingCustomer();
        var existingApplicationId = existing.Application.Id;
        var customers = new FakeCustomerRepository(existing);
        var handler = CreateHandler(denies: false, customers, new PassThroughUnitOfWork(), new RecordingPublisher());

        var command = CommandInState(amount: 40_000m) with { CompanyName = "New Company LLC" };
        var result = await handler.HandleAsync(command, CancellationToken.None);

        var approved = Assert.IsType<SubmitLoanApplicationResult.Approved>(result);
        Assert.True(approved.IsReturningCustomer);
        Assert.Equal(existing.Id, approved.CustomerId);
        Assert.Equal(existingApplicationId, approved.ApplicationId);
        Assert.Empty(customers.Added);
        Assert.Equal("New Company LLC", existing.CompanyName);
        Assert.Equal(40_000m, existing.Application.RequestedAmount);
    }

    [Fact]
    public async Task An_approval_publishes_exactly_one_event_for_the_new_records()
    {
        var customers = new FakeCustomerRepository();
        var publisher = new RecordingPublisher();
        var handler = CreateHandler(denies: false, customers, new PassThroughUnitOfWork(), publisher);

        await handler.HandleAsync(CommandInState(), CancellationToken.None);

        var evt = Assert.Single(publisher.Published);
        Assert.Equal(customers.Added[0].Id, evt.CustomerId);
        Assert.Equal(customers.Added[0].Application.Id, evt.ApplicationId);
    }

    [Fact]
    public async Task A_returning_customer_publishes_an_event_for_the_existing_records()
    {
        var existing = ExistingCustomer();
        var publisher = new RecordingPublisher();
        var handler = CreateHandler(denies: false, new FakeCustomerRepository(existing), new PassThroughUnitOfWork(), publisher);

        await handler.HandleAsync(CommandInState(), CancellationToken.None);

        var evt = Assert.Single(publisher.Published);
        Assert.Equal(existing.Id, evt.CustomerId);
        Assert.Equal(existing.Application.Id, evt.ApplicationId);
    }

    [Fact]
    public async Task The_event_is_published_inside_the_transaction()
    {
        var unitOfWork = new PassThroughUnitOfWork();
        var publisher = new RecordingPublisher(unitOfWork);
        var handler = CreateHandler(denies: false, new FakeCustomerRepository(), unitOfWork, publisher);

        await handler.HandleAsync(CommandInState(), CancellationToken.None);

        Assert.True(Assert.Single(publisher.PublishedInsideTransaction));
    }

    [Fact]
    public async Task No_event_is_published_when_the_transaction_fails()
    {
        var publisher = new RecordingPublisher();
        var handler = CreateHandler(denies: false, new FakeCustomerRepository(), new FailingUnitOfWork(), publisher);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(CommandInState(), CancellationToken.None));

        Assert.Empty(publisher.Published);
    }
}
