using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR021_Tests
{
    private const string Id = "VR.021";

    [Fact]
    public void Violates_when_two_tables_in_join_have_no_alias()
    {
        var r = Validate(@"
SELECT Orders.Id
FROM dbo.Orders
INNER JOIN dbo.OrderLines ON OrderLines.OrderId = Orders.Id;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_with_aliases()
    {
        var r = Validate(@"
SELECT o.Id
FROM dbo.Orders o
INNER JOIN dbo.OrderLines l ON l.OrderId = o.Id;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.021 nomi parlanti tollerati
SELECT Orders.Id
FROM dbo.Orders
INNER JOIN dbo.OrderLines ON OrderLines.OrderId = Orders.Id;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
