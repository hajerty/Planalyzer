using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

// VR.024 — missing ';' terminator. Info severity, but a finding must still be
// emitted when expected. We use multi-statement input where the prior
// statement clearly needs the terminator (THROW, CTE).
public class VR024_Tests
{
    private const string Id = "VR.024";

    [Fact]
    public void Violates_when_statement_terminator_missing()
    {
        var sql = @"
WITH X AS (SELECT 1 AS V)
SELECT V FROM X
SELECT 1;";
        var r = Validate(sql);
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_with_proper_terminators()
    {
        var sql = @"
WITH X AS (SELECT 1 AS V)
SELECT V FROM X;
SELECT 1;";
        var r = Validate(sql);
        Assert.False(HasFinding(r, Id));
    }
}
