using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR013_Tests
{
    private const string Id = "VR.013";

    [Fact]
    public void Violates_on_equal_NULL()
    {
        var r = Validate("SELECT Id FROM dbo.Orders WHERE Status = NULL;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Violates_on_not_equal_NULL()
    {
        var r = Validate("SELECT Id FROM dbo.Orders WHERE Status <> NULL;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_with_IS_NULL()
    {
        var r = Validate("SELECT Id FROM dbo.Orders WHERE Status IS NULL;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.013 ANSI_NULLS OFF nel contesto
SELECT Id FROM dbo.Orders WHERE Status = NULL;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
