using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR029_Tests
{
    private const string Id = "VR.029";

    [Fact]
    public void Violates_when_DDL_inside_explicit_transaction()
    {
        var sql = @"
BEGIN TRANSACTION;
CREATE TABLE dbo.NewTable (Id INT);
COMMIT;";
        var r = Validate(sql);
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_when_DDL_outside_transaction()
    {
        var r = Validate("CREATE TABLE dbo.NewTable (Id INT);");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.029 deploy script atomico richiesto
BEGIN TRANSACTION;
CREATE TABLE dbo.NewTable (Id INT);
COMMIT;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
