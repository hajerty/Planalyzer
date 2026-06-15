using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR028_Tests
{
    private const string Id = "VR.028";

    [Fact]
    public void Violates_on_case_without_else()
    {
        var r = Validate(@"
SELECT CASE WHEN Id = 1 THEN 'one' END AS Label FROM dbo.T;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_on_case_with_else()
    {
        var r = Validate(@"
SELECT CASE WHEN Id = 1 THEN 'one' ELSE 'other' END AS Label FROM dbo.T;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.028 NULL atteso semanticamente
SELECT CASE WHEN Id = 1 THEN 'one' END AS Label FROM dbo.T;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
