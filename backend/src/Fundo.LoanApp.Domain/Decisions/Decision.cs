namespace Fundo.LoanApp.Domain.Decisions;

public sealed record Decision
{
    private Decision(bool isApproved, string? denialReason)
    {
        IsApproved = isApproved;
        DenialReason = denialReason;
    }

    public bool IsApproved { get; }
    public string? DenialReason { get; }

    public static Decision Approved() => new(true, null);

    public static Decision Denied(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new Decision(false, reason);
    }
}
