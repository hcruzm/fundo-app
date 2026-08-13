namespace Fundo.LoanApp.Domain.Decisions;

/// <summary>
/// Evaluates denial rules in registration order and stops at the first denial.
/// </summary>
public sealed class DecisionEngine(IEnumerable<IDenialRule> rules)
{
    public async Task<Decision> DecideAsync(LoanApplicationCandidate candidate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        foreach (var rule in rules)
        {
            var outcome = await rule.EvaluateAsync(candidate, ct);
            if (outcome.IsDenial)
            {
                return Decision.Denied(outcome.Reason!);
            }
        }

        return Decision.Approved();
    }
}
