using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.016 — Too many derived/expression columns in select-list (BP).
/// Threshold from <see cref="ValidationOptions.MaxDerivedColumns"/>.
/// Heuristic: anything that is not a bare ColumnReferenceExpression counts.
/// </summary>
public sealed class VR016_DerivedColumns : RuleBase
{
    public override string RuleId => "VR.016";
    public override string Title => "Troppe colonne calcolate";
    public override Severity DefaultSeverity => Severity.Medium;
    public override string Source => "BP";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this, ctx.Options.MaxDerivedColumns);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR016_DerivedColumns _rule;
        private readonly int _max;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR016_DerivedColumns r, int max) { _rule = r; _max = max; }

        public override void Visit(QuerySpecification node)
        {
            if (node.SelectElements is null) return;
            int derived = 0;
            foreach (var se in node.SelectElements)
            {
                if (se is SelectScalarExpression sse
                    && sse.Expression is not ColumnReferenceExpression)
                {
                    derived++;
                }
            }
            if (derived > _max)
            {
                Findings.Add(_rule.Finding(
                    $"{derived} colonne calcolate nella SELECT-list (soglia {_max}).",
                    node,
                    "Sposta la logica in una view/CTE o lato applicazione."));
            }
        }
    }
}
