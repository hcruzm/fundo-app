using Fundo.LoanApp.Application.Abstractions;
using Fundo.LoanApp.Domain.Applications;
using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Domain.Decisions;
using Fundo.LoanApp.Domain.Events;

namespace Fundo.LoanApp.Application.LoanApplications;

public sealed class SubmitLoanApplicationHandler(
    DecisionEngine decisionEngine,
    ISsnHasher ssnHasher,
    ICustomerRepository customers,
    ILoanApplicationRepository applications,
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

        var upsert = await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var existing = await customers.FindBySsnHashAsync(ssnHash, token);

            if (existing is null)
            {
                var customer = Customer.Create(
                    ssnHash,
                    ssnHasher.Last4(command.Ssn),
                    command.FirstName,
                    command.LastName,
                    command.CompanyName,
                    address,
                    now);
                customers.Add(customer);

                var application = LoanApplication.Create(customer.Id, command.RequestedAmount, now);
                applications.Add(application);

                // Recorded inside the transaction: the event commits or rolls back with the records.
                eventPublisher.Publish(new CustomerUpsertedEvent(customer.Id, application.Id));

                return new UpsertOutcome(customer.Id, application.Id, IsUpdate: false);
            }

            existing.UpdateDetails(command.FirstName, command.LastName, command.CompanyName, address, now);

            var currentApplication = await applications.GetByCustomerIdAsync(existing.Id, token)
                ?? throw new InvalidOperationException($"Customer {existing.Id} has no application to update.");
            currentApplication.UpdateRequestedAmount(command.RequestedAmount, now);

            eventPublisher.Publish(new CustomerUpsertedEvent(existing.Id, currentApplication.Id));

            return new UpsertOutcome(existing.Id, currentApplication.Id, IsUpdate: true);
        }, ct);

        return new SubmitLoanApplicationResult.Approved(upsert.ApplicationId, upsert.CustomerId, upsert.IsUpdate);
    }

    private sealed record UpsertOutcome(Guid CustomerId, Guid ApplicationId, bool IsUpdate);
}
