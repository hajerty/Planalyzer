using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

// VR.009 — Scalar UDF in WHERE/SELECT.
// The rule's exact detection strategy depends on backend implementation
// (heuristic: known scalar-UDF call by name, or any user function reference).
// These tests pin down the contract from the catalog: a clearly user-defined
// scalar function call in WHERE must be flagged; built-ins / clean SELECT
// must not.
public class VR009_Tests
{
    private const string Id = "VR.009";

    [Fact]
    public void Violates_when_user_scalar_udf_used_in_where()
    {
        var r = Validate("SELECT Id FROM dbo.Orders WHERE dbo.fn_IsActive(Id) = 1;");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_in_a_plain_select()
    {
        var r = Validate("SELECT Id FROM dbo.Orders WHERE Status = 'A';");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.009 UDF inlinabile su 2019+
SELECT Id FROM dbo.Orders WHERE dbo.fn_IsActive(Id) = 1;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
