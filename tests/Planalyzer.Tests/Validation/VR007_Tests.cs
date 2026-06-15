using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR007_Tests
{
    private const string Id = "VR.007";

    [Fact]
    public void Violates_with_leading_wildcard()
    {
        var r = Validate("SELECT Id FROM dbo.Customers WHERE Name LIKE '%abc%';");
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_with_trailing_wildcard_only()
    {
        var r = Validate("SELECT Id FROM dbo.Customers WHERE Name LIKE 'abc%';");
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.007 ricerca full-scan accettata
SELECT Id FROM dbo.Customers WHERE Name LIKE '%abc%';";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
