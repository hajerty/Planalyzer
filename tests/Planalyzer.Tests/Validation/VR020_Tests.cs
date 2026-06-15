using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR020_Tests
{
    private const string Id = "VR.020";

    [Fact]
    public void Violates_when_datetime_type_used()
    {
        var sql = @"DECLARE @when DATETIME = SYSDATETIME();";
        var r = Validate(sql);
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_with_datetime2()
    {
        var sql = @"DECLARE @when DATETIME2 = SYSDATETIME();";
        var r = Validate(sql);
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.020 vincolo compatibilità legacy
DECLARE @when DATETIME = SYSDATETIME();";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
