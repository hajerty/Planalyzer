using Planalyzer.Comparison;
using Planalyzer.Core.Parsing;
using Xunit;

namespace Planalyzer.Tests;

public class ComparisonTests
{
    private static string Plan(bool actual, double estRows, double? actualRows)
    {
        var rt = actualRows.HasValue
            ? $@"<RunTimeInformation><RunTimeCountersPerThread Thread=""0"" ActualRows=""{actualRows.Value}"" ActualRowsRead=""{actualRows.Value}"" ActualExecutions=""1"" ActualElapsedms=""5"" ActualCPUms=""5"" ActualLogicalReads=""10"" ActualPhysicalReads=""0"" ActualReadAheads=""0""/></RunTimeInformation>"
            : "";
        return $@"<ShowPlanXML xmlns=""http://schemas.microsoft.com/sqlserver/2004/07/showplan"" Version=""1.564"" Build=""16.0"">
  <BatchSequence><Batch><Statements>
    <StmtSimple StatementText=""SELECT 1"" QueryHash=""0xH"" QueryPlanHash=""0xP""
                StatementSubTreeCost=""0.5"" StatementEstRows=""{estRows}"">
      <QueryPlan>
        <RelOp NodeId=""0"" PhysicalOp=""Index Seek"" LogicalOp=""Index Seek""
               EstimateRows=""{estRows}"" EstimatedTotalSubtreeCost=""0.5""
               EstimateCPU=""0.1"" EstimateIO=""0.4"" Parallel=""false"">
          <OutputList/>
          {rt}
        </RelOp>
      </QueryPlan>
    </StmtSimple>
  </Statements></Batch></BatchSequence>
</ShowPlanXML>";
    }

    [Fact]
    public void Estimated_vs_actual_detects_skew()
    {
        var est = ShowplanParser.ParseString(Plan(false, 10, null));
        var act = ShowplanParser.ParseString(Plan(true, 10, 5000));
        var rep = EstimatedVsActual.Compare(est, act);
        Assert.Single(rep.Operators);
        Assert.True(rep.Operators[0].SkewRatio >= 100);
    }

    [Fact]
    public void Multi_db_aggregates_recommendations()
    {
        var p1 = ShowplanParser.ParseString(Plan(false, 100, null));
        var p2 = ShowplanParser.ParseString(Plan(false, 100, null));
        var rep = MultiDbComparer.Compare(new[]
        {
            new DbVariant("prod", p1),
            new DbVariant("staging", p2),
        });
        Assert.Equal("stesso", rep.QueryHashStatus);
        Assert.NotNull(rep.SqlSuggestion);
    }
}
