using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR030_Tests
{
    private const string Id = "VR.030";

    [Fact]
    public void Violates_on_implicit_cross_join_without_correlation()
    {
        var r = Validate("SELECT a.Id, b.Id FROM dbo.A a, dbo.B b;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_on_explicit_cross_join()
    {
        var r = Validate("SELECT a.Id, b.Id FROM dbo.A a CROSS JOIN dbo.B b;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_on_correlated_comma_join()
    {
        // The comma-join with WHERE correlation is VR.012, not VR.030.
        var r = Validate("SELECT a.Id FROM dbo.A a, dbo.B b WHERE a.Id = b.AId;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.030 prodotto cartesiano voluto per matrice di test
SELECT a.Id, b.Id FROM dbo.A a, dbo.B b;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
