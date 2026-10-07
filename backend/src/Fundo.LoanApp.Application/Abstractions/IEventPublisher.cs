using Fundo.LoanApp.Domain.Events;

namespace Fundo.LoanApp.Application.Abstractions;

/// <summary>
/// Records the event as part of the current unit of work. It must be called inside
/// <see cref="IUnitOfWork.ExecuteInTransactionAsync{T}"/>: the event is stored only if that
/// transaction commits, and it is delivered later, outside the request.
/// </summary>
public interface IEventPublisher
{
    void Publish(CustomerUpsertedEvent evt);
}
