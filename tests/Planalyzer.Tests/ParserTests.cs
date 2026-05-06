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

    private const string NestedJoinPlan = """
<ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan" Version="1.564" Build="16.0">
  <BatchSequence><Batch><Statements>
    <StmtSimple StatementText="x" QueryHash="0xH" QueryPlanHash="0xP"
                StatementSubTreeCost="2" StatementEstRows="100">
      <QueryPlan>
        <RelOp NodeId="0" PhysicalOp="Hash Match" LogicalOp="Inner Join"
               EstimateRows="100" EstimatedTotalSubtreeCost="2"
               EstimateCPU="0.1" EstimateIO="0.1" Parallel="false">
          <OutputList/>
          <Hash>
            <DefinedValues/>
            <HashKeysBuild><ColumnReference Column="A"/></HashKeysBuild>
            <HashKeysProbe><ColumnReference Column="A"/></HashKeysProbe>
            <RelOp NodeId="1" PhysicalOp="Index Scan" LogicalOp="Index Scan"
                   EstimateRows="50" EstimatedTotalSubtreeCost="1"
                   EstimateCPU="0.05" EstimateIO="0.05" Parallel="false">
              <OutputList/>
              <IndexScan>
                <Object Database="db" Schema="dbo" Table="L" Index="IX_L"/>
              </IndexScan>
            </RelOp>
            <RelOp NodeId="2" PhysicalOp="Index Scan" LogicalOp="Index Scan"
                   EstimateRows="50" EstimatedTotalSubtreeCost="1"
                   EstimateCPU="0.05" EstimateIO="0.05" Parallel="false">
              <OutputList/>
              <IndexScan>
                <Object Database="db" Schema="dbo" Table="R" Index="IX_R"/>
              </IndexScan>
            </RelOp>
          </Hash>
        </RelOp>
      </QueryPlan>
    </StmtSimple>
  </Statements></Batch></BatchSequence>
</ShowPlanXML>
""";

    [Fact]
    public void Parses_child_relops_nested_inside_wrapper()
    {
        // Showplan stores child RelOps inside the operator wrapper element (Hash, NestedLoops, ...).
        // The parser must recover them as Children, not skip them.
        var p = ShowplanParser.ParseString(NestedJoinPlan);
        var root = p.Statements.Single().Root!;
        Assert.Equal("Hash Match", root.PhysicalOp);
        Assert.Equal(2, root.Children.Count);
        Assert.All(root.Children, c => Assert.Equal("Index Scan", c.PhysicalOp));
        // Promoted property: HashKeysBuild lifted to top level on the parent RelOp.
        Assert.True(root.Properties.ContainsKey("HashKeysBuild"));
        // Each leaf carries its Object via descent through IndexScan wrapper.
        Assert.Single(root.Children[0].Objects);
        Assert.Single(root.Children[1].Objects);
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
