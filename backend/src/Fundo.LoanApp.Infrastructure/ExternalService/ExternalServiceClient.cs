using System.Net.Http.Json;

namespace Fundo.LoanApp.Infrastructure.ExternalService;

public sealed class ExternalServiceClient(HttpClient httpClient)
{
    /// <summary>
    /// Creates or replaces the customer, keyed by the SSN hash. The call is idempotent, so the
    /// outbox can safely deliver the same event more than once.
    /// </summary>
    public async Task UpsertAsync(CustomerPayload payload, CancellationToken ct)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/customers/{payload.SsnHash}", payload, ct);
        response.EnsureSuccessStatusCode();
    }
}
