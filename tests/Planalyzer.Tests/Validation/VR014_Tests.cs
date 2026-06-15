using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR014_Tests
{
    private const string Id = "VR.014";

    [Fact]
    public void Violates_when_select_has_neither_top_nor_limiting_predicate()
    {
        var r = Validate("SELECT Id FROM dbo.Orders;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_with_TOP()
    {
        var r = Validate("SELECT TOP 100 Id FROM dbo.Orders;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_with_where_predicate()
    {
        var r = Validate("SELECT Id FROM dbo.Orders WHERE Id = 1;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.014 piccola tabella di lookup
SELECT Id FROM dbo.Orders;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
