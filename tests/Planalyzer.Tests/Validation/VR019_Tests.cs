using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

// VR.019 — table variable joined to a "big" table. Statically we can detect a
// @table variable participating in a JOIN; the "big" heuristic is left to the
// backend (catalog or stats). These tests pin the static structural case.
public class VR019_Tests
{
    private const string Id = "VR.019";

    [Fact]
    public void Violates_when_table_variable_joined_to_persistent_table()
    {
        var sql = @"
DECLARE @ids TABLE (Id INT PRIMARY KEY);
SELECT o.Id
FROM dbo.Orders o
INNER JOIN @ids i ON i.Id = o.Id;";
        var r = Validate(sql);
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_for_plain_join_of_persistent_tables()
    {
        var r = Validate(@"
SELECT o.Id
FROM dbo.Orders o
INNER JOIN dbo.OrderLines l ON l.OrderId = o.Id;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
DECLARE @ids TABLE (Id INT PRIMARY KEY);
-- justify: VR.019 set < 10 righe garantito dal chiamante
SELECT o.Id
FROM dbo.Orders o
INNER JOIN @ids i ON i.Id = o.Id;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
