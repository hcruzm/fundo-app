namespace Fundo.LoanApp.Domain.Decisions.Rules;

public sealed class RestrictedStateRule(RestrictedStates restrictedStates) : IDenialRule
{
    public Task<RuleOutcome> EvaluateAsync(LoanApplicationCandidate candidate, CancellationToken ct)
    {
        var state = candidate.Address.State;

        return Task.FromResult(
            restrictedStates.Codes.Contains(state)
                ? RuleOutcome.Deny($"We do not currently accept applications from {state.ToUpperInvariant()}.")
                : RuleOutcome.Pass());
    }
}
