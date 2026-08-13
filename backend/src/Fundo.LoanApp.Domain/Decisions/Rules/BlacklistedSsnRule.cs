namespace Fundo.LoanApp.Domain.Decisions.Rules;

public sealed class BlacklistedSsnRule(IBlacklistedSsnRepository blacklist) : IDenialRule
{
    public async Task<RuleOutcome> EvaluateAsync(LoanApplicationCandidate candidate, CancellationToken ct)
    {
        var isBlacklisted = await blacklist.ContainsAsync(candidate.SsnHash, ct);

        // The reason stays generic on purpose: a specific message would disclose the blacklist.
        return isBlacklisted
            ? RuleOutcome.Deny("This application cannot be processed.")
            : RuleOutcome.Pass();
    }
}
