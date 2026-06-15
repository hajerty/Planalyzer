using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.004 — Too many joined tables (HK: optimizer time grows super-linearly,
/// early-abort plans become frequent). Threshold: MaxJoinedTables.
/// </summary>
public sealed class VR004_TooManyJoins : RuleBase
{
    public override string RuleId => "VR.004";
    public override string Title => "Troppe tabelle in JOIN";
    public override Severity DefaultSeverity => Severity.High;
    public override string Source => "HK";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this, ctx.Options.MaxJoinedTables);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR004_TooManyJoins _rule;
        private readonly int _max;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR004_TooManyJoins r, int max) { _rule = r; _max = max; }

        public override void Visit(QuerySpecification node)
        {
            if (node.FromClause is null) return;
            int count = 0;
            foreach (var tr in node.FromClause.TableReferences)
                count += CountTables(tr);
            if (count > _max)
            {
                Findings.Add(_rule.Finding(
                    $"Trovate {count} tabelle (soglia {_max}). L'ottimizzatore tende a degradare.",
                    node.FromClause,
                    "Valuta CTE intermedie, materializzazione o split logico."));
            }
        }

        private static int CountTables(TableReference tr) => tr switch
        {
            QualifiedJoin qj => CountTables(qj.FirstTableReference) + CountTables(qj.SecondTableReference),
            UnqualifiedJoin uj => CountTables(uj.FirstTableReference) + CountTables(uj.SecondTableReference),
            JoinParenthesisTableReference jp => CountTables(jp.Join),
            NamedTableReference => 1,
            QueryDerivedTable => 1,
            VariableTableReference => 1,
            SchemaObjectFunctionTableReference => 1,
            _ => 1
        };
    }
}
