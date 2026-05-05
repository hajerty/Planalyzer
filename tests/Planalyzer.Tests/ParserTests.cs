using Planalyzer.Core.Analysis;
using Planalyzer.Core.Knowledge;
using Planalyzer.Core.Parsing;
using Xunit;

namespace Planalyzer.Tests;

public class ParserTests
{
    private const string MinimalEstimatedPlan = """
<ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan" Version="1.564" Build="16.0.4150.1">
  <BatchSequence>
    <Batch>
      <Statements>
        <StmtSimple StatementText="SELECT * FROM dbo.Orders WHERE CustomerId = 1;"
                    StatementType="SELECT" QueryHash="0xABCDEF" QueryPlanHash="0x12345"
                    StatementSubTreeCost="1.234" StatementEstRows="100"
                    CardinalityEstimationModelVersion="160" StatementOptmLevel="FULL">
          <QueryPlan>
            <RelOp NodeId="0" PhysicalOp="Clustered Index Scan" LogicalOp="Clustered Index Scan"
                   EstimateRows="100" EstimateIO="0.1" EstimateCPU="0.05"
                   EstimatedTotalSubtreeCost="1.234" EstimatedRowsRead="10000" Parallel="false">
              <OutputList>
                <ColumnReference Database="db" Schema="dbo" Table="Orders" Column="Id"/>
                <ColumnReference Database="db" Schema="dbo" Table="Orders" Column="CustomerId"/>
              </OutputList>
              <IndexScan Ordered="false">
                <DefinedValues>
                  <DefinedValue><ColumnReference Database="db" Schema="dbo" Table="Orders" Column="Id"/></DefinedValue>
                </DefinedValues>
                <Object Database="db" Schema="dbo" Table="Orders" Index="PK_Orders" IndexKind="Clustered"/>
                <Predicate>
                  <ScalarOperator ScalarString="dbo.Orders.CustomerId=(1)"/>
                </Predicate>
              </IndexScan>
            </RelOp>
          </QueryPlan>
        </StmtSimple>
      </Statements>
    </Batch>
  </BatchSequence>
</ShowPlanXML>
""";

    [Fact]
    public void Parses_basic_plan_and_captures_predicate()
    {
        var p = ShowplanParser.ParseString(MinimalEstimatedPlan);
        Assert.False(p.IsActual);
        var stmt = Assert.Single(p.Statements);
        Assert.Equal("0xABCDEF", stmt.QueryHash);
        Assert.NotNull(stmt.Root);
        Assert.Equal("Clustered Index Scan", stmt.Root!.PhysicalOp);
        Assert.Single(stmt.Root.Objects);
        Assert.Contains("Predicate", stmt.Root.Properties.Keys);
        // Predicate scalar string captured (no truncation).
        Assert.Contains("CustomerId", stmt.Root.Properties["Predicate"]);
    }

    [Fact]
    public void Heuristic_emits_residual_predicate()
    {
        var p = ShowplanParser.ParseString(MinimalEstimatedPlan);
        var findings = Heuristics.Analyze(p).ToList();
        Assert.Contains(findings, f => f.Code == "PRED.RESIDUAL");
    }

    [Fact]
    public void Renderer_includes_synthesis_section()
    {
        var p = ShowplanParser.ParseString(MinimalEstimatedPlan);
        var r = PlanAnalyzer.Analyze(p);
        var text = TextRenderer.Render(r, AudienceLevel.Beginner);
        Assert.Contains("Spiegazione sintetica", text);
        Assert.Contains("Punti di attenzione", text);
        Assert.Contains("Top operatori per costo", text);
    }

    [Fact]
    public void Expert_level_dumps_full_properties()
    {
        var p = ShowplanParser.ParseString(MinimalEstimatedPlan);
        var r = PlanAnalyzer.Analyze(p);
        var text = TextRenderer.Render(r, AudienceLevel.Expert);
        Assert.Contains("Dettaglio completo proprietà operatori", text);
        Assert.Contains("Predicate:", text);
    }
}
