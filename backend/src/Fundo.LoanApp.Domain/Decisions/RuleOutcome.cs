namespace Fundo.LoanApp.Domain.Decisions;

public sealed record RuleOutcome
{
    private RuleOutcome(bool isDenial, string? reason)
    {
        IsDenial = isDenial;
        Reason = reason;
    }

    public bool IsDenial { get; }
    public string? Reason { get; }

    public static RuleOutcome Pass() => new(false, null);

    public static RuleOutcome Deny(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new RuleOutcome(true, reason);
    }
}
