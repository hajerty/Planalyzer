using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR006_Tests
{
    private const string Id = "VR.006";

    [Fact]
    public void Violates_when_function_applied_to_column_in_where()
    {
        var r = Validate("SELECT Id FROM dbo.Orders WHERE YEAR(OrderDate) = 2025;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_with_sargable_range_predicate()
    {
        var r = Validate(
            "SELECT Id FROM dbo.Orders WHERE OrderDate >= '20250101' AND OrderDate < '20260101';");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.006 colonna calcolata indicizzata
SELECT Id FROM dbo.Orders WHERE YEAR(OrderDate) = 2025;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
