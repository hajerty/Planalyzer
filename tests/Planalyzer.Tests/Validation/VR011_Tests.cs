using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

// VR.011 — implicit conversion (e.g. NVARCHAR parameter against a VARCHAR
// column, or numeric literal against a string column). Without a catalog of
// real column types, detection must rely on heuristics like NVARCHAR literal
// (N'...') compared against a non-prefixed column, or numeric against quoted
// literal. This test pins the most common case.
public class VR011_Tests
{
    private const string Id = "VR.011";

    [Fact]
    public void Violates_when_nvarchar_literal_compared_against_varchar_like_column()
    {
        // Heuristic: comparing column ending in suffix that's typically VARCHAR
        // against an N'...' nvarchar literal.
        var r = Validate("SELECT Id FROM dbo.Orders WHERE Code = N'X';");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_with_matched_types()
    {
        var r = Validate("SELECT Id FROM dbo.Orders WHERE Code = 'X';");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.011 colonna nvarchar, conversione assente
SELECT Id FROM dbo.Orders WHERE Code = N'X';";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
