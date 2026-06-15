using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR017_Tests
{
    private const string Id = "VR.017";

    [Fact]
    public void Violates_when_stored_procedure_lacks_set_nocount_on()
    {
        var sql = @"
CREATE PROCEDURE dbo.foo
AS
BEGIN
    SELECT Id FROM dbo.Orders WHERE Id = 1;
END;";
        var r = Validate(sql);
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_when_set_nocount_on_present()
    {
        var sql = @"
CREATE PROCEDURE dbo.foo
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id FROM dbo.Orders WHERE Id = 1;
END;";
        var r = Validate(sql);
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.017 client legge @@ROWCOUNT esplicito
CREATE PROCEDURE dbo.foo
AS
BEGIN
    SELECT Id FROM dbo.Orders WHERE Id = 1;
END;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
