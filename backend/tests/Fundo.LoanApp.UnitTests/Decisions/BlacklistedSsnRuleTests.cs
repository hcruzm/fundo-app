using Fundo.LoanApp.Domain.Customers;
using Fundo.LoanApp.Domain.Decisions;
using Fundo.LoanApp.Domain.Decisions.Rules;

namespace Fundo.LoanApp.UnitTests.Decisions;

public class BlacklistedSsnRuleTests
{
    private sealed class FakeBlacklist(params string[] blacklisted) : IBlacklistedSsnRepository
    {
        public Task<bool> ContainsAsync(SsnHash ssnHash, CancellationToken ct) =>
            Task.FromResult(blacklisted.Contains(ssnHash.Value));
    }

    private static LoanApplicationCandidate CandidateWithHash(string hash) => new(
        "Ada", "Lovelace", new Address("1 Byron Street", "Austin", "TX", "78701"),
        "Analytical Engines LLC", 25_000m, new SsnHash(hash));

    [Fact]
    public async Task Denies_a_blacklisted_ssn()
    {
        var rule = new BlacklistedSsnRule(new FakeBlacklist("blocked-hash"));

        var outcome = await rule.EvaluateAsync(CandidateWithHash("blocked-hash"), CancellationToken.None);

        Assert.True(outcome.IsDenial);
        Assert.Equal("This application cannot be processed.", outcome.Reason);
    }

    [Fact]
    public async Task Passes_an_ssn_that_is_not_blacklisted()
    {
        var rule = new BlacklistedSsnRule(new FakeBlacklist("blocked-hash"));

        var outcome = await rule.EvaluateAsync(CandidateWithHash("other-hash"), CancellationToken.None);

        Assert.False(outcome.IsDenial);
    }
}
