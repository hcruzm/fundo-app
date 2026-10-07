using System.Net;
using Fundo.LoanApp.Infrastructure.ExternalService;

namespace Fundo.LoanApp.UnitTests.ExternalService;

public class ExternalServiceClientTests
{
    private static CustomerPayload SamplePayload() => new(
        "hash-1", "6789", "Ada", "Lovelace", "Analytical Engines LLC",
        new AddressPayload("1 Byron Street", "Austin", "TX", "78701"),
        new ApplicationPayload(Guid.NewGuid(), 25_000m, "Approved"));

    [Fact]
    public async Task UpsertAsync_puts_to_the_item_route_keyed_by_the_ssn_hash()
    {
        var handler = new RecordingHandler();
        var client = new ExternalServiceClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://external.test") });

        await client.UpsertAsync(SamplePayload(), CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/api/customers/hash-1", request.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task A_failed_response_throws_so_the_message_stays_in_the_outbox()
    {
        var handler = new FailingHandler();
        var client = new ExternalServiceClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://external.test") });

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.UpsertAsync(SamplePayload(), CancellationToken.None));
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }
}
