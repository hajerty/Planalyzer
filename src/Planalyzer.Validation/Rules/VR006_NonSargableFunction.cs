using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Planalyzer.Validation.Rules;

/// <summary>
/// VR.006 — Function applied to a column in a WHERE predicate (HK: non-sargable).
/// Examples: WHERE YEAR(d)=2025, WHERE LOWER(c)='x'. Forces scan even with index.
/// </summary>
public sealed class VR006_NonSargableFunction : RuleBase
{
    public override string RuleId => "VR.006";
    public override string Title => "Funzione su colonna in WHERE (non-sargable)";
    public override Severity DefaultSeverity => Severity.High;
    public override string Source => "HK";

    public override IEnumerable<ValidationFinding> Apply(TSqlFragment root, ValidationContext ctx)
    {
        var v = new Visitor(this);
        root.Accept(v);
        return v.Findings;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        private readonly VR006_NonSargableFunction _rule;
        private int _inWhereDepth;
        public List<ValidationFinding> Findings { get; } = new();
        public Visitor(VR006_NonSargableFunction r) => _rule = r;

        public override void ExplicitVisit(WhereClause node)
        {
            _inWhereDepth++;
            try { base.ExplicitVisit(node); }
            finally { _inWhereDepth--; }
        }

        public override void Visit(BooleanComparisonExpression node)
        {
            if (_inWhereDepth == 0) return;
            if (Wraps(node.FirstExpression) || Wraps(node.SecondExpression))
            {
                Findings.Add(_rule.Finding(
                    "Funzione applicata alla colonna nel WHERE: rende il predicato non-sargable.",
                    node,
                    "Riscrivi spostando la funzione sul lato del valore (es. d >= '2025-01-01')."));
            }
        }

        private static bool Wraps(ScalarExpression? e)
        {
            if (e is not FunctionCall fc) return false;
            // Heuristic: at least one argument is a column reference.
            if (fc.Parameters is null) return false;
            foreach (var p in fc.Parameters)
                if (ContainsColumnRef(p)) return true;
            return false;
        }

        private static bool ContainsColumnRef(ScalarExpression e) => e switch
        {
            ColumnReferenceExpression => true,
            FunctionCall fc2 => fc2.Parameters?.Any(ContainsColumnRef) == true,
            ParenthesisExpression pe => ContainsColumnRef(pe.Expression),
            _ => false
        };
    }
}
