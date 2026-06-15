using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR027_Tests
{
    private const string Id = "VR.027";

    [Fact]
    public void Violates_when_sp_executesql_called_without_parameters()
    {
        var sql = @"EXEC sp_executesql N'SELECT 1';";
        var r = Validate(sql);
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_when_parameterized()
    {
        var sql = @"EXEC sp_executesql N'SELECT 1 WHERE 1 = @p', N'@p int', @p = 1;";
        var r = Validate(sql);
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.027 query statica, no parametri
EXEC sp_executesql N'SELECT 1';";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
