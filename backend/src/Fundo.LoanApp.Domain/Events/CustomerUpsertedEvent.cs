namespace Fundo.LoanApp.Domain.Events;

/// <summary>
/// Raised when an approved application creates or updates a customer and their application.
/// </summary>
public sealed record CustomerUpsertedEvent(Guid CustomerId, Guid ApplicationId);
