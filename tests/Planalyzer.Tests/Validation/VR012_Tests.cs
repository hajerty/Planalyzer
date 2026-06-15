using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR012_Tests
{
    private const string Id = "VR.012";

    [Fact]
    public void Violates_on_old_style_comma_join()
    {
        var r = Validate("SELECT a.Id FROM dbo.A a, dbo.B b WHERE a.Id = b.AId;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_with_ansi_join()
    {
        var r = Validate("SELECT a.Id FROM dbo.A a INNER JOIN dbo.B b ON a.Id = b.AId;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.012 legacy code, refactor pianificato
SELECT a.Id FROM dbo.A a, dbo.B b WHERE a.Id = b.AId;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
