using System.Linq;
using Planalyzer.Validation;
using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR003_Tests
{
    private const string Id = "VR.003";

    [Fact]
    public void Violates_when_select_list_exceeds_threshold()
    {
        var cols = string.Join(", ", Enumerable.Range(1, 35).Select(i => $"C{i}"));
        var r = ValidateWith(
            $"SELECT {cols} FROM dbo.T;",
            new ValidationOptions { MaxOutputColumns = 30 });
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_under_threshold()
    {
        var r = ValidateWith(
            "SELECT C1, C2, C3 FROM dbo.T;",
            new ValidationOptions { MaxOutputColumns = 30 });
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var cols = string.Join(", ", Enumerable.Range(1, 35).Select(i => $"C{i}"));
        var sql = $@"
-- justify: VR.003 export ETL legacy
SELECT {cols} FROM dbo.T;";
        var r = ValidateWith(sql, new ValidationOptions { MaxOutputColumns = 30 });
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
