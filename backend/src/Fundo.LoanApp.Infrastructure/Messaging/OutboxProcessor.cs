using System.Text.Json;
using Fundo.LoanApp.Domain.Events;
using Fundo.LoanApp.Infrastructure.ExternalService;
using Fundo.LoanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fundo.LoanApp.Infrastructure.Messaging;

/// <summary>
/// Polls the outbox outside the request pipeline and delivers pending events. A message is
/// marked processed only after the external service accepts it; a failed delivery stays
/// pending and is retried on the next poll, so delivery is at-least-once.
/// </summary>
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval, timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessPendingAsync(stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    // The outbox itself could not be read or updated. The messages are safe in
                    // the table, so the processor keeps running and tries again on the next tick.
                    logger.LogError(exception, "Failed to process the outbox.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
    }

    public async Task ProcessPendingAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LoanAppDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<CustomerUpsertedDispatcher>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in pending)
        {
            var evt = JsonSerializer.Deserialize<CustomerUpsertedEvent>(message.Payload)!;

            try
            {
                await dispatcher.DispatchAsync(evt, ct);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                logger.LogError(
                    exception,
                    "Failed to deliver outbox message {MessageId} for customer {CustomerId}; it will be retried.",
                    message.Id,
                    evt.CustomerId);
                continue;
            }

            message.ProcessedAt = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Delivered outbox message {MessageId} for customer {CustomerId} to the external service.",
                message.Id,
                evt.CustomerId);
        }
    }
}
