using System.Reflection;
using Fundo.LoanApp.Domain.Customers;
using NetArchTest.Rules;

namespace Fundo.LoanApp.UnitTests.Architecture;

public class DependencyRuleTests
{
    private static readonly Assembly Domain = typeof(Customer).Assembly;

    [Fact]
    public void Domain_does_not_depend_on_any_other_layer_or_framework()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Fundo.LoanApp.Application",
                "Fundo.LoanApp.Infrastructure",
                "Fundo.LoanApp.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Microsoft.Extensions",
                "Npgsql")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Domain must not depend on outer layers. Offenders: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
