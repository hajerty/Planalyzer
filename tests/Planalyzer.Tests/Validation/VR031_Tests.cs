using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR031_Tests
{
    private const string Id = "VR.031";

    [Fact]
    public void Violates_on_OR_predicate_across_different_columns()
    {
        var r = Validate("SELECT Id FROM dbo.Orders WHERE CustomerId = 1 OR Status = 'A';");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_on_OR_on_same_column()
    {
        // Same column OR is fine (often turns into IN-list).
        var r = Validate("SELECT Id FROM dbo.Orders WHERE Status = 'A' OR Status = 'B';");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.031 indici filtrati garantiscono seek
SELECT Id FROM dbo.Orders WHERE CustomerId = 1 OR Status = 'A';";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
