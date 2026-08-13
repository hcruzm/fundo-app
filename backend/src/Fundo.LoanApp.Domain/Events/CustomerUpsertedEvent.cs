namespace Fundo.LoanApp.Domain.Events;

/// <summary>
/// Raised after an approved application has been committed. <see cref="IsUpdate"/> tells the
/// external service whether to create or replace its copy of the customer.
/// </summary>
public sealed record CustomerUpsertedEvent(Guid CustomerId, Guid ApplicationId, bool IsUpdate);
