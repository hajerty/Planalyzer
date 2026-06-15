using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR032_Tests
{
    private const string Id = "VR.032";

    [Fact]
    public void Violates_on_NOT_IN_against_subquery()
    {
        var sql = @"
SELECT Id
FROM dbo.Customers
WHERE Id NOT IN (SELECT CustomerId FROM dbo.Orders);";
        var r = Validate(sql);
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_with_NOT_EXISTS_correlated()
    {
        var sql = @"
SELECT c.Id
FROM dbo.Customers c
WHERE NOT EXISTS (SELECT 1 FROM dbo.Orders o WHERE o.CustomerId = c.Id);";
        var r = Validate(sql);
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.032 colonna NOT NULL garantita da constraint
SELECT Id
FROM dbo.Customers
WHERE Id NOT IN (SELECT CustomerId FROM dbo.Orders);";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
