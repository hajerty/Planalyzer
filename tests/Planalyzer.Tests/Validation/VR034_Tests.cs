using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR034_Tests
{
    private const string Id = "VR.034";

    [Fact]
    public void Violates_on_optional_parameter_pattern()
    {
        var sql = @"
DECLARE @cust INT = NULL;
SELECT Id
FROM dbo.Orders
WHERE (@cust IS NULL OR CustomerId = @cust);";
        var r = Validate(sql);
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_with_straight_predicate()
    {
        var sql = @"
DECLARE @cust INT = 1;
SELECT Id FROM dbo.Orders WHERE CustomerId = @cust;";
        var r = Validate(sql);
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
DECLARE @cust INT = NULL;
-- justify: VR.034 OPTION(RECOMPILE) accettato per ricerca dinamica
SELECT Id
FROM dbo.Orders
WHERE (@cust IS NULL OR CustomerId = @cust)
OPTION (RECOMPILE);";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
