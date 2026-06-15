using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR005_Tests
{
    private const string Id = "VR.005";

    [Fact]
    public void Violates_when_nolock_used_without_justification()
    {
        var r = Validate("SELECT Id FROM dbo.Orders WITH (NOLOCK);");
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.Null(f!.Justification);
    }

    [Fact]
    public void Does_not_violate_without_nolock_hint()
    {
        var r = Validate("SELECT Id FROM dbo.Orders;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.005 dashboard cache stale ok
SELECT Id FROM dbo.Orders WITH (NOLOCK);";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
