using System.Net.Http.Json;

namespace Fundo.LoanApp.Infrastructure.ExternalService;

public sealed class ExternalServiceClient(HttpClient httpClient)
{
    public async Task CreateAsync(CustomerPayload payload, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync("/api/customers", payload, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(CustomerPayload payload, CancellationToken ct)
    {
        var response = await httpClient.PutAsJsonAsync($"/api/customers/{payload.SsnHash}", payload, ct);
        response.EnsureSuccessStatusCode();
    }
}
