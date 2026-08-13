using System.Security.Cryptography;
using System.Text;
using Fundo.LoanApp.Application.Abstractions;
using Fundo.LoanApp.Domain.Customers;

namespace Fundo.LoanApp.Infrastructure.Security;

/// <summary>
/// Hashes SSNs with a keyed HMAC so the digest is stable enough to index but useless
/// to anyone who reads the database without the key.
/// </summary>
public sealed class HmacSsnHasher : ISsnHasher
{
    private readonly byte[] key;

    public HmacSsnHasher(string base64Key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64Key);
        key = Convert.FromBase64String(base64Key);

        if (key.Length < 32)
        {
            throw new ArgumentException("The SSN hash key must be at least 32 bytes.", nameof(base64Key));
        }
    }

    public SsnHash Hash(string ssn)
    {
        var digits = Normalize(ssn);
        var digest = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(digits));
        return new SsnHash(Convert.ToHexStringLower(digest));
    }

    public string Last4(string ssn) => Normalize(ssn)[^4..];

    private static string Normalize(string ssn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ssn);

        var digits = new string([.. ssn.Where(char.IsAsciiDigit)]);
        if (digits.Length != 9)
        {
            throw new ArgumentException("An SSN must contain exactly nine digits.", nameof(ssn));
        }

        return digits;
    }
}
