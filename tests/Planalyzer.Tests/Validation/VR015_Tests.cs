using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR015_Tests
{
    private const string Id = "VR.015";

    [Fact]
    public void Violates_on_uncommented_select_distinct()
    {
        var r = Validate("SELECT DISTINCT CustomerId FROM dbo.Orders;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_on_plain_select()
    {
        var r = Validate("SELECT CustomerId FROM dbo.Orders;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.015 dedup intenzionale dopo union
SELECT DISTINCT CustomerId FROM dbo.Orders;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
