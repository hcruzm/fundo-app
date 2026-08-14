using System.Threading.Channels;
using Fundo.LoanApp.Domain.Events;
using Fundo.LoanApp.Infrastructure.ExternalService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fundo.LoanApp.Infrastructure.Messaging;

/// <summary>
/// Drains the event channel outside the request pipeline. A delivery failure is logged and
/// the event is dropped; the worker keeps running so one bad event cannot stall the queue.
/// </summary>
public sealed class ExternalServiceWorker(
    Channel<CustomerUpsertedEvent> channel,
    IServiceScopeFactory scopeFactory,
    ILogger<ExternalServiceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<CustomerUpsertedDispatcher>();

                await dispatcher.DispatchAsync(evt, stoppingToken);

                logger.LogInformation(
                    "Delivered {Operation} for customer {CustomerId} to the external service.",
                    evt.IsUpdate ? "update" : "create",
                    evt.CustomerId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to deliver the event for customer {CustomerId} after retries.",
                    evt.CustomerId);
            }
        }
    }
}
