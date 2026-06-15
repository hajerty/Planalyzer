using System.Linq;
using Planalyzer.Validation;
using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR016_Tests
{
    private const string Id = "VR.016";

    [Fact]
    public void Violates_when_too_many_derived_columns()
    {
        // Many CASE/CONVERT expressions in select list.
        var derived = string.Join(", ",
            Enumerable.Range(1, 12).Select(i => $"CASE WHEN C{i} = 0 THEN 1 ELSE 0 END AS D{i}"));
        var r = ValidateWith(
            $"SELECT {derived} FROM dbo.T;",
            new ValidationOptions { MaxDerivedColumns = 8, MaxOutputColumns = 1000 });
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_with_few_derived_columns()
    {
        var r = ValidateWith(
            "SELECT CASE WHEN C1 = 0 THEN 1 ELSE 0 END AS D1 FROM dbo.T;",
            new ValidationOptions { MaxDerivedColumns = 8 });
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var derived = string.Join(", ",
            Enumerable.Range(1, 12).Select(i => $"CASE WHEN C{i} = 0 THEN 1 ELSE 0 END AS D{i}"));
        var sql = $@"
-- justify: VR.016 view di reporting
SELECT {derived} FROM dbo.T;";
        var r = ValidateWith(sql, new ValidationOptions { MaxDerivedColumns = 8, MaxOutputColumns = 1000 });
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
