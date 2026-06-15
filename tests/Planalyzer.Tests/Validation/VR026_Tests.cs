using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR026_Tests
{
    private const string Id = "VR.026";

    [Fact]
    public void Violates_when_GOTO_present()
    {
        var sql = @"
DECLARE @i INT = 0;
IF @i = 0 GOTO done;
SET @i = 1;
done:
SELECT @i;";
        var r = Validate(sql);
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_without_GOTO()
    {
        var sql = @"
DECLARE @i INT = 0;
IF @i = 0 SET @i = 1;
SELECT @i;";
        var r = Validate(sql);
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
DECLARE @i INT = 0;
-- justify: VR.026 retry pattern documentato
IF @i = 0 GOTO done;
SET @i = 1;
done:
SELECT @i;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
