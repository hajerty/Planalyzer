using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR008_Tests
{
    private const string Id = "VR.008";

    [Fact]
    public void Violates_when_explicit_cursor_is_declared()
    {
        var sql = @"
DECLARE c CURSOR FOR SELECT Id FROM dbo.Orders;
OPEN c;
CLOSE c;
DEALLOCATE c;";
        var r = Validate(sql);
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_in_set_based_query()
    {
        var r = Validate("SELECT Id FROM dbo.Orders;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.008 batch amministrativo notturno
DECLARE c CURSOR FOR SELECT Id FROM dbo.Orders;
OPEN c;
CLOSE c;
DEALLOCATE c;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
