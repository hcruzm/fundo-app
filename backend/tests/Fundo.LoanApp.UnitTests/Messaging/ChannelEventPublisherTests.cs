using System.Threading.Channels;
using Fundo.LoanApp.Domain.Events;
using Fundo.LoanApp.Infrastructure.Messaging;

namespace Fundo.LoanApp.UnitTests.Messaging;

public class ChannelEventPublisherTests
{
    [Fact]
    public void Publish_hands_the_event_to_the_channel_without_blocking()
    {
        var channel = Channel.CreateUnbounded<CustomerUpsertedEvent>();
        var publisher = new ChannelEventPublisher(channel);
        var evt = new CustomerUpsertedEvent(Guid.NewGuid(), Guid.NewGuid(), IsUpdate: true);

        publisher.Publish(evt);

        Assert.True(channel.Reader.TryRead(out var read));
        Assert.Equal(evt, read);
    }
}
