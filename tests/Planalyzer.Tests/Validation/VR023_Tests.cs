using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

// VR.023 — WHILE loop walking single rows (RBAR). Detection is heuristic: a
// WHILE block whose body issues a SELECT/UPDATE keyed on a scalar variable
// updated each iteration. We pin the textbook shape.
public class VR023_Tests
{
    private const string Id = "VR.023";

    [Fact]
    public void Violates_on_classic_row_by_row_while_loop()
    {
        var sql = @"
DECLARE @i INT = 1;
WHILE @i <= 100
BEGIN
    UPDATE dbo.Orders SET Total = Total + 1 WHERE Id = @i;
    SET @i = @i + 1;
END;";
        var r = Validate(sql);
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_on_plain_set_based_update()
    {
        var r = Validate("UPDATE dbo.Orders SET Total = Total + 1 WHERE Id BETWEEN 1 AND 100;");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.023 batch piccolo, niente alternativa set-based
DECLARE @i INT = 1;
WHILE @i <= 100
BEGIN
    UPDATE dbo.Orders SET Total = Total + 1 WHERE Id = @i;
    SET @i = @i + 1;
END;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
