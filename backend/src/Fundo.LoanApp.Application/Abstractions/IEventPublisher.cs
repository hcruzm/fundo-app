using Fundo.LoanApp.Domain.Events;

namespace Fundo.LoanApp.Application.Abstractions;

/// <summary>
/// Hands the event off for processing outside the request. Implementations must not block.
/// </summary>
public interface IEventPublisher
{
    void Publish(CustomerUpsertedEvent evt);
}
