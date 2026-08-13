namespace Fundo.LoanApp.Domain.Customers;

/// <summary>
/// A keyed digest of a social security number. The plaintext value is never stored.
/// </summary>
public readonly record struct SsnHash(string Value)
{
    public override string ToString() => Value;
}
