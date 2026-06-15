using Planalyzer.Validation;
using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR001_Tests
{
    private const string Id = "VR.001";

    [Fact]
    public void Violates_on_select_star()
    {
        var r = Validate("SELECT * FROM dbo.Orders;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_when_columns_are_explicit()
    {
        var r = Validate("SELECT Id, Total FROM dbo.Orders;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_inside_exists_subquery()
    {
        var r = Validate(@"
SELECT o.Id
FROM dbo.Orders o
WHERE EXISTS (SELECT * FROM dbo.OrderLines l WHERE l.OrderId = o.Id);");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified_with_inline_comment()
    {
        var sql = @"
-- justify: VR.001 dump diagnostico una tantum
SELECT * FROM dbo.Orders;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
