using Planalyzer.Validation;
using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR010_Tests
{
    private const string Id = "VR.010";

    [Fact]
    public void Violates_on_update_without_where()
    {
        var r = Validate("UPDATE dbo.Orders SET Total = 0;");
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.Equal(Severity.Critical, f!.Severity);
    }

    [Fact]
    public void Violates_on_delete_without_where()
    {
        var r = Validate("DELETE FROM dbo.Orders;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_when_update_has_where()
    {
        var r = Validate("UPDATE dbo.Orders SET Total = 0 WHERE Id = 2;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Critical_is_NOT_justifiable()
    {
        // The catalog states Critical findings can never be justified.
        var sql = @"
-- justify: VR.010 reset acceptance
UPDATE dbo.Orders SET Total = 0;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.Equal(Severity.Critical, f!.Severity);
        Assert.Null(f.Justification);
        Assert.False(r.Certifiable);
    }
}
