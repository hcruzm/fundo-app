using System.Text.Json;
using Fundo.LoanApp.Application.Abstractions;
using Fundo.LoanApp.Domain.Events;
using Fundo.LoanApp.Infrastructure.Persistence;

namespace Fundo.LoanApp.Infrastructure.Messaging;

/// <summary>
/// Adds the event to the change tracker as an outbox row. Nothing is written until the unit
/// of work saves and commits, so a rollback discards the event together with the records.
/// </summary>
public sealed class OutboxEventPublisher(LoanAppDbContext db, TimeProvider timeProvider) : IEventPublisher
{
    public void Publish(CustomerUpsertedEvent evt) =>
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Payload = JsonSerializer.Serialize(evt),
            CreatedAt = timeProvider.GetUtcNow()
        });
}
