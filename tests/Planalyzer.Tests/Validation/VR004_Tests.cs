using Xunit;
using static Planalyzer.Tests.Validation.TestHelpers;

namespace Planalyzer.Tests.Validation;

public class VR004_Tests
{
    private const string Id = "VR.004";

    [Fact]
    public void Violates_when_too_many_joins()
    {
        var sql = @"
SELECT a.Id
FROM dbo.A a
JOIN dbo.B b ON b.AId = a.Id
JOIN dbo.C c ON c.BId = b.Id
JOIN dbo.D d ON d.CId = c.Id
JOIN dbo.E e ON e.DId = d.Id
JOIN dbo.F f ON f.EId = e.Id
JOIN dbo.G g ON g.FId = f.Id
JOIN dbo.H h ON h.GId = g.Id
JOIN dbo.I i ON i.HId = h.Id;";
        var r = Validate(sql);
        Assert.True(HasFinding(r, Id));
    }

    [Fact]
    public void Does_not_violate_with_few_joins()
    {
        var sql = @"
SELECT a.Id
FROM dbo.A a
JOIN dbo.B b ON b.AId = a.Id
JOIN dbo.C c ON c.BId = b.Id
JOIN dbo.D d ON d.CId = c.Id;";
        var r = Validate(sql);
        Assert.False(HasFinding(r, Id));
    }

    [Fact]
    public void Can_be_justified()
    {
        var sql = @"
-- justify: VR.004 reporting wide-fact tollerato
SELECT a.Id
FROM dbo.A a
JOIN dbo.B b ON b.AId = a.Id
JOIN dbo.C c ON c.BId = b.Id
JOIN dbo.D d ON d.CId = c.Id
JOIN dbo.E e ON e.DId = d.Id
JOIN dbo.F f ON f.EId = e.Id
JOIN dbo.G g ON g.FId = f.Id
JOIN dbo.H h ON h.GId = g.Id
JOIN dbo.I i ON i.HId = h.Id;";
        var r = Validate(sql);
        var f = FindingOf(r, Id);
        Assert.NotNull(f);
        Assert.NotNull(f!.Justification);
    }
}
