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

    [Fact]
    public async Task Publish_preserves_the_order_of_events()
    {
        var channel = Channel.CreateUnbounded<CustomerUpsertedEvent>();
        var publisher = new ChannelEventPublisher(channel);
        var first = new CustomerUpsertedEvent(Guid.NewGuid(), Guid.NewGuid(), IsUpdate: false);
        var second = new CustomerUpsertedEvent(Guid.NewGuid(), Guid.NewGuid(), IsUpdate: true);

        publisher.Publish(first);
        publisher.Publish(second);

        Assert.Equal(first, await channel.Reader.ReadAsync());
        Assert.Equal(second, await channel.Reader.ReadAsync());
    }
}
