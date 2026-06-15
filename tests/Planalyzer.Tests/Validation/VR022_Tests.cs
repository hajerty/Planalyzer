using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR022_Tests
{
    private const string Id = "VR.022";

    [Fact]
    public void Violates_on_ordinal_order_by()
    {
        var r = Validate("SELECT Id, Name FROM dbo.Customers ORDER BY 1, 2;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_on_named_order_by()
    {
        var r = Validate("SELECT Id, Name FROM dbo.Customers ORDER BY Id, Name;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.022 export ad-hoc, ordinali consapevoli
SELECT Id, Name FROM dbo.Customers ORDER BY 1, 2;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
