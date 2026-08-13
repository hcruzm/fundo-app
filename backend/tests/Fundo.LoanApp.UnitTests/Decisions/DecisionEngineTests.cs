using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Domain.Decisions;

namespace Fundo.LoanApp.UnitTests.Decisions;

public class DecisionEngineTests
{
    private static readonly LoanApplicationCandidate AnyCandidate = new(
        "Ada", "Lovelace", new Address("1 Byron Street", "Austin", "TX", "78701"),
        "Analytical Engines LLC", 25_000m, new SsnHash("abc"));

    private sealed class StubRule(RuleOutcome outcome) : IDenialRule
    {
        public int Invocations { get; private set; }

        public Task<RuleOutcome> EvaluateAsync(LoanApplicationCandidate candidate, CancellationToken ct)
        {
            Invocations++;
            return Task.FromResult(outcome);
        }
    }

    [Fact]
    public async Task Approves_when_every_rule_passes()
    {
        var engine = new DecisionEngine([new StubRule(RuleOutcome.Pass()), new StubRule(RuleOutcome.Pass())]);

        var decision = await engine.DecideAsync(AnyCandidate, CancellationToken.None);

        Assert.True(decision.IsApproved);
        Assert.Null(decision.DenialReason);
    }

    [Fact]
    public async Task Approves_when_there_are_no_rules()
    {
        var engine = new DecisionEngine([]);

        var decision = await engine.DecideAsync(AnyCandidate, CancellationToken.None);

        Assert.True(decision.IsApproved);
    }

    [Fact]
    public async Task Returns_the_reason_of_the_first_denying_rule()
    {
        var engine = new DecisionEngine([
            new StubRule(RuleOutcome.Deny("first reason")),
            new StubRule(RuleOutcome.Deny("second reason"))
        ]);

        var decision = await engine.DecideAsync(AnyCandidate, CancellationToken.None);

        Assert.False(decision.IsApproved);
        Assert.Equal("first reason", decision.DenialReason);
    }

    [Fact]
    public async Task Does_not_evaluate_rules_after_the_first_denial()
    {
        var denying = new StubRule(RuleOutcome.Deny("denied"));
        var later = new StubRule(RuleOutcome.Pass());
        var engine = new DecisionEngine([denying, later]);

        await engine.DecideAsync(AnyCandidate, CancellationToken.None);

        Assert.Equal(1, denying.Invocations);
        Assert.Equal(0, later.Invocations);
    }
}
