namespace Fundo.LoanApp.Domain.Applications;

/// <summary>
/// Part of the <see cref="Customers.Customer"/> aggregate. It is created and changed only
/// through its customer, which is why its factory and its mutator are internal.
/// </summary>
public sealed class LoanApplication
{
    private LoanApplication()
    {
        // Required by EF Core.
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal static LoanApplication Create(Guid customerId, decimal requestedAmount, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(customerId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestedAmount, 0m);

        return new LoanApplication
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customerId,
            RequestedAmount = requestedAmount,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    internal void UpdateRequestedAmount(decimal requestedAmount, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestedAmount, 0m);

        RequestedAmount = requestedAmount;
        UpdatedAt = now;
    }
}
