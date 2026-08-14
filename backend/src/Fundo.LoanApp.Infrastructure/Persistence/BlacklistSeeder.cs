using Fundo.LoanApp.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Fundo.LoanApp.Infrastructure.Persistence;

public sealed class BlacklistSeeder(LoanAppDbContext db, ISsnHasher hasher)
{
    /// <summary>
    /// The plaintext SSNs that reviewers use to trigger a blacklist denial. They live here
    /// rather than in the migration because the stored value depends on the hash key.
    /// </summary>
    public static readonly IReadOnlyList<(string Ssn, string Note)> Entries =
    [
        ("111-11-1111", "seed: blacklisted for demonstration"),
        ("222-22-2222", "seed: blacklisted for demonstration")
    ];

    public async Task SeedAsync(CancellationToken ct)
    {
        foreach (var (ssn, note) in Entries)
        {
            var hash = hasher.Hash(ssn).Value;

            if (!await db.BlacklistedSsns.AnyAsync(b => b.SsnHash == hash, ct))
            {
                db.BlacklistedSsns.Add(new BlacklistedSsn(hash, note));
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
