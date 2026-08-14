using System.Threading.Channels;
using Fundo.LoanApp.Application.Abstractions;
using Fundo.LoanApp.Domain.Events;

namespace Fundo.LoanApp.Infrastructure.Messaging;

/// <summary>
/// Writes to an unbounded in-process channel. TryWrite always succeeds on an unbounded
/// channel, so the request thread never waits on the external service.
/// </summary>
public sealed class ChannelEventPublisher(Channel<CustomerUpsertedEvent> channel) : IEventPublisher
{
    public void Publish(CustomerUpsertedEvent evt) => channel.Writer.TryWrite(evt);
}
