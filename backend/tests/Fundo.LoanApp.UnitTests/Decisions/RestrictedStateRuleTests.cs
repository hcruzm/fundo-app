using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Domain.Decisions;
using Fundo.LoanApp.Domain.Decisions.Rules;

namespace Fundo.LoanApp.UnitTests.Decisions;

public class RestrictedStateRuleTests
{
    private static readonly RestrictedStates Restricted =
        new(new HashSet<string>(["NY"], StringComparer.OrdinalIgnoreCase));

    private static LoanApplicationCandidate CandidateInState(string state) => new(
        "Ada", "Lovelace", new Address("1 Byron Street", "New York", state, "10001"),
        "Analytical Engines LLC", 25_000m, new SsnHash("abc"));

    [Fact]
    public async Task Denies_an_applicant_in_a_restricted_state()
    {
        var rule = new RestrictedStateRule(Restricted);

        var outcome = await rule.EvaluateAsync(CandidateInState("NY"), CancellationToken.None);

        Assert.True(outcome.IsDenial);
        Assert.Equal("We do not currently accept applications from NY.", outcome.Reason);
    }

    [Fact]
    public async Task Denies_regardless_of_the_casing_used_in_the_form()
    {
        var rule = new RestrictedStateRule(Restricted);

        var outcome = await rule.EvaluateAsync(CandidateInState("ny"), CancellationToken.None);

        Assert.True(outcome.IsDenial);
    }

    [Fact]
    public async Task Passes_an_applicant_in_an_allowed_state()
    {
        var rule = new RestrictedStateRule(Restricted);

        var outcome = await rule.EvaluateAsync(CandidateInState("TX"), CancellationToken.None);

        Assert.False(outcome.IsDenial);
        Assert.Null(outcome.Reason);
    }
}
