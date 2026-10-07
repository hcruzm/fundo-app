using Fundo.LoanApp.Application.Abstractions;
using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Domain.Decisions;
using Fundo.LoanApp.Domain.Events;

namespace Fundo.LoanApp.Application.LoanApplications;

public sealed class SubmitLoanApplicationHandler(
    DecisionEngine decisionEngine,
    ISsnHasher ssnHasher,
    ICustomerRepository customers,
    IUnitOfWork unitOfWork,
    IEventPublisher eventPublisher,
    TimeProvider timeProvider)
{
    public async Task<SubmitLoanApplicationResult> HandleAsync(
        SubmitLoanApplicationCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ssnHash = ssnHasher.Hash(command.Ssn);
        var address = new Address(
            command.Address.Street,
            command.Address.City,
            command.Address.State,
            command.Address.PostalCode);

        var candidate = new LoanApplicationCandidate(
            command.FirstName,
            command.LastName,
            address,
            command.CompanyName,
            command.RequestedAmount,
            ssnHash);

        var decision = await decisionEngine.DecideAsync(candidate, ct);
        if (!decision.IsApproved)
        {
            return new SubmitLoanApplicationResult.Denied(decision.DenialReason!);
        }

        var now = timeProvider.GetUtcNow();

        return await unitOfWork.ExecuteInTransactionAsync<SubmitLoanApplicationResult>(async token =>
        {
            var customer = await customers.FindBySsnHashAsync(ssnHash, token);
            var isReturningCustomer = customer is not null;

            if (customer is null)
            {
                customer = Customer.Create(
                    ssnHash,
                    ssnHasher.Last4(command.Ssn),
                    command.FirstName,
                    command.LastName,
                    command.CompanyName,
                    address,
                    command.RequestedAmount,
                    now);
                customers.Add(customer);
            }
            else
            {
                customer.Reapply(
                    command.FirstName,
                    command.LastName,
                    command.CompanyName,
                    address,
                    command.RequestedAmount,
                    now);
            }

            // Recorded inside the transaction: the event commits or rolls back with the records.
            eventPublisher.Publish(new CustomerUpsertedEvent(customer.Id, customer.Application.Id));

            return new SubmitLoanApplicationResult.Approved(customer.Application.Id, customer.Id, isReturningCustomer);
        }, ct);
    }
}
