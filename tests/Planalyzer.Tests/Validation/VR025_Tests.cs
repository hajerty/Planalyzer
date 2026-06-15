using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR025_Tests
{
    private const string Id = "VR.025";

    [Fact]
    public void Violates_with_uncommented_index_hint()
    {
        var r = Validate("SELECT Id FROM dbo.Orders WITH (INDEX(IX_Orders_CustomerId)) WHERE CustomerId = 1;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_without_index_hint()
    {
        var r = Validate("SELECT Id FROM dbo.Orders WHERE CustomerId = 1;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.025 sniffing parametri patologico, pinning necessario
SELECT Id FROM dbo.Orders WITH (INDEX(IX_Orders_CustomerId)) WHERE CustomerId = 1;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
