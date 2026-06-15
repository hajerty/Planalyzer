using Planalyzer.Validation;
using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR018_Tests
{
    private const string Id = "VR.018";

    [Fact]
    public void Violates_on_exec_with_string_concatenation()
    {
        var sql = @"
DECLARE @t sysname = N'dbo.Orders';
EXEC ('SELECT * FROM ' + @t);";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.Equal(Severity.Critical, f!.Severity);
    }

    [Fact]
    public void Does_not_violate_on_parameterized_sp_executesql()
    {
        var sql = @"EXEC sp_executesql N'SELECT 1 WHERE 1 = @p', N'@p int', @p = 1;";
        var r = Validate(sql);
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Critical_is_NOT_justifiable()
    {
        var sql = @"
-- justify: VR.018 input sanitizzato dal layer applicativo
DECLARE @t sysname = N'dbo.Orders';
EXEC ('SELECT * FROM ' + @t);";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.Equal(Severity.Critical, f!.Severity);
        Assert.Null(f.Justification);
    }
}
