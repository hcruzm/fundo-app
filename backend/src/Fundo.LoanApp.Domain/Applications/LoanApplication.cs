namespace Fundo.LoanApp.Domain.Applications;

public sealed class LoanApplication
{
    private LoanApplication()
    {
        // Required by EF Core.
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static LoanApplication Create(Guid customerId, decimal requestedAmount, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(customerId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestedAmount, 0m);

        return new LoanApplication
        {
            Id = Guid.CreateVersion7(),
            CustomerId = customerId,
            RequestedAmount = requestedAmount,
            Status = ApplicationStatus.Approved,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void UpdateRequestedAmount(decimal requestedAmount, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestedAmount, 0m);

        RequestedAmount = requestedAmount;
        UpdatedAt = now;
    }
}
