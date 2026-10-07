namespace Fundo.LoanApp.Infrastructure.Messaging;

/// <summary>
/// An event waiting to be delivered. It is written in the same transaction as the records it
/// describes, so it exists if and only if they do.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public string Payload { get; init; } = null!;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
