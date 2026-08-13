using Fundo.LoanApp.Domain.Customers;

namespace Fundo.LoanApp.Application.Abstractions;

public interface ISsnHasher
{
    /// <summary>Produces the keyed digest used for lookups and blacklist checks.</summary>
    SsnHash Hash(string ssn);

    /// <summary>Returns the last four digits, the only part of the SSN that is stored in clear text.</summary>
    string Last4(string ssn);
}
