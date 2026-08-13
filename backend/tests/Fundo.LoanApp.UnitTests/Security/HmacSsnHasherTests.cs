using Fundo.LoanApp.Infrastructure.Security;

namespace Fundo.LoanApp.UnitTests.Security;

public class HmacSsnHasherTests
{
    private const string KeyA = "Zm9vYmFyLXRlc3Qta2V5LTAxMjM0NTY3ODlhYmNkZWY=";
    private const string KeyB = "YmFyLWZvby10ZXN0LWtleS0wMTIzNDU2Nzg5YWJjZGU=";

    [Fact]
    public void Hash_is_deterministic_for_the_same_input_and_key()
    {
        var hasher = new HmacSsnHasher(KeyA);

        Assert.Equal(hasher.Hash("123-45-6789"), hasher.Hash("123-45-6789"));
    }

    [Theory]
    [InlineData("123-45-6789")]
    [InlineData("123456789")]
    [InlineData("123 45 6789")]
    public void Hash_ignores_formatting_so_the_same_ssn_always_matches(string formatted)
    {
        var hasher = new HmacSsnHasher(KeyA);

        Assert.Equal(hasher.Hash("123456789"), hasher.Hash(formatted));
    }

    [Fact]
    public void Hash_differs_across_keys()
    {
        Assert.NotEqual(new HmacSsnHasher(KeyA).Hash("123456789"), new HmacSsnHasher(KeyB).Hash("123456789"));
    }

    [Fact]
    public void Hash_never_contains_the_plaintext()
    {
        var hash = new HmacSsnHasher(KeyA).Hash("123456789");

        Assert.DoesNotContain("123456789", hash.Value, StringComparison.Ordinal);
        Assert.Equal(64, hash.Value.Length);
    }

    [Fact]
    public void Last4_returns_the_trailing_digits_of_the_normalized_value()
    {
        Assert.Equal("6789", new HmacSsnHasher(KeyA).Last4("123-45-6789"));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567890")]
    [InlineData("abc-de-fghi")]
    public void Hash_rejects_a_value_that_is_not_nine_digits(string invalid)
    {
        var hasher = new HmacSsnHasher(KeyA);

        Assert.Throws<ArgumentException>(() => hasher.Hash(invalid));
    }
}
