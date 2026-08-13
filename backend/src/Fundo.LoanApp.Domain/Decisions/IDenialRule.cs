namespace Fundo.LoanApp.Domain.Decisions;

/// <summary>
/// One reason an application can be denied. Register an implementation to add a rule;
/// the engine and the use case do not change.
/// </summary>
public interface IDenialRule
{
    Task<RuleOutcome> EvaluateAsync(LoanApplicationCandidate candidate, CancellationToken ct);
}
