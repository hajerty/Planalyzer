using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

// VR.033 — COUNT(DISTINCT) on a "large" set. The "large" heuristic depends on
// stats; statically we just flag the presence of COUNT(DISTINCT) over an
// unbounded scan. Backend may refine.
public class VR033_Tests
{
    private const string Id = "VR.033";

    [Fact]
    public void Violates_on_count_distinct_over_full_table()
    {
        var r = Validate("SELECT COUNT(DISTINCT CustomerId) FROM dbo.Orders;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_on_plain_count()
    {
        var r = Validate("SELECT COUNT(*) FROM dbo.Orders;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.033 KPI distinct richiesto, eseguito off-peak
SELECT COUNT(DISTINCT CustomerId) FROM dbo.Orders;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
