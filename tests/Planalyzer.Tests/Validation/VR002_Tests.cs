using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR002_Tests
{
    private const string Id = "VR.002";

    [Fact]
    public void Violates_when_schema_prefix_missing()
    {
        var r = Validate("SELECT Id FROM Orders;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_with_dbo_prefix()
    {
        var r = Validate("SELECT Id FROM dbo.Orders;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.002 tabella temp permessa
SELECT Id FROM Orders;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
