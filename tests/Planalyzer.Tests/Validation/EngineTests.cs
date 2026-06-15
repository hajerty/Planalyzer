using System.Linq;
using Planalyzer.Validation;
using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class EngineTests
{
    [Fact]
    public void ParseError_marks_report_uncertifiable_and_records_error()
    {
        var r = Validate("SELEKT 1 FRUM dbo.Orders");
        Assert.NotNull(r.ParseError);
        Assert.False(r.Certifiable);
    }

    [Fact]
    public void Score_falls_below_50_when_critical_and_high_combine()
    {
        var sql = @"
SELECT * FROM dbo.Orders;
UPDATE dbo.Orders SET Total = 0;";
        var r = Validate(sql);
        Assert.True(r.CountsBySeverity.TryGetValue(nameof(Severity.Critical), out var crit) && crit > 0,
            "Expected at least one Critical finding (UPDATE without WHERE).");
        Assert.True(r.Score < 50, $"Expected score < 50, got {r.Score}.");
    }

    [Fact]
    public void Justify_does_not_disarm_a_Critical_finding()
    {
        var sql = @"
-- justify: VR.010 manutenzione una tantum approvata
UPDATE dbo.Orders SET Total = 0;";
        var r = Validate(sql);
        var f = FindingOf(r, "VR.010");
        Assert.NotNull(f);
        Assert.Equal(Severity.Critical, f!.Severity);
        // Critical findings must never carry a Justification (engine rule).
        Assert.Null(f.Justification);
        Assert.False(r.Certifiable);
    }

    [Fact]
    public void Registry_contains_at_least_thirty_rules_with_distinct_ids()
    {
        var ids = Registry.All.Select(r => r.RuleId).ToList();
        Assert.True(ids.Count >= 30, $"Expected >= 30 rules, found {ids.Count}.");
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
