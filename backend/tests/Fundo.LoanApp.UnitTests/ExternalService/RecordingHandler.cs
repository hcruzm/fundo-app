using System.Net;

namespace Fundo.LoanApp.UnitTests.ExternalService;

internal sealed class RecordingHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
